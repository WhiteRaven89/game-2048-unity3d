# game-2048-unity3d

A 2048 game written in Unity in 2022. On the `demo/pure-core` branch it is also
something else: the subject of an extraction.

**Agent output is cheap and verification is the bottleneck.** What is on this branch
is a rules core that can be verified in four seconds, the tests that verification
consists of, the three bugs those tests found in the original, and a harness that
runs coding agents against it under checks that fail loudly.

`Assets/` is frozen. It is the "before" half of the comparison and not one line of it
has changed.

---

## Run it

```bash
dotnet test core/Game2048.sln          # 167 tests, ~4s including build
dotnet run --project core/src/Game2048.Cli          # play it in the terminal
dotnet run --project core/src/Game2048.Cli -- --seed 7 --replay harness/testdata/moves.txt
```

The harness (Go 1.26, standard library only):

```bash
cd harness && go build -o harness.exe . && cd ..

./harness/harness.exe -task harness/tasks/guard-demo.json -replay transcripts/fixtures/guard-demo
./harness/harness.exe -task harness/tasks/telemetry.json  -replay transcripts/fixtures/self-heal
```

The first is refused by a guardrail. The second fails on iteration 1 and passes on
iteration 2. Both run offline with no agent CLI and no API key — see
[the fixture note](harness/transcripts/fixtures/README.md) about what they are and
are not.

## Layout

```
Assets/                       the original game. Frozen.
core/
  src/Game2048.Core/          the rules. netstandard2.1, zero dependencies
  src/Game2048.Cli/           terminal front-end, plays and replays
  src/Game2048.Legacy/        the original's two buggy functions, ported faithfully
  tests/                      167 tests. Agents may not write here
harness/                      Go tool: one scoped task, run under guardrails
docs/
  FINDINGS.md                 the three bugs, and why four years of play missed them
  EXTRACTION.md               before and after, with the reasoning for each change
  DELEGATION-LOG.md           every harness run, appended automatically
AGENTS.md                     the rules every session works under
```

## The three bugs

All in [`LevelManager.cs`](Assets/Src/Managers/LevelManager.cs), all four years old,
none fixed in place. Full write-ups in [docs/FINDINGS.md](docs/FINDINGS.md).

1. **The lose condition is unreachable.** `IsMoveLeft()` dereferences neighbours
   before bounds-checking them, and guards each with the wrong axis. On a full board
   it either returns `true` or throws — over 500 random full boards it returned
   `false` zero times. The function that exists to answer "has the player lost?"
   cannot produce that answer.
2. **The winning tile renders as a 2.** `SpawnTileIndex()`'s switch stops at 1024 and
   its default returns index 0, which is the "2" prefab.
3. **A move that changes nothing still spawns a tile.** Nothing asks whether the
   board changed, so holding a direction against a wall hands out free tiles.

Each lives at a boundary — the lose condition, the win condition, and an input that
does nothing. Play exercises the middle of the state space and almost never its
edges. That is the argument for testable systems over tested ones, and it is why
these three are the ones that survived.

## Why the harness looks like this

Every guardrail traces to a specific way a run can look like it worked when it did
not, and several were added after the loop caught something:

- **`core/tests/**` is unwritable.** The standard way an agent loop fakes success is
  editing the test until it agrees with the code. Blocking the path removes the
  option instead of asking nicely.
- **The guardrail reads the diff, not the agent's permissions.** Whatever deny-rules
  an agent CLI supports are passed along as a hint. They belong to that vendor, mean
  something different in each tool, and cannot be verified from outside. The diff can.
- **Diffs are taken against the run's base**, not per iteration — an agent that
  writes a blocked file and reverts it next pass has still written a blocked file.
- **`.gitignore` and `.gitattributes` are unwritable**, because either can hide a
  change from the check that reads the diff.
- **The delegation log is lifted out of the tree during a run.** The harness writes
  it, so leaving it there made the guardrail blame the agent for the harness's own
  output on every second run. Teaching the check to ignore that path would have
  opened the hole the deny-list entry exists to close.

[docs/DELEGATION-LOG.md](docs/DELEGATION-LOG.md) records every run. The column worth
reading is what the checks *missed*.

## What is not here

Said plainly rather than left to be discovered:

- **Unity is not wired to the new core.** Riskiest integration, nothing new proved.
- **No live agent run is recorded yet.** The transcripts under
  `harness/transcripts/fixtures/` are hand-authored test data, labelled as such. The
  `Agent` interface has a working `ExecAgent` that drives any CLI reading a prompt on
  stdin, and `-record` turns a real session into a replayable transcript.
- **`Game2048.Legacy` ports two functions, not the whole `LevelManager`.**
- **The CLI is only partly tested.** `MoveScript` is, because a misparse silently
  plays a different game. Argument parsing, rendering and the console loop are not,
  because their failures are immediate and visible. That division is deliberate and
  the reasoning is written down in the `.csproj`.
