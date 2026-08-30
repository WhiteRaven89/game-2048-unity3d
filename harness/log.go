package main

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// appendDelegationLog adds one run to docs/DELEGATION-LOG.md.
//
// It appends and never rewrites. A log an agent loop can edit is a log that
// records only the runs it was happy with, and the runs worth reading are the
// ones that went wrong.
func appendDelegationLog(root string, run *Run) error {
	path := filepath.Join(root, "docs", "DELEGATION-LOG.md")

	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}

	if _, err := os.Stat(path); os.IsNotExist(err) {
		header := "# Delegation log\n\n" +
			"Every harness run, appended automatically by `harness/`. Nothing here is\n" +
			"edited afterwards except the **What I changed** lines, which are written by\n" +
			"hand - the column that matters is what the checks *missed*.\n"

		if writeErr := os.WriteFile(path, []byte(header), 0o644); writeErr != nil {
			return writeErr
		}
	}

	file, err := os.OpenFile(path, os.O_APPEND|os.O_WRONLY, 0o644)
	if err != nil {
		return err
	}

	defer file.Close()

	_, err = file.WriteString(renderRun(run))

	return err
}

func renderRun(run *Run) string {
	var b strings.Builder

	fmt.Fprintf(&b, "\n---\n\n## %s - %s\n\n", run.Task.ID, run.Started.Format(time.RFC3339))
	fmt.Fprintf(&b, "| | |\n|---|---|\n")
	fmt.Fprintf(&b, "| Outcome | **%s** |\n", run.Outcome)
	fmt.Fprintf(&b, "| Agent | `%s` |\n", run.Agent)
	fmt.Fprintf(&b, "| Branch | `%s` |\n", run.Branch)
	fmt.Fprintf(&b, "| Base | `%s` |\n", short(run.Base))
	fmt.Fprintf(&b, "| Iterations | %d of %d |\n", len(run.Iterations), run.Task.MaxIterations)
	fmt.Fprintf(&b, "| Elapsed | %s |\n", run.Elapsed.Round(time.Second))

	if run.Error != "" {
		fmt.Fprintf(&b, "| Harness error | %s |\n", run.Error)
	}

	fmt.Fprintf(&b, "\n**Goal**\n\n> %s\n", strings.ReplaceAll(strings.TrimSpace(run.Task.Goal), "\n", "\n> "))

	for _, iteration := range run.Iterations {
		fmt.Fprintf(&b, "\n### Iteration %d (%s)\n\n", iteration.Number, iteration.Elapsed.Round(time.Millisecond))

		if len(iteration.Violations) > 0 {
			b.WriteString("**Guardrail blocked the run:**\n\n")

			for _, violation := range iteration.Violations {
				fmt.Fprintf(&b, "- %s\n", violation)
			}

			continue
		}

		for _, step := range iteration.Steps {
			status := "passed"
			if !step.Passed {
				status = "**failed**"
			}

			fmt.Fprintf(&b, "- `%s` %s (%s)\n", step.Name, status, step.Duration.Round(time.Millisecond))
		}

		for _, step := range iteration.Steps {
			if step.Passed {
				continue
			}

			fmt.Fprintf(&b, "\n<details><summary>%s output</summary>\n\n```\n%s\n```\n\n</details>\n", step.Name, strings.TrimSpace(step.Summary))
		}
	}

	b.WriteString("\n**What came back wrong:** _(fill in)_\n\n")
	b.WriteString("**Did any check catch it:** _(fill in)_\n\n")
	b.WriteString("**What I changed:** _(fill in)_\n")

	return b.String()
}
