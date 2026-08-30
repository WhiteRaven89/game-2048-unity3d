package main

import (
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"
)

const usage = `harness - run one scoped task against core/ under guardrails.

  harness -task tasks/undo-stack.json -agent "claude -p --dangerously-skip-permissions"
  harness -task tasks/undo-stack.json -replay transcripts/undo-stack
  harness -task tasks/undo-stack.json -agent "..." -record transcripts/undo-stack

Flags:
  -task     Task spec to run (required).
  -agent    Agent command. Receives the prompt on stdin and edits the working tree.
  -replay   Play a recorded transcript directory instead of calling an agent.
  -record   While using -agent, save each iteration so -replay can repeat it.
  -timeout  Per-iteration limit for the agent call (default 10m).
  -keep     Stay on the run branch afterwards instead of returning to the branch
            you started on.
  -dry-run  Check the task spec and the working tree, then stop.

Exit codes: 0 passed, 1 did not pass, 2 the harness could not run.
`

func main() {
	os.Exit(run())
}

func run() int {
	var (
		taskPath   = flag.String("task", "", "task spec json")
		agentCmd   = flag.String("agent", "", "agent command line")
		replayDir  = flag.String("replay", "", "recorded transcript directory")
		recordDir  = flag.String("record", "", "save this session as a transcript")
		timeout    = flag.Duration("timeout", 10*time.Minute, "per-iteration agent timeout")
		keepBranch = flag.Bool("keep", false, "stay on the run branch afterwards")
		dryRun     = flag.Bool("dry-run", false, "validate and stop")
	)

	flag.Usage = func() { fmt.Fprint(os.Stderr, usage) }
	flag.Parse()

	if *taskPath == "" {
		flag.Usage()

		return 2
	}

	task, err := loadTask(*taskPath)
	if err != nil {
		fmt.Fprintln(os.Stderr, "task:", err)

		return 2
	}

	repo, err := openRepo()
	if err != nil {
		fmt.Fprintln(os.Stderr, err)

		return 2
	}

	// Hold the delegation log out of the tree for the whole run, before anything
	// else looks at the tree. It is the only file the harness itself writes into
	// the repo, and every check below is cleaner for its absence: the clean-tree
	// precondition needs no exception, and the diff the guardrails read contains
	// nothing the harness put there. Restored on every exit path, including a
	// panic - losing the record of earlier runs because this one failed would be
	// the worst trade available.
	previousLog, err := takeLogCustody(repo.Root)
	if err != nil {
		fmt.Fprintln(os.Stderr, "could not take custody of the delegation log:", err)

		return 2
	}

	logRestored := false

	defer func() {
		if !logRestored {
			_ = returnLog(repo.Root, previousLog, nil)
		}
	}()

	// A run starts from a clean tree or not at all. Otherwise the guardrail
	// cannot tell what the agent did from what was already sitting there, and
	// the one check that matters becomes unreliable exactly when it is needed.
	clean, err := repo.IsClean()
	if err != nil {
		fmt.Fprintln(os.Stderr, err)

		return 2
	}

	if !clean {
		fmt.Fprintln(os.Stderr, "working tree has uncommitted changes; commit or stash them first.")
		fmt.Fprintln(os.Stderr, "the guardrails cannot tell your edits from the agent's.")

		return 2
	}

	startingBranch, err := repo.CurrentBranch()
	if err != nil {
		fmt.Fprintln(os.Stderr, err)

		return 2
	}

	agent, err := chooseAgent(repo, *agentCmd, *replayDir, *recordDir)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)

		return 2
	}

	if *dryRun {
		fmt.Printf("task    %s  (ok)\nagent   %s\nbranch  %s (clean)\n", task.ID, agent.Name(), startingBranch)

		return 0
	}

	result := execute(repo, task, agent, *timeout, os.Stdout)

	fmt.Printf("\noutcome %s\n", result.Outcome)

	if result.Branch != "" && !*keepBranch {
		// The run branch keeps whatever happened; going back to where the user
		// was means a failed run does not leave them somewhere they did not ask
		// to be. Uncommitted work on the run branch would block the checkout, so
		// commit it first - a blocked or failed attempt is worth keeping.
		if result.Outcome != OutcomePassed {
			_ = repo.CommitAll(fmt.Sprintf("harness: %s (%s)", task.ID, result.Outcome))
		} else {
			_ = repo.CommitAll(fmt.Sprintf("harness: %s", task.ID))
		}

		if err := repo.Checkout(startingBranch); err != nil {
			fmt.Fprintf(os.Stderr, "left you on %s: %v\n", result.Branch, err)
		} else {
			fmt.Printf("work is on %s; you are back on %s\n", result.Branch, startingBranch)
		}
	}

	// The log goes back last, after the checkout, and the ordering is the whole
	// point of it. Restored before, it lands on the run branch, gets committed
	// with the attempt, and disappears the moment the harness puts you back where
	// you started - so the one record meant to outlive the run is the only thing
	// guaranteed to be thrown away with it. Restored here, it lands uncommitted on
	// the branch you are actually on, which is where someone will read it.
	logRestored = true

	if err := returnLog(repo.Root, previousLog, result); err != nil {
		fmt.Fprintln(os.Stderr, "could not write the delegation log:", err)
	}

	if result.Outcome == OutcomePassed {
		return 0
	}

	if result.Outcome == OutcomeErrored {
		return 2
	}

	return 1
}

func chooseAgent(repo *Repo, agentCmd, replayDir, recordDir string) (Agent, error) {
	if replayDir != "" && agentCmd != "" {
		return nil, fmt.Errorf("choose one of -agent and -replay, not both")
	}

	if replayDir != "" {
		path := replayDir
		if !filepath.IsAbs(path) {
			path = filepath.Join(repo.Root, "harness", replayDir)
		}

		if _, err := os.Stat(path); err != nil {
			return nil, fmt.Errorf("transcript %s: %w", replayDir, err)
		}

		return &ReplayAgent{Dir: repo.Root, Transcript: path}, nil
	}

	if agentCmd == "" {
		return nil, fmt.Errorf("pass -agent with a command, or -replay with a recorded transcript")
	}

	record := recordDir
	if record != "" && !filepath.IsAbs(record) {
		record = filepath.Join(repo.Root, "harness", recordDir)
	}

	return &ExecAgent{Command: strings.Fields(agentCmd), Dir: repo.Root, RecordTo: record}, nil
}
