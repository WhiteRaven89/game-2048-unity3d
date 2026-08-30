package main

import (
	"bytes"
	"fmt"
	"os/exec"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"time"
)

// Step is one verification gate and what it said.
type Step struct {
	Name     string
	Passed   bool
	Duration time.Duration

	// Summary is the part worth handing back to the agent: the compiler errors,
	// or the failing test names. Full output goes in the transcript, not into
	// the next prompt - a wall of MSBuild noise buries the one line that matters.
	Summary string

	Output string
}

// Verify runs every gate in order and stops at the first failure. Ordered
// cheapest-and-most-specific first: a compile error explains itself, a failing
// test needs reading, and a replay mismatch needs both.
func verify(root string, task *Task) []Step {
	solution := filepath.Join(root, "core", "Game2048.sln")

	steps := []Step{runStep("build", root, summariseBuild, "dotnet", "build", solution, "--nologo", "-v", "q")}

	if !steps[0].Passed {
		return steps
	}

	// Normal verbosity, not quiet. Quiet prints the names of failing tests and
	// nothing else, so the agent is handed "this test failed" with no statement
	// of what it expected or got - which is an invitation to guess. Normal
	// includes the assertion message, and summariseTests keeps that and discards
	// the stack frames.
	steps = append(steps, runStep("test", root, summariseTests, "dotnet", "test", solution, "--nologo", "-v", "n", "--no-build"))

	if !steps[1].Passed {
		return steps
	}

	if task.Replay != nil {
		steps = append(steps, replayStep(root, task.Replay))
	}

	return steps
}

func runStep(name, dir string, summarise func(string) string, command string, args ...string) Step {
	started := time.Now()

	cmd := exec.Command(command, args...)
	cmd.Dir = dir

	var combined bytes.Buffer
	cmd.Stdout = &combined
	cmd.Stderr = &combined

	err := cmd.Run()
	output := combined.String()

	step := Step{
		Name:     name,
		Passed:   err == nil,
		Duration: time.Since(started),
		Output:   output,
	}

	if !step.Passed {
		step.Summary = summarise(output)

		if strings.TrimSpace(step.Summary) == "" {
			step.Summary = fmt.Sprintf("%s failed: %v", name, err)
		}
	}

	return step
}

// summariseBuild keeps compiler diagnostics and drops the rest.
func summariseBuild(output string) string {
	var kept []string
	seen := map[string]bool{}

	for _, line := range splitLines(output) {
		trimmed := strings.TrimSpace(line)

		if !strings.Contains(trimmed, ": error ") && !strings.Contains(trimmed, ": warning ") {
			continue
		}

		if seen[trimmed] {
			continue
		}

		seen[trimmed] = true

		kept = append(kept, trimmed)
	}

	return strings.Join(capLines(kept, 25), "\n")
}

// summariseTests keeps the failing test names and their assertion messages.
func summariseTests(output string) string {
	lines := splitLines(output)

	var kept []string
	seen := map[string]bool{}

	for i, line := range lines {
		trimmed := strings.TrimSpace(line)

		if !strings.Contains(trimmed, "[FAIL]") {
			continue
		}

		if seen[trimmed] {
			continue
		}

		seen[trimmed] = true

		kept = append(kept, trimmed)

		// The assertion message sits directly under the name. Take a few lines of
		// it and stop at the stack trace, which is never the useful part.
		for _, follow := range lines[i+1 : min(i+5, len(lines))] {
			text := strings.TrimSpace(follow)

			if text == "" || strings.Contains(text, "[FAIL]") {
				break
			}

			if strings.Contains(text, "Stack Trace") {
				break
			}

			kept = append(kept, "    "+text)
		}
	}

	if len(kept) == 0 {
		for _, line := range lines {
			if strings.HasPrefix(strings.TrimSpace(line), "Failed!") {
				kept = append(kept, strings.TrimSpace(line))
			}
		}
	}

	return strings.Join(capLines(kept, 40), "\n")
}

// replayStep plays a recorded game through the CLI and compares the end state.
//
// This exists because build and test can both pass while the rules quietly
// change. A unit test asserts what someone thought to ask; a pinned replay
// asserts that the whole game still unfolds the way it did.
func replayStep(root string, check *ReplayCheck) Step {
	started := time.Now()

	project := filepath.Join(root, "core", "src", "Game2048.Cli")
	moves := filepath.Join(root, filepath.FromSlash(check.MovesFile))

	args := []string{
		"run", "--project", project, "--no-build", "--",
		"--seed", strconv.Itoa(check.Seed),
		"--replay", moves,
	}

	if check.Rows > 0 {
		args = append(args, "--rows", strconv.Itoa(check.Rows))
	}

	if check.Columns > 0 {
		args = append(args, "--cols", strconv.Itoa(check.Columns))
	}

	cmd := exec.Command("dotnet", args...)
	cmd.Dir = root

	var combined bytes.Buffer
	cmd.Stdout = &combined
	cmd.Stderr = &combined

	err := cmd.Run()
	output := combined.String()

	step := Step{Name: "replay", Duration: time.Since(started), Output: output}

	if err != nil {
		step.Summary = "the replay run itself failed:\n" + strings.TrimSpace(output)

		return step
	}

	actual := map[string]string{}

	for _, line := range splitLines(output) {
		if key, value, found := strings.Cut(strings.TrimSpace(line), "="); found {
			actual[key] = value
		}
	}

	var mismatches []string

	for _, key := range sortedKeys(check.Expect) {
		want := check.Expect[key]

		got, present := actual[key]
		if !present {
			mismatches = append(mismatches, fmt.Sprintf("%s: the CLI printed no such value", key))

			continue
		}

		if got != want {
			mismatches = append(mismatches, fmt.Sprintf("%s: expected %s, got %s", key, want, got))
		}
	}

	step.Passed = len(mismatches) == 0

	if !step.Passed {
		step.Summary = "the recorded game no longer plays the same way:\n" + strings.Join(mismatches, "\n")
	}

	return step
}

func sortedKeys(m map[string]string) []string {
	keys := make([]string, 0, len(m))

	for key := range m {
		keys = append(keys, key)
	}

	sort.Strings(keys)

	return keys
}

func capLines(lines []string, limit int) []string {
	if len(lines) <= limit {
		return lines
	}

	return append(lines[:limit], fmt.Sprintf("... and %d more", len(lines)-limit))
}

func min(a, b int) int {
	if a < b {
		return a
	}

	return b
}
