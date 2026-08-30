package main

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// delegationLogPath is where every run is recorded, repo-relative and in git's
// forward-slash form. Named once because two places have to agree on it: the
// writer below, and the deny list that stops an agent editing its own record.
const delegationLogPath = "docs/DELEGATION-LOG.md"

const logHeader = "# Delegation log\n\n" +
	"Every harness run, appended automatically by `harness/`. Nothing here is\n" +
	"edited afterwards except the **What I changed** lines, which are written by\n" +
	"hand - the column that matters is what the checks *missed*.\n"

// takeLogCustody reads the delegation log and leaves the tree looking as though
// the harness had never written it, returning what it held.
//
// The log is the only file the harness itself puts into the repo, and any
// difference between it and HEAD during a run poisons the one check that matters:
// the diff shows a changed file the agent never touched, and the guardrail blames
// the agent for the harness's output. Teaching the diff check to ignore this path
// would open exactly the hole the deny-list entry exists to close, because an
// agent editing its own record would then look identical to the harness writing
// it.
//
// So custody means "make this file match HEAD", which is three different actions
// depending on where it stands:
//
//   - Already committed and unmodified: nothing to do. Importantly not a
//     deletion. Removing it unconditionally was the previous version of this
//     function, and it broke every run the moment the log was first committed,
//     because a tracked file removed is a dirty tree.
//   - Committed but edited by hand: restore the committed content, having kept
//     the edits to write back afterwards.
//   - Not committed at all: remove it.
func takeLogCustody(repo *Repo) (string, error) {
	path := filepath.Join(repo.Root, filepath.FromSlash(delegationLogPath))

	existing, readErr := os.ReadFile(path)
	if readErr != nil && !os.IsNotExist(readErr) {
		return "", readErr
	}

	tracked, err := repo.IsTracked(delegationLogPath)
	if err != nil {
		return "", err
	}

	if tracked {
		return string(existing), repo.RestoreFromHead(delegationLogPath)
	}

	if os.IsNotExist(readErr) {
		return "", nil
	}

	return string(existing), os.Remove(path)
}

// returnLog puts the log back, with this run appended. Appending and never
// rewriting is the point: a log that gets tidied records only the runs someone
// was happy with, and the runs worth reading are the ones that went wrong.
func returnLog(root, previous string, run *Run) error {
	path := filepath.Join(root, filepath.FromSlash(delegationLogPath))

	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}

	if strings.TrimSpace(previous) == "" {
		previous = logHeader
	}

	body := previous
	if run != nil {
		body += renderRun(run)
	}

	return os.WriteFile(path, []byte(body), 0o644)
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
