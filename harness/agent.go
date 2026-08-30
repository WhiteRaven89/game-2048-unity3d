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

// ExecAgent runs an external agent CLI, handing it the prompt on stdin.
type ExecAgent struct {
	Command []string
	Dir     string

	// RecordTo, when set, saves each iteration's prompt, output and resulting
	// patch, turning a live session into one ReplayAgent can play back.
	RecordTo string
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
	if err := os.MkdirAll(a.RecordTo, 0o755); err != nil {
		return err
	}

	stem := filepath.Join(a.RecordTo, fmt.Sprintf("iteration-%02d", iteration))

	if err := os.WriteFile(stem+".prompt.txt", []byte(prompt), 0o644); err != nil {
		return err
	}

	if err := os.WriteFile(stem+".output.txt", []byte(output), 0o644); err != nil {
		return err
	}

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

	return os.WriteFile(stem+".patch", []byte(patch), 0o644)
}

// ReplayAgent plays back a recorded session, one iteration at a time.
//
// It reproduces what the agent did, not what a model would say now - which is
// the point. A recorded failure stays a failure, so the self-correcting loop can
// be shown working on the same case every time.
type ReplayAgent struct {
	Dir        string // repo root
	Transcript string // directory holding iteration-NN.* files
}

func (a *ReplayAgent) Name() string { return "replay:" + a.Transcript }

func (a *ReplayAgent) Work(_ context.Context, _ string, iteration int) (string, error) {
	stem := filepath.Join(a.Transcript, fmt.Sprintf("iteration-%02d", iteration))

	output, err := os.ReadFile(stem + ".output.txt")
	if err != nil {
		return "", fmt.Errorf("transcript has no iteration %d: %w", iteration, err)
	}

	patch, err := os.ReadFile(stem + ".patch")
	if err != nil {
		return string(output), fmt.Errorf("transcript iteration %d has no patch: %w", iteration, err)
	}

	if strings.TrimSpace(string(patch)) == "" {
		return string(output), nil
	}

	// Reset first: a recorded patch is cumulative against the run's base, the
	// same way the guardrail diff is.
	if _, err := git(a.Dir, "checkout", "--", "."); err != nil {
		return string(output), err
	}

	apply := exec.Command("git", "apply", "--whitespace=nowarn", stem+".patch")
	apply.Dir = a.Dir

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
