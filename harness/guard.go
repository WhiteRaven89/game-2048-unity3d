package main

import (
	"fmt"
	"path"
	"strconv"
	"strings"
)

// deniedPaths can never be written by an agent, whatever a task spec asks for.
//
// Each entry is here because of a specific way a run can look like it worked
// when it did not - not because the directory seemed important.
var deniedPaths = []struct {
	prefix string
	why    string
}{
	{"Assets/", "the Unity project is the untouched 'before' half of the comparison"},
	{"ProjectSettings/", "same reason as Assets/"},
	{"core/tests/", "an agent that can edit the tests can always pass them"},
	{"AGENTS.md", "the rules of the run are not the agent's to rewrite"},
	{"harness/", "the harness judges the run; it cannot also be the thing under change"},
	{".gitignore", "an ignore rule would hide a new file from the diff check below"},
}

// isDeniedPattern reports whether a task's writePath would reach into a denied
// area. Checked when the task loads, so a misconfigured spec fails before an
// agent is ever invoked rather than being caught afterwards.
func isDeniedPattern(pattern string) (bool, string) {
	// Compare on the literal part of the pattern - everything before the first
	// wildcard. Reasoning about the glob itself is how "**" slipped through an
	// earlier version of this function: it matches every denied path and looked
	// like none of them.
	normalised := strings.TrimPrefix(strings.ReplaceAll(pattern, "\\", "/"), "./")

	prefix := normalised
	if i := strings.IndexAny(normalised, "*?["); i >= 0 {
		prefix = normalised[:i]
	}

	for _, denied := range deniedPaths {
		trimmed := strings.TrimSuffix(denied.prefix, "/")

		// The pattern points into denied ground...
		if prefix == trimmed || strings.HasPrefix(prefix, denied.prefix) {
			return true, denied.why
		}

		// ...or denied ground sits underneath it, which is the same problem seen
		// from the other end. An empty prefix - a bare "**" - lands here against
		// every entry, which is correct: it asks for everything.
		if strings.HasPrefix(trimmed+"/", prefix) {
			return true, denied.why
		}
	}

	return false, ""
}

// Violation is one reason a run must stop. Runs abort on the first one rather
// than repairing it: a guardrail that quietly fixes what an agent did wrong
// teaches nobody anything, and the interesting information is the attempt.
type Violation struct {
	Kind   string
	Detail string
}

func (v Violation) String() string { return v.Kind + ": " + v.Detail }

// checkGuards inspects what actually changed on disk after an agent has run.
//
// The agent CLI is told which paths it may write, but that instruction is a
// hint, not a control: it belongs to whichever vendor's tool is being driven,
// it means something slightly different in each of them, and it is not
// verifiable from here. What is verifiable is the diff.
func checkGuards(repo *Repo, task *Task) ([]Violation, error) {
	changed, err := repo.ChangedPaths()
	if err != nil {
		return nil, err
	}

	var violations []Violation

	for _, file := range changed {
		if denied, why := isDeniedPath(file); denied {
			violations = append(violations, Violation{
				Kind:   "wrote a blocked path",
				Detail: fmt.Sprintf("%s (%s)", file, why),
			})

			continue
		}

		if !matchesAny(file, task.WritePaths) {
			violations = append(violations, Violation{
				Kind:   "wrote outside the task's writePaths",
				Detail: fmt.Sprintf("%s is not matched by %s", file, strings.Join(task.WritePaths, ", ")),
			})
		}
	}

	added, err := repo.AddedPackageReferences()
	if err != nil {
		return nil, err
	}

	for _, reference := range added {
		violations = append(violations, Violation{
			Kind:   "added a dependency",
			Detail: strings.TrimSpace(reference),
		})
	}

	lines, err := repo.ChangedLineCount()
	if err != nil {
		return nil, err
	}

	if lines > task.MaxChangedLines {
		violations = append(violations, Violation{
			Kind: "change is too large to review",
			Detail: strconv.Itoa(lines) + " lines changed, cap is " +
				strconv.Itoa(task.MaxChangedLines),
		})
	}

	return violations, nil
}

func isDeniedPath(file string) (bool, string) {
	normalised := strings.ReplaceAll(file, "\\", "/")

	for _, denied := range deniedPaths {
		trimmed := strings.TrimSuffix(denied.prefix, "/")

		if normalised == trimmed || strings.HasPrefix(normalised, denied.prefix) {
			return true, denied.why
		}
	}

	return false, ""
}

func matchesAny(file string, patterns []string) bool {
	normalised := strings.ReplaceAll(file, "\\", "/")

	for _, pattern := range patterns {
		if matches(normalised, strings.ReplaceAll(pattern, "\\", "/")) {
			return true
		}
	}

	return false
}

// matches supports exactly two forms: a "dir/**" prefix, and path.Match for
// everything else. Two forms are enough for every task written so far, and a
// guardrail nobody can predict the behaviour of is not a guardrail.
func matches(file, pattern string) bool {
	if strings.HasSuffix(pattern, "/**") {
		return strings.HasPrefix(file, strings.TrimSuffix(pattern, "**"))
	}

	ok, err := path.Match(pattern, file)

	return err == nil && ok
}
