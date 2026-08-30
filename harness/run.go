package main

import (
	"fmt"
	"io"
	"strings"
	"time"
)

// Outcome is how a run ended. Named states rather than a bool, because
// "the agent could not do it" and "the agent tried to cheat" call for different
// responses from whoever reads the log.
type Outcome string

const (
	OutcomePassed    Outcome = "passed"
	OutcomeExhausted Outcome = "exhausted the iteration cap"
	OutcomeBlocked   Outcome = "blocked by a guardrail"
	OutcomeErrored   Outcome = "the harness itself failed"
)

// Iteration records one pass through the loop.
type Iteration struct {
	Number      int
	AgentOutput string
	Violations  []Violation
	Steps       []Step
	Elapsed     time.Duration
}

func (i Iteration) Passed() bool {
	if len(i.Violations) > 0 {
		return false
	}

	for _, step := range i.Steps {
		if !step.Passed {
			return false
		}
	}

	return len(i.Steps) > 0
}

// Run is the whole record of one task attempt.
type Run struct {
	Task       *Task
	Agent      string
	Branch     string
	Base       string
	Outcome    Outcome
	Error      string
	Iterations []Iteration
	Started    time.Time
	Elapsed    time.Duration
}

// execute drives one task from start to finish.
//
// The shape is deliberately boring: ask, check what came back, verify, feed the
// failure back, stop at a cap. What makes it worth anything is that every exit
// is explicit - there is no path where the loop quietly gives up and reports
// something vague.
func execute(repo *Repo, task *Task, agent Agent, timeout time.Duration, out io.Writer) *Run {
	run := &Run{
		Task:    task,
		Agent:   agent.Name(),
		Base:    repo.Base,
		Started: time.Now(),
	}

	defer func() { run.Elapsed = time.Since(run.Started) }()

	branch := fmt.Sprintf("harness/%s-%s", task.ID, time.Now().Format("0102-1504"))

	if err := repo.CreateBranch(branch); err != nil {
		run.Outcome, run.Error = OutcomeErrored, err.Error()

		return run
	}

	run.Branch = branch

	fmt.Fprintf(out, "task    %s\nagent   %s\nbranch  %s\nbase    %s\n\n", task.ID, agent.Name(), branch, short(repo.Base))

	var lastSteps []Step

	for number := 1; number <= task.MaxIterations; number++ {
		iteration := Iteration{Number: number}
		started := time.Now()

		fmt.Fprintf(out, "iteration %d/%d\n", number, task.MaxIterations)

		prompt := buildPrompt(task, number, lastSteps)

		ctx, cancel := newContext(timeout)
		output, err := agent.Work(ctx, prompt, number)
		cancel()

		iteration.AgentOutput = output

		if err != nil {
			iteration.Elapsed = time.Since(started)
			run.Iterations = append(run.Iterations, iteration)
			run.Outcome, run.Error = OutcomeErrored, err.Error()

			fmt.Fprintf(out, "  agent   FAILED  %v\n", err)

			return run
		}

		// Guardrails before verification, always. A run that wrote to a blocked
		// path has already failed, and running its tests would only produce a
		// green tick next to something that must not be accepted.
		violations, err := checkGuards(repo, task)
		if err != nil {
			iteration.Elapsed = time.Since(started)
			run.Iterations = append(run.Iterations, iteration)
			run.Outcome, run.Error = OutcomeErrored, err.Error()

			return run
		}

		iteration.Violations = violations

		if len(violations) > 0 {
			for _, violation := range violations {
				fmt.Fprintf(out, "  guard   BLOCKED %s\n", violation)
			}

			iteration.Elapsed = time.Since(started)
			run.Iterations = append(run.Iterations, iteration)
			run.Outcome = OutcomeBlocked

			// Deliberately not reverted. The violating diff is the most useful
			// thing this run produced, and it is left on the branch to be read.
			return run
		}

		iteration.Steps = verify(repo.Root, task)

		for _, step := range iteration.Steps {
			status := "ok"
			if !step.Passed {
				status = "FAILED"
			}

			fmt.Fprintf(out, "  %-7s %-7s %s\n", step.Name, status, step.Duration.Round(time.Millisecond))
		}

		iteration.Elapsed = time.Since(started)
		run.Iterations = append(run.Iterations, iteration)

		if iteration.Passed() {
			run.Outcome = OutcomePassed

			fmt.Fprintf(out, "\npassed on iteration %d\n", number)

			return run
		}

		lastSteps = iteration.Steps

		for _, step := range iteration.Steps {
			if !step.Passed {
				fmt.Fprintf(out, "\n%s\n", indent(step.Summary, "  | "))
			}
		}

		fmt.Fprintln(out)
	}

	run.Outcome = OutcomeExhausted

	// No silent give-up: say what was still wrong, and leave the branch alone so
	// a person can pick it up where the loop stopped.
	fmt.Fprintf(out, "stopped after %d iterations without passing\n", task.MaxIterations)
	fmt.Fprintf(out, "the branch %s is left as it stands for review\n", branch)

	return run
}

func indent(text, prefix string) string {
	lines := splitLines(strings.TrimRight(text, "\n"))

	for i, line := range lines {
		lines[i] = prefix + line
	}

	return strings.Join(lines, "\n")
}

func short(sha string) string {
	if len(sha) > 8 {
		return sha[:8]
	}

	return sha
}
