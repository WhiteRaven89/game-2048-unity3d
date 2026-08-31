package main

import (
	"bytes"
	"context"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"
)

// Agent is whatever writes the code. Two implementations, both real:
//
//   - ExecAgent shells out to an agent CLI and lets it edit the working tree.
//   - ReplayAgent applies a recorded patch from a previous ExecAgent run.
//
// The interface exists for the second one. A live agent needs a network, an API
// key and a working rate limit, none of which are guaranteed during a
// walkthrough - and a loop that can only be demonstrated when three external
// things cooperate cannot be demonstrated. Replay makes a real past session
// reproducible on demand.
type Agent interface {
	// Name identifies the agent in the transcript.
	Name() string

	// Work applies changes to the working tree and returns what it said. It does
	// not report success: whether the work is any good is the harness's question,
	// answered by verification, not the agent's to claim.
	Work(ctx context.Context, prompt string, iteration int) (string, error)
}

// RecordedBase is implemented by an agent that replays a session captured at a
// known commit. A recorded patch is a diff against that commit, so replaying it
// anywhere else is not reproducing the session - it is applying a patch to a tree
// it was never written for, which fails as soon as the work is merged.
type RecordedBase interface {
	RecordedBase() string
}

// ExecAgent runs an external agent CLI, handing it the prompt on stdin.
type ExecAgent struct {
	Command []string
	Dir     string

	// Base is the commit the run started from, written into the transcript so a
	// later replay can return to it.
	Base string

	// RecordTo, when set, saves each iteration's prompt, output and resulting
	// patch, turning a live session into one ReplayAgent can play back.
	RecordTo string

	// Recordings are held in memory until Flush, deliberately. Transcripts live
	// under harness/, which is a denied path, so writing them during a run made
	// the guardrail block the harness's own recorder and attribute it to the
	// agent - the same mistake the delegation log made, in a second place. The
	// agent's diff has to contain only the agent's work.
	recordings []recording
}

type recording struct {
	iteration int
	prompt    string
	output    string
	patch     string
}

func (a *ExecAgent) Name() string { return "exec:" + strings.Join(a.Command, " ") }

func (a *ExecAgent) Work(ctx context.Context, prompt string, iteration int) (string, error) {
	if len(a.Command) == 0 {
		return "", fmt.Errorf("no agent command configured; pass -agent or use -replay")
	}

	cmd := exec.CommandContext(ctx, a.Command[0], a.Command[1:]...)
	cmd.Dir = a.Dir
	cmd.Stdin = strings.NewReader(prompt)

	var combined bytes.Buffer
	cmd.Stdout = &combined
	cmd.Stderr = &combined

	err := cmd.Run()
	output := combined.String()

	if a.RecordTo != "" {
		if recordErr := a.record(iteration, prompt, output); recordErr != nil {
			return output, recordErr
		}
	}

	if err != nil {
		return output, fmt.Errorf("agent command failed: %w", err)
	}

	return output, nil
}

func (a *ExecAgent) record(iteration int, prompt, output string) error {
	// The patch is what makes the transcript replayable. Intent-to-add first, so
	// files the agent created are in it: a transcript missing a new file replays
	// as a build error, which reads like the agent's mistake rather than the
	// recorder's.
	if _, err := git(a.Dir, "add", "-N", "."); err != nil {
		return err
	}

	patch, err := git(a.Dir, "diff", "HEAD")
	if err != nil {
		return err
	}

	a.recordings = append(a.recordings, recording{
		iteration: iteration,
		prompt:    prompt,
		output:    output,
		patch:     patch,
	})

	return nil
}

// Flush writes the recorded session to disk. Called once the run is over and the
// guardrails have had their look, never during.
func (a *ExecAgent) Flush() error {
	if a.RecordTo == "" || len(a.recordings) == 0 {
		return nil
	}

	if err := os.MkdirAll(a.RecordTo, 0o755); err != nil {
		return err
	}

	// The commit every patch below is a diff against. Without it a transcript is
	// only replayable until someone merges the work it recorded.
	if a.Base != "" {
		if err := os.WriteFile(filepath.Join(a.RecordTo, "base.txt"), []byte(a.Base+"\n"), 0o644); err != nil {
			return err
		}
	}

	for _, entry := range a.recordings {
		stem := filepath.Join(a.RecordTo, fmt.Sprintf("iteration-%02d", entry.iteration))

		for suffix, content := range map[string]string{
			".prompt.txt": entry.prompt,
			".output.txt": entry.output,
			".patch":      entry.patch,
		} {
			if err := os.WriteFile(stem+suffix, []byte(content), 0o644); err != nil {
				return err
			}
		}
	}

	return nil
}

// ReplayAgent plays back a recorded session, one iteration at a time.
//
// It reproduces what the agent did, not what a model would say now - which is
// the point. A recorded failure stays a failure, so the self-correcting loop can
// be shown working on the same case every time.
//
// The whole transcript is read into memory up front, before the run branch is
// created. It has to be: replaying at the recorded base checks out a commit from
// before the transcript was committed, and reading the files lazily then finds an
// empty directory. The transcript describes the run; it cannot also be subject to
// it.
type ReplayAgent struct {
	Dir        string // repo root
	Transcript string // directory the session was loaded from

	base       string
	iterations map[int]recordedIteration
}

type recordedIteration struct {
	output string
	patch  string
}

func newReplayAgent(dir, transcript string) (*ReplayAgent, error) {
	agent := &ReplayAgent{
		Dir:        dir,
		Transcript: transcript,
		iterations: map[int]recordedIteration{},
	}

	if content, err := os.ReadFile(filepath.Join(transcript, "base.txt")); err == nil {
		agent.base = strings.TrimSpace(string(content))
	}

	for number := 1; ; number++ {
		stem := filepath.Join(transcript, fmt.Sprintf("iteration-%02d", number))

		output, err := os.ReadFile(stem + ".output.txt")
		if err != nil {
			break
		}

		patch, err := os.ReadFile(stem + ".patch")
		if err != nil {
			return nil, fmt.Errorf("iteration %d has an output but no patch: %w", number, err)
		}

		agent.iterations[number] = recordedIteration{output: string(output), patch: string(patch)}
	}

	if len(agent.iterations) == 0 {
		return nil, fmt.Errorf("%s holds no iterations", transcript)
	}

	return agent, nil
}

func (a *ReplayAgent) Name() string { return "replay:" + a.Transcript }

// RecordedBase returns the commit this session was captured against, or "" for a
// transcript that does not name one - the hand-authored fixtures, which are
// regenerated against whatever is current and so replay from HEAD.
func (a *ReplayAgent) RecordedBase() string { return a.base }

func (a *ReplayAgent) Work(_ context.Context, _ string, iteration int) (string, error) {
	recorded, ok := a.iterations[iteration]
	if !ok {
		return "", fmt.Errorf("this session ran %d iterations; the loop asked for %d", len(a.iterations), iteration)
	}

	output, patch := recorded.output, recorded.patch

	// Reset first, and unconditionally. A recorded patch is cumulative against the
	// run's base, the same way the guardrail diff is, so the tree has to be back at
	// base before it is applied - including when the patch is empty, which is how a
	// recorded iteration says "I reverted what I did last time". Skipping the reset
	// in that case left the previous iteration's breakage in place and made the
	// recovery unreplayable.
	if _, err := git(a.Dir, "reset", "--hard", "HEAD"); err != nil {
		return string(output), err
	}

	// Untracked files from a previous iteration would survive the reset. Ignored
	// files are left alone: -d without -x.
	if _, err := git(a.Dir, "clean", "-fd"); err != nil {
		return string(output), err
	}

	if strings.TrimSpace(string(patch)) == "" {
		return string(output), nil
	}

	// Fed on stdin rather than by path, for the same reason the transcript is held
	// in memory: the file it came from may not exist in the tree being replayed.
	apply := exec.Command("git", "apply", "--whitespace=nowarn", "-")
	apply.Dir = a.Dir
	apply.Stdin = strings.NewReader(patch)

	var stderr bytes.Buffer
	apply.Stderr = &stderr

	if err := apply.Run(); err != nil {
		return string(output), fmt.Errorf("could not apply recorded patch: %w: %s", err, stderr.String())
	}

	return string(output), nil
}

// buildPrompt assembles what the agent is told. The task goal never changes
// between iterations; only the failure report does.
func buildPrompt(task *Task, iteration int, failures []Step) string {
	var b strings.Builder

	b.WriteString("You are working in a .NET solution at core/.\n\n")
	b.WriteString("TASK (this is fixed and not open to reinterpretation):\n")
	b.WriteString(task.Goal)
	b.WriteString("\n\nRULES:\n")
	b.WriteString("- You may only create or edit files matching: " + strings.Join(task.WritePaths, ", ") + "\n")
	b.WriteString("- You may not touch core/tests/**, Assets/**, ProjectSettings/**, harness/**, AGENTS.md or .gitignore.\n")
	b.WriteString("- You may not add any NuGet package.\n")
	b.WriteString("- Keep the change under " + fmt.Sprint(task.MaxChangedLines) + " changed lines.\n")
	b.WriteString("- Every non-obvious line must be explicable to a reviewer.\n")

	if iteration > 1 && len(failures) > 0 {
		b.WriteString("\nYOUR PREVIOUS ATTEMPT FAILED VERIFICATION.\n")

		for _, step := range failures {
			if step.Passed {
				continue
			}

			b.WriteString("\n--- " + step.Name + " ---\n")
			b.WriteString(step.Summary)
			b.WriteString("\n")
		}

		b.WriteString("\nFix the cause. Do not change the tests; they are correct and you cannot write to them.\n")
	}

	return b.String()
}

func newContext(timeout time.Duration) (context.Context, context.CancelFunc) {
	return context.WithTimeout(context.Background(), timeout)
}
