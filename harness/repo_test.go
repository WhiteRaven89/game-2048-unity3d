package main

import (
	"os"
	"path/filepath"
	"testing"
)

// A run branch is stamped to the minute. Two replays of the same task back to
// back - which is what the README asks a reader to do - fall in the same minute,
// and the second one used to die with "a branch named ... already exists". This
// is that case.
func TestASecondRunInTheSameMinuteGetsItsOwnBranch(t *testing.T) {
	repo := newTestRepo(t)

	const preferred = "harness/telemetry-0831-1630"

	first, err := repo.FreeBranchName(preferred)
	if err != nil {
		t.Fatal(err)
	}

	if first != preferred {
		t.Fatalf("nothing was in the way; wanted %s, got %s", preferred, first)
	}

	if err := repo.CreateBranch(first, ""); err != nil {
		t.Fatal(err)
	}

	second, err := repo.FreeBranchName(preferred)
	if err != nil {
		t.Fatal(err)
	}

	if second != preferred+"-2" {
		t.Fatalf("wanted %s-2 once %s was taken, got %s", preferred, preferred, second)
	}

	// And the first run's branch is still there, untouched. A run branch is the
	// record of a run; the fix must not reuse or overwrite one.
	exists, err := repo.BranchExists(first)
	if err != nil {
		t.Fatal(err)
	}

	if !exists {
		t.Fatalf("%s was taken over instead of stepped around", first)
	}
}

func newTestRepo(t *testing.T) *Repo {
	t.Helper()

	root := t.TempDir()

	for _, args := range [][]string{
		{"init", "-q", "-b", "main"},
		{"config", "user.email", "harness@example.invalid"},
		{"config", "user.name", "harness test"},
	} {
		if _, err := git(root, args...); err != nil {
			t.Fatal(err)
		}
	}

	if err := os.WriteFile(filepath.Join(root, "seed.txt"), []byte("x\n"), 0o644); err != nil {
		t.Fatal(err)
	}

	repo := &Repo{Root: root}

	if err := repo.CommitAll("seed"); err != nil {
		t.Fatal(err)
	}

	return repo
}
