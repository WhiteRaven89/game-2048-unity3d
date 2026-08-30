package main

import (
	"encoding/json"
	"fmt"
	"os"
	"strings"
)

// Task is the frozen statement of what a run is for.
//
// It is read once, before the agent is invoked, and never written back. That is
// the point: an agent that can edit its own task can always succeed, by deciding
// it was asked for whatever it managed to produce.
type Task struct {
	ID   string `json:"id"`
	Goal string `json:"goal"`

	// WritePaths may narrow what a run is allowed to touch. It can never widen
	// it - everything in deniedPaths stays denied whatever a task says.
	WritePaths []string `json:"writePaths"`

	MaxIterations   int `json:"maxIterations"`
	MaxChangedLines int `json:"maxChangedLines"`

	// Replay optionally pins the end state of a recorded game, so a change that
	// builds and passes the unit tests but silently alters the rules still fails.
	Replay *ReplayCheck `json:"replay,omitempty"`
}

// ReplayCheck asserts the CLI still plays a known game the same way.
type ReplayCheck struct {
	Seed      int    `json:"seed"`
	MovesFile string `json:"movesFile"`
	Rows      int    `json:"rows"`
	Columns   int    `json:"columns"`

	// Expect holds key=value lines the CLI must print. Only the keys named here
	// are checked, so a task that deliberately changes the score can pin cells
	// alone.
	Expect map[string]string `json:"expect"`
}

func loadTask(path string) (*Task, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}

	var task Task

	decoder := json.NewDecoder(strings.NewReader(string(raw)))
	decoder.DisallowUnknownFields() // a typo in a task file is a failure, not a default

	if err := decoder.Decode(&task); err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}

	if err := task.validate(); err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}

	return &task, nil
}

func (t *Task) validate() error {
	switch {
	case strings.TrimSpace(t.ID) == "":
		return fmt.Errorf("task needs an id")
	case strings.TrimSpace(t.Goal) == "":
		return fmt.Errorf("task needs a goal")
	case len(t.WritePaths) == 0:
		return fmt.Errorf("task needs at least one writePath")
	case t.MaxIterations < 1:
		return fmt.Errorf("maxIterations must be at least 1")
	case t.MaxChangedLines < 1:
		return fmt.Errorf("maxChangedLines must be at least 1")
	}

	for _, pattern := range t.WritePaths {
		if denied, why := isDeniedPattern(pattern); denied {
			return fmt.Errorf("writePath %q is not allowed: %s", pattern, why)
		}
	}

	if t.Replay != nil {
		if t.Replay.MovesFile == "" {
			return fmt.Errorf("replay needs a movesFile")
		}

		if len(t.Replay.Expect) == 0 {
			return fmt.Errorf("replay needs at least one expected value")
		}
	}

	return nil
}
