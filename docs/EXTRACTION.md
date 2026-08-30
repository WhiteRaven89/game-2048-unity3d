# Extraction

What moved out of `LevelManager.cs`, and why each change was made. Every "why" here
is a consequence, not a principle: the point is not that the original violated a
rule, it is that specific things could not be done because of how it was built.

`Assets/` is unchanged. The comparison is between
[`Assets/Src/Managers/LevelManager.cs`](../Assets/Src/Managers/LevelManager.cs) as it
has stood since 2022 and [`core/src/Game2048.Core/`](../core/src/Game2048.Core/).

| | Before | After |
|---|---:|---:|
| Rules live in | one 723-line `MonoBehaviour` | 803 lines across 8 files |
| Four direction handlers | 136 lines | one 79-line `Move` |
| One tile move | 163 lines (`MoveTiles`) | part of the same 79 |
| `GetComponent` calls | 38 | 0 |
| Tests | 0 | 167, running in 87 ms |
| Runs without Unity | no | yes |

The after is not much smaller, and that is worth saying plainly. Roughly a third of
it is XML documentation and comments explaining decisions. The gain is not fewer
lines; it is that the lines can be executed by a test.

---

## 1. Two sources of truth became one

**Before.** Tile values lived on `NumberTile` components attached to scene objects,
in a `List<GameObject>`. Occupancy lived separately in an `int[,]`:

```csharp
int[,] availableSlots = null;
private List<GameObject> numberTiles;
```

Every move had to update both, by hand, in the right order:

```csharp
ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
FlagSlotAsOccupied(xTargetCoord, targetIndex + 1);
```

**The consequence.** Two structures describing one fact can disagree. When they
disagree there is no way to say which is right, so there is no assertion anyone can
write about the board. `availableSlots` says a cell is occupied; `numberTiles` has
nothing there; which is the bug? The question has no answer, so the state cannot be
checked — not by a test, and not by a person reading a log.

**After.** One immutable array, and nothing else stores a position or a value.

```csharp
public sealed class Board : IEquatable<Board>
{
    private readonly int[] _cells;   // row-major, the only copy
}
```

Occupancy is `_cells[i] != 0`. There is no second structure to fall out of step with,
so `Assert.Equal(expectedBoard, actualBoard)` means something.

---

## 2. Four direction handlers became one

**Before.** `MoveTilesLeft`, `MoveTilesRight`, `MoveTilesUp`, `MoveTilesDown`
([lines 284–419](../Assets/Src/Managers/LevelManager.cs#L284-L419)) — 136 lines. Each
is the same triple loop three times over: move, merge, move again. Here is Left,
and Right differs only in which way the inner index counts:

```csharp
private void MoveTilesLeft()
{
    for (int x = 0; x < rows; x++)
        for (int y = 1; y < columns; y++)
        {
            if (availableSlots[x, y] == 0) continue;
            MoveTiles(x, y, x, y - 1);
        }
    // ... the same loop again for UpgradeTiles
    // ... and a third time for MoveTiles
}

private void MoveTilesRight()
{
    for (int x = 0; x < rows; x++)
        for (int y = columns - 2; y >= 0; y--)
        {
            if (availableSlots[x, y] == 0) continue;
            MoveTiles(x, y, x, y + 1);
        }
    // ... twice more
}
```

**The consequence.** This is one function written four times. A rule change has to be
made in four places and can be forgotten in three, and a bug fixed in Left stays
alive in Up. Nothing enforces that the four agree — there is no shared code path for
a test to cover, so testing Left tells you nothing about Down.

**After.** One implementation, and a small struct that says which way to walk the
grid:

```csharp
public void Map(int line, int position, out int row, out int column)
{
    switch (_direction)
    {
        case Direction.Left:  row = line;                   column = position;              break;
        case Direction.Right: row = line;                   column = _columns - 1 - position; break;
        case Direction.Up:    row = position;               column = line;                  break;
        case Direction.Down:  row = _rows - 1 - position;   column = line;                  break;
    }
}
```

Position 0 is always against the wall the tiles are sliding toward, so the collapse
walks positions upward and never knows which direction it is serving.

**Why coordinate mapping and not transpose.** Transposing the board, collapsing left,
and transposing back is easier to read. It also means every reported merge position
is in transposed space and has to be mapped back on the way out — one more step to
get wrong, and the kind of wrong that puts an animation at the wrong end of the
board. Mapping coordinates keeps merge positions in board space throughout. There is
a test for exactly that (`Merge_coordinates_are_reported_in_board_space_for_a_vertical_move`).

**Why this abstraction and not others.** It is the only one in `Core` that is not a
plain type. There is no `IBoardFactory`, no strategy class per direction, no command
hierarchy. This one earns its place because it deletes 136 lines of duplication and
has a test that fails when it is wrong; an interface with one implementation and no
test seam would not.

---

## 3. Lookups by search became lookups by index

**Before.** Finding the tile at a coordinate was a linear search with a
`GetComponent` inside the predicate:

```csharp
numberTile = numberTiles.Find(t => t.GetComponent<NumberTile>().XCoord == x
                                && t.GetComponent<NumberTile>().YCoord == y);
```

Four such lookups exist ([lines 638, 647, 654, 659](../Assets/Src/Managers/LevelManager.cs#L638-L659)),
and they are called from inside the nested loops above.

**The consequence.** Two, and the second matters more. The obvious one is cost —
`GetComponent` per candidate per cell per pass, three passes per direction. The one
that actually caused a bug is that **`Find` returns `null` off the end of the grid**,
and the caller reads that as "no tile here" rather than "you asked about a cell that
does not exist". That is the mechanism behind finding 1: an out-of-range coordinate
and an empty cell are indistinguishable, so nothing forces a bounds check.

**After.** An indexer that refuses the question rather than answering it wrongly:

```csharp
public int this[int row, int column]
{
    get
    {
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row), row, ...);
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column), column, ...);

        return _cells[(row * Columns) + column];
    }
}
```

Off the grid throws; empty returns 0. They are different answers because they are
different questions.

---

## 4. Mutation inside an argument became a value

**Before**, in `UpgradeTiles` ([line 595](../Assets/Src/Managers/LevelManager.cs#L595)):

```csharp
GameObject upgradedTile = GetNumberTile(
    lstNumbersTilePrefabs[SpawnTileIndex(currentTile.GetComponent<NumberTile>().TileValue *= 2)],
    0, 0,
    currentTile.GetComponent<NumberTile>().TileValue);
```

`TileValue *= 2` runs inside an argument expression, and the fourth argument reads
the same field again — relying on the doubling having already happened, in an
evaluation order the reader has to work out.

**The consequence.** The line has a side effect that is invisible unless you read it
character by character, and its correctness depends on argument evaluation order. It
is also where finding 2 enters, via `SpawnTileIndex`.

**After.** The doubled value is a local, and the merge is a value, not an edit:

```csharp
int merged = value * 2;
next[writeRow, writeColumn] = merged;
merges.Add(new Merge(writeRow, writeColumn, merged));
scoreDelta += merged;
```

---

## 5. Spawning moved out of the rules

**Before.** `CreatetileAtRandomPosition()` calls `UnityEngine.Random` directly, picks
a random start, and scans forward until it finds a gap:

```csharp
float highOrLowChance = Random.Range(0f, 0.99f);
...
int x = Random.Range(0, rows);
int y = Random.Range(0, columns);

while (!found) { ... }
```

**The consequence.** Three.

- The rules cannot be replayed. Randomness is reached for from inside them, from a
  global the caller cannot supply.
- The number of random draws varies with how full the board is, so even capturing
  the seed would not reproduce a game.
- On a full board the `while (!found)` loop has no exit.

**After.** Spawning is a separate function taking an injected generator, drawing
exactly two values every time regardless of the board:

```csharp
int chosen = rng.Next(emptyCount);       // which empty cell
int value  = rng.Next(10) == 0 ? 4 : 2;  // which value
```

That fixed draw count is what makes a replay a seed plus a list of moves and nothing
else — there is a test asserting the count is 2 on an almost-empty board and on an
almost-full one. A full board throws instead of hanging.

**Why the RNG is hand-written.** `SeededRng` is twenty lines of xorshift32 rather
than a wrapper over `System.Random`, because `System.Random`'s algorithm is
explicitly not part of its contract and changed in .NET 6. A replay recorded under
Unity's Mono would not reproduce under .NET 8, and "same seed, same game" would hold
by luck rather than by design.

---

## 6. `Move` stopped deciding whether to spawn

**Before.** The handlers return `void`. Nothing knows whether a move did anything,
which is finding 3.

**After.** `MoveResult.Moved`, computed by comparing the finished boards:

```csharp
Board result = Board.FromArray(next);
bool moved = !result.Equals(board);
```

The first version of this tracked a flag while writing cells. It was replaced
because a flag missed on one path reports "nothing moved" for a move that did move,
and the caller then declines to spawn — a bug that would be invisible for a long
time. Comparing boards is a few microseconds slower and needs no proof.

---

## What was deliberately left alone

Not everything in the original is wrong, and a rewrite that replaces working code
because it is old is a different kind of mistake. These were read and kept:

- **`GenericFSM` / `State`** — a clean generic state machine, properly namespaced.
- **`SimplePool`** — a straightforward object pool that does its job.
- **`CIBuildCreator`** — a working command-line build entry point.
- **Level data as JSON with a `ScriptableObject` fallback and a server-override
  hook** — this is liveops-shaped already. Grid dimensions come from data, not code.

The one lifecycle inconsistency worth naming and *not* fixing here: tiles are pooled
on spawn but `Destroy`-ed on merge. That is a real defect, it belongs in the view
layer, and the view layer is out of scope for an extraction of the rules.

---

## What is not done

- **Unity is not wired to the new core.** It was the riskiest integration and it is
  not needed for the comparison: Unity is the "before", Core plus tests plus CLI are
  the "after". Doing it would mean a view layer that renders a `Board` and animates
  from `MoveResult.Merges` — a day of work with nothing new to prove.
- **`Game2048.Legacy` ports two functions, not the whole file.** `IsMoveLeft` and
  `SpawnTileIndex` are the two with defects worth demonstrating. Porting
  `MoveTiles`'s 163 lines faithfully would take a day and demonstrate the same point.
