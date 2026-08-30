package main

import "testing"

// The path rules are the only pure logic in the harness, and they are the part
// that must not be wrong: everything else fails loudly, while a matcher that is
// slightly too generous fails by letting something through.

func TestDeniedPathsAreRefusedHoweverTheyAreWritten(t *testing.T) {
	denied := []string{
		"core/tests/RulesMoveTests.cs",
		"core/tests/Game2048.Core.Tests/BoardTests.cs",
		"Assets/Src/Managers/LevelManager.cs",
		"ProjectSettings/ProjectVersion.txt",
		"AGENTS.md",
		".gitignore",
		"harness/guard.go",
		// Windows separators, because git is not the only thing that names a file.
		"core\\tests\\RulesMoveTests.cs",
	}

	for _, file := range denied {
		if blocked, _ := isDeniedPath(file); !blocked {
			t.Errorf("%q should be blocked and is not", file)
		}
	}
}

func TestOrdinarySourcePathsAreNotBlocked(t *testing.T) {
	allowed := []string{
		"core/src/Game2048.Core/Rules.cs",
		"core/src/Game2048.Cli/Program.cs",
		"docs/FINDINGS.md",
		"README.md",
	}

	for _, file := range allowed {
		if blocked, why := isDeniedPath(file); blocked {
			t.Errorf("%q should be allowed, blocked as: %s", file, why)
		}
	}
}

func TestNamesThatMerelyResembleABlockedPathAreAllowed(t *testing.T) {
	// A prefix check is easy to write too loosely. "core/tests-old" starts with
	// "core/tests" as a string but is not inside it, and a source file named
	// after the harness is not the harness.
	allowed := []string{
		"core/testsuite/Notes.md",
		"core/src/Game2048.Core/Harness.cs",
		"docs/AGENTS.md",
		"core/src/Game2048.Core/AssetsLoader.cs",
	}

	for _, file := range allowed {
		if blocked, why := isDeniedPath(file); blocked {
			t.Errorf("%q should be allowed, blocked as: %s", file, why)
		}
	}
}

func TestATaskCannotWidenItsWritePathsIntoDeniedGround(t *testing.T) {
	// The deny list is not a default a task can override. Anything that would
	// reach a blocked directory is refused when the spec loads, before an agent
	// is ever started.
	widening := []string{
		"core/tests/**",
		"core/**",       // contains core/tests
		"**",            // contains everything
		"Assets/Src/**", //
		"AGENTS.md",
	}

	for _, pattern := range widening {
		if blocked, _ := isDeniedPattern(pattern); !blocked {
			t.Errorf("writePath %q should be refused and is not", pattern)
		}
	}
}

func TestLegitimateWritePathsAreAccepted(t *testing.T) {
	fine := []string{
		"core/src/**",
		"core/src/Game2048.Core/**",
		// Allowed even though the delegation log lives under it. A denied *file*
		// does not poison the directory around it - the per-file check still
		// refuses the log itself - or no task could ever write documentation.
		"docs/**",
	}

	for _, pattern := range fine {
		if blocked, why := isDeniedPattern(pattern); blocked {
			t.Errorf("writePath %q should be accepted, refused as: %s", pattern, why)
		}
	}
}

func TestTheDelegationLogCannotBeWrittenEvenUnderAnAllowedScope(t *testing.T) {
	// The pairing that makes the previous test safe: docs/** is a legal scope,
	// and the log inside it is still refused when the diff is checked.
	if blocked, _ := isDeniedPattern("docs/DELEGATION-LOG.md"); !blocked {
		t.Error("a task should not be able to name the delegation log as a writePath")
	}

	if blocked, _ := isDeniedPath("docs/DELEGATION-LOG.md"); !blocked {
		t.Error("writing the delegation log should be refused")
	}

	if blocked, _ := isDeniedPath("docs/FINDINGS.md"); blocked {
		t.Error("an ordinary doc should still be writable")
	}
}

func TestWritePathMatching(t *testing.T) {
	cases := []struct {
		file    string
		pattern string
		want    bool
	}{
		{"core/src/Game2048.Core/Rules.cs", "core/src/**", true},
		{"core/src/Game2048.Cli/Program.cs", "core/src/**", true},
		{"core/tests/BoardTests.cs", "core/src/**", false},
		{"docs/FINDINGS.md", "core/src/**", false},
		{"core/src/Game2048.Core/Rules.cs", "core/src/Game2048.Core/**", true},
		{"core/src/Game2048.Cli/Program.cs", "core/src/Game2048.Core/**", false},
		// Backslashes normalise, so a Windows-shaped path is matched the same way.
		{"core\\src\\Game2048.Core\\Rules.cs", "core/src/**", true},
		// A bare glob still works for a single file.
		{"README.md", "README.md", true},
		{"docs/README.md", "README.md", false},
	}

	for _, test := range cases {
		if got := matchesAny(test.file, []string{test.pattern}); got != test.want {
			t.Errorf("matchesAny(%q, %q) = %v, want %v", test.file, test.pattern, got, test.want)
		}
	}
}

func TestTaskValidationRefusesAnUnusableSpec(t *testing.T) {
	cases := map[string]Task{
		"no id":             {Goal: "x", WritePaths: []string{"core/src/**"}, MaxIterations: 1, MaxChangedLines: 1},
		"no goal":           {ID: "x", WritePaths: []string{"core/src/**"}, MaxIterations: 1, MaxChangedLines: 1},
		"no write paths":    {ID: "x", Goal: "x", MaxIterations: 1, MaxChangedLines: 1},
		"no iterations":     {ID: "x", Goal: "x", WritePaths: []string{"core/src/**"}, MaxChangedLines: 1},
		"no line cap":       {ID: "x", Goal: "x", WritePaths: []string{"core/src/**"}, MaxIterations: 1},
		"denied write path": {ID: "x", Goal: "x", WritePaths: []string{"core/tests/**"}, MaxIterations: 1, MaxChangedLines: 1},
		"replay with no expectations": {
			ID: "x", Goal: "x", WritePaths: []string{"core/src/**"}, MaxIterations: 1, MaxChangedLines: 1,
			Replay: &ReplayCheck{Seed: 1, MovesFile: "m.txt"},
		},
	}

	for name, task := range cases {
		if err := task.validate(); err == nil {
			t.Errorf("%s: expected the spec to be refused", name)
		}
	}
}

func TestAValidSpecPasses(t *testing.T) {
	task := Task{
		ID:              "undo-stack",
		Goal:            "add an undo stack",
		WritePaths:      []string{"core/src/**"},
		MaxIterations:   4,
		MaxChangedLines: 300,
	}

	if err := task.validate(); err != nil {
		t.Fatalf("valid spec refused: %v", err)
	}
}
