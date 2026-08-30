# Findings

Three defects in `Assets/Src/Managers/LevelManager.cs`, found by writing the tests
the file never had. None of them was found by playing the game, and the reason why
is the point of this document.

The file was written in August 2022 and its last content change was **2022-08-29**.
The tests below were written **2026-08-29**. All three defects had been sitting
there for four years, in a game that works.

**None of them is fixed in `Assets/`.** That directory is frozen: it is the "before"
half of a comparison, and repairing it would delete the evidence. The corrected
logic lives in `core/src/Game2048.Core/`, and `core/src/Game2048.Legacy/` holds a
faithful port of the original two functions with their defects intact, so the same
test suite can be pointed at both.

---

## 1. The lose condition is unreachable

`LevelManager.IsMoveLeft()`, [line 696](../Assets/Src/Managers/LevelManager.cs#L696).

```csharp
NumberTile currentTile = GetNumberTileBasedOnCoord(x, y).GetComponent<NumberTile>();
NumberTile rightTile   = GetNumberTileBasedOnCoord(x , y + 1).GetComponent<NumberTile>();
NumberTile upTile      = GetNumberTileBasedOnCoord(x + 1 , y).GetComponent<NumberTile>();

if (x != rows - 1 && currentTile.TileValue == rightTile.TileValue)
{
    return true;
}
else if (y != columns - 1 && currentTile.TileValue == upTile.TileValue)
{
    return true;
}
```

Two defects, and the first hides the second.

**The neighbours are dereferenced before they are bounds-checked.**
`GetNumberTileBasedOnCoord` is a `List.Find` that returns `null` when nothing
matches. At the last column, `y + 1` is off the grid, the lookup returns `null`, and
`.GetComponent<NumberTile>()` throws. The guard that would have prevented it is on
the line below the throw.

**The guards are on the wrong axes.** `x != rows - 1` gates `rightTile`, which varies
in `y`. `y != columns - 1` gates `upTile`, which varies in `x`. Each guard protects
the neighbour it is not for. Repairing only the null dereference would leave a
function that skips real merges and reports the game over while moves remain.

### Why this is worse than "the game-over check crashes"

Follow the control flow on a full board. The scan runs `x` outer, `y` inner. On the
first row it either finds an equal pair and returns `true`, or it reaches
`y == columns - 1`, looks up a neighbour that is not there, and throws.

There is no third path. **`IsMoveLeft` cannot return `false`.**

The function whose entire purpose is to answer "has the player lost?" is incapable
of producing that answer. Not unreliable at it — incapable.

Measured over 500 randomly generated full boards
([`LegacyComparisonTests`](../core/tests/Game2048.Core.Tests/LegacyComparisonTests.cs)):

| Outcome | Boards |
|---|---:|
| Threw `NullReferenceException` | 127 |
| Returned `true` | 373 |
| **Returned `false`** | **0** |

Of the 127 crashes, **5** were on boards that genuinely had no move left — positions
where a correct implementation returns `true` for "game over" and the player should
see a result screen.

### Why it shipped anyway

```csharp
if (numberTiles.Count < rows * columns)
{
    return true;
}
```

That early return fires before a single lookup happens. While any cell is free — the
entire span of ordinary play — the function takes a path that cannot crash and
cannot be wrong. It is correct right up to the moment it matters.

**Caught by:** `A_completely_full_board_with_no_equal_neighbours_is_game_over_and_does_not_throw`,
plus seven more in [`RulesGameOverTests`](../core/tests/Game2048.Core.Tests/RulesGameOverTests.cs).
Reintroducing the swapped-axis guards into `Game2048.Core` fails 8 tests.

**How the extracted version avoids it:** `Rules.IsGameOver` bounds-checks before
reading, and the tests include a non-square board, because rows and columns being
confused cannot show up while they are equal.

---

## 2. The winning tile renders as a 2

`LevelManager.SpawnTileIndex()`, [line 663](../Assets/Src/Managers/LevelManager.cs#L663).

```csharp
switch (powerOf2Value)
{
    case 2: return 0;   //  tile of 2
    ...
    case 1024: return 9;   //  tile of 1204
    default:
        break;
}

return 0;
```

The switch stops at 1024. The default returns `0`, and index `0` is the "2" prefab.

Reach 2048 — the tile the game is named after, the win condition, the thing a player
spends twenty minutes working toward — and it draws as the smallest tile on the
board.

**Caught by:** `Legacy_renders_the_winning_tile_as_a_two`, which asserts
`SpawnTileIndex(2048) == SpawnTileIndex(2)`.

**How the extracted version avoids it:** it has no table to fall off the end of. A
tile is an `int`. `Rules.Move` turns two 2048s into a 4096 by the same line of code
that turns two 2s into a 4, and the mapping from value to sprite is a presentation
concern that never enters the rules.

---

## 3. A move that changes nothing still spawns a tile

`LevelManager.ProcessTileShiftAlgorithm()`, [line 202](../Assets/Src/Managers/LevelManager.cs#L202).

```csharp
if (inputType == InputType.Up) MoveTilesUp();
else if (inputType == InputType.Down) MoveTilesDown();
else if (inputType == InputType.Left) MoveTilesLeft();
else if (inputType == InputType.Right) MoveTilesRight();

if (IsMoveLeft())
{
    CheckTilesForMerging();
    CreatetileAtRandomPosition();   // ← unconditional
    ...
```

Nothing asks whether the move changed anything. The four handlers return `void` and
track no state, so there is nothing to ask. Line 214 is the only in-play spawn site
and it is inside that branch.

Hold a direction against a wall and every press hands the player a free tile. The
board fills with tiles they did not earn, and then they lose. It is a difficulty bug
that punishes a no-op input, and it is the most *visible* of the three during
ordinary play — which makes it the most interesting one to have survived.

Worth noting alongside it: `CheckTilesForMerging()`
([line 684](../Assets/Src/Managers/LevelManager.cs#L684)) checks nothing. It sets
`IsMerged = false` on every tile. The name describes a query; the body is a reset.

**Caught by:** `A_move_that_changes_nothing_is_refused_and_costs_the_player_nothing`
and three others. Removing the guard from `Game.TryMove` fails 4 tests.

**How the extracted version avoids it:** `MoveResult.Moved` exists for exactly this.
`Move` returns whether anything changed, and `Game.TryMove` returns early without
spawning when it did not. The flag is computed by comparing the finished boards
rather than by tracking a flag as cells are written — a few microseconds slower, and
immune to being missed on one path.

---

## What the three have in common

All three live at a boundary that manual play reaches rarely or never: the lose
condition, the win condition, and an input that does nothing.

That is not a coincidence, it is a property of how the code was verified. Playing
the game exercises the middle of the state space thoroughly and its edges almost
never. Four years of playing found none of these. A day of writing tests found all
three, because a test can start the board at a boundary instead of having to reach
it.

This is the difference between a tested system and a testable one. The original
cannot be tested at any of these boundaries at all — `IsMoveLeft` needs a populated
scene, `GameObject`s, `NumberTile` components and a `LevelManager` in a running
Unity player before it can be asked a single question. Making the rules a function
of a board is what made the boundaries reachable, and reaching them is what found
the bugs.

---

## On trusting this test suite

A suite that passes on its first run has demonstrated nothing. Each defect was
reintroduced into `Game2048.Core` and the suite re-run:

| Defect reintroduced | Tests that failed |
|---|---:|
| Merged tile allowed to merge again | 13 |
| `LineCount`/`LineLength` swapped | 26 |
| `IsGameOver` with the original's swapped-axis guards | 8 |
| Merge does not double the value | 15 |
| `Down` mapped as `Up` | 7 |
| `TryMove` spawning regardless of `Moved` | 4 |
| Merges performed but never reported | 8 |
| Gap between equal tiles blocks the merge | **1** |

The last row was the useful one. The gap-merge rule was caught by a single test, and
the reason is structural: **an invariant cannot catch an under-merge.** A move that
wrongly declines to merge still conserves the board's total, still adds no tiles,
and still leaves every cell a power of two. Every property assertion stays satisfied.

Property tests are strong against "did something impossible" and blind to "quietly
did less". Five stated examples were added and the catch rate went from 1 to 5. The
comment above them in
[`RulesMoveTests`](../core/tests/Game2048.Core.Tests/RulesMoveTests.cs) records why
they exist, so nobody deletes them as redundant.
