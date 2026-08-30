package main

import (
	"bytes"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
)

// Repo is the git working tree a run happens in.
type Repo struct {
	Root string

	// Base is the commit a run started from. Every diff is taken against it, so
	// a guardrail sees the whole run rather than one iteration at a time - an
	// agent that writes a blocked file and reverts it on the next pass has still
	// written a blocked file, and this notices.
	Base string
}

func openRepo() (*Repo, error) {
	root, err := git("", "rev-parse", "--show-toplevel")
	if err != nil {
		return nil, fmt.Errorf("not inside a git repository: %w", err)
	}

	repo := &Repo{Root: filepath.FromSlash(strings.TrimSpace(root))}

	head, err := repo.run("rev-parse", "HEAD")
	if err != nil {
		return nil, err
	}

	repo.Base = strings.TrimSpace(head)

	return repo, nil
}

func (r *Repo) run(args ...string) (string, error) { return git(r.Root, args...) }

func git(dir string, args ...string) (string, error) {
	command := exec.Command("git", args...)
	command.Dir = dir

	var stdout, stderr bytes.Buffer
	command.Stdout = &stdout
	command.Stderr = &stderr

	if err := command.Run(); err != nil {
		return "", fmt.Errorf("git %s: %w: %s", strings.Join(args, " "), err, strings.TrimSpace(stderr.String()))
	}

	return stdout.String(), nil
}

// trackNewFiles records the existence of every new file without staging its
// contents.
//
// It is called before anything reads a diff, and it is the reason the rest of
// this file has one code path instead of two. A brand new file is invisible to
// `git diff`, so a check written only against the diff would miss the easiest
// way to slip work past it - and a second untracked-file code path is a second
// thing to get wrong. Intent-to-add makes new files show up in every ordinary
// diff, so every check below sees them for free.
func (r *Repo) trackNewFiles() error {
	_, err := r.run("add", "-N", ".")

	return err
}

// IsClean reports whether the tree has anything in it the harness did not put
// there. A run refuses to start otherwise, because a guardrail cannot tell an
// agent's edit from one that was already sitting there.
//
// The delegation log is the single exception, and it has to be: the harness
// appends to it at the end of every run, so requiring a spotless tree would mean
// a second run could never follow a first without a commit in between - and two
// runs back to back is the normal way this gets used.
func (r *Repo) IsClean() (bool, error) {
	out, err := r.run("status", "--porcelain")
	if err != nil {
		return false, err
	}

	for _, line := range splitLines(out) {
		entry := strings.TrimSpace(line)

		if entry == "" {
			continue
		}

		// Porcelain lines are "XY path"; the status letters are never a path.
		fields := strings.Fields(entry)
		path := fields[len(fields)-1]

		if path != delegationLogPath && path != "docs/" {
			return false, nil
		}
	}

	return true, nil
}

func (r *Repo) CurrentBranch() (string, error) {
	out, err := r.run("rev-parse", "--abbrev-ref", "HEAD")

	return strings.TrimSpace(out), err
}

func (r *Repo) CreateBranch(name string) error {
	_, err := r.run("checkout", "-b", name)

	return err
}

func (r *Repo) Checkout(ref string) error {
	_, err := r.run("checkout", ref)

	return err
}

// ChangedPaths lists every path that differs from Base, new files included.
func (r *Repo) ChangedPaths() ([]string, error) {
	if err := r.trackNewFiles(); err != nil {
		return nil, err
	}

	out, err := r.run("diff", "--name-only", r.Base)
	if err != nil {
		return nil, err
	}

	var paths []string

	for _, line := range splitLines(out) {
		if file := strings.TrimSpace(line); file != "" {
			paths = append(paths, file)
		}
	}

	return paths, nil
}

// AddedPackageReferences returns every PackageReference line the run introduced,
// in an existing project file or a new one.
func (r *Repo) AddedPackageReferences() ([]string, error) {
	if err := r.trackNewFiles(); err != nil {
		return nil, err
	}

	diff, err := r.run("diff", "--unified=0", r.Base, "--", "*.csproj", "*.props", "*.targets")
	if err != nil {
		return nil, err
	}

	var added []string

	for _, line := range splitLines(diff) {
		if strings.HasPrefix(line, "+") && strings.Contains(line, "<PackageReference") {
			added = append(added, strings.TrimSpace(strings.TrimPrefix(line, "+")))
		}
	}

	return added, nil
}

// ChangedLineCount totals added and removed lines across the whole run.
func (r *Repo) ChangedLineCount() (int, error) {
	if err := r.trackNewFiles(); err != nil {
		return 0, err
	}

	numstat, err := r.run("diff", "--numstat", r.Base)
	if err != nil {
		return 0, err
	}

	total := 0

	for _, line := range splitLines(numstat) {
		fields := strings.Fields(line)

		if len(fields) < 2 {
			continue
		}

		// "-" appears for binary files; count those as zero rather than failing.
		for _, field := range fields[:2] {
			if count, convErr := strconv.Atoi(field); convErr == nil {
				total += count
			}
		}
	}

	return total, nil
}

// Patch is the whole run as a diff, for recording a transcript.
func (r *Repo) Patch() (string, error) {
	if err := r.trackNewFiles(); err != nil {
		return "", err
	}

	return r.run("diff", r.Base)
}

func (r *Repo) CommitAll(message string) error {
	if _, err := r.run("add", "-A"); err != nil {
		return err
	}

	_, err := r.run("commit", "-m", message)

	return err
}

// WriteFile is used by tests to place a file in the tree.
func (r *Repo) WriteFile(relative, content string) error {
	full := filepath.Join(r.Root, filepath.FromSlash(relative))

	if err := os.MkdirAll(filepath.Dir(full), 0o755); err != nil {
		return err
	}

	return os.WriteFile(full, []byte(content), 0o644)
}

func splitLines(text string) []string {
	return strings.Split(strings.ReplaceAll(text, "\r\n", "\n"), "\n")
}
