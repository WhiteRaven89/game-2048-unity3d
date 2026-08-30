using System;
using System.Collections.Generic;

namespace Game2048.Core
{
    /// <summary>
    /// The rules of 2048, as functions of a board.
    /// <para>
    /// Nothing here reads the clock, touches a scene, or spawns a tile. A move takes
    /// a board and returns a board, which is what makes every rule in this file
    /// assertable in a unit test that runs in microseconds.
    /// </para>
    /// </summary>
    public static class Rules
    {
        /// <summary>
        /// Applies one move. The board passed in is not modified; the result carries
        /// a new one.
        /// </summary>
        public static MoveResult Move(Board board, Direction direction)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var traversal = new Traversal(board, direction);

            var next = new int[board.Rows, board.Columns];
            var merges = new List<Merge>();
            int scoreDelta = 0;

            for (int line = 0; line < traversal.LineCount; line++)
            {
                int write = 0;
                int read = 0;

                while (read < traversal.LineLength)
                {
                    int value = Read(line, read);

                    if (value == 0)
                    {
                        read++;
                        continue;
                    }

                    // The nearest occupied cell further from the wall. Gaps between
                    // the two do not block a merge - [2,0,0,2] collapses to [4,0,0,0].
                    int partner = read + 1;

                    while (partner < traversal.LineLength && Read(line, partner) == 0)
                    {
                        partner++;
                    }

                    traversal.Map(line, write, out int writeRow, out int writeColumn);

                    if (partner < traversal.LineLength && Read(line, partner) == value)
                    {
                        int merged = value * 2;
                        next[writeRow, writeColumn] = merged;
                        merges.Add(new Merge(writeRow, writeColumn, merged));
                        scoreDelta += merged;

                        // Consume both tiles. Advancing past the partner is what caps a
                        // tile at one merge per move: [2,2,2,2] gives [4,4,0,0], never
                        // [8,0,0,0]. Because the scan runs from the wall outward, the
                        // pair nearest the wall merges first - merges resolve in the
                        // direction of travel.
                        read = partner + 1;
                    }
                    else
                    {
                        next[writeRow, writeColumn] = value;
                        read = partner;
                    }

                    write++;
                }
            }

            // Comparing the finished boards, rather than tracking a flag as cells are
            // written, is a few microseconds slower and immune to an entire class of
            // bug: a flag missed on one path reports "nothing moved" on a move that
            // did move, and the caller then declines to spawn. The cheap version is
            // not worth having to prove.
            Board result = Board.FromArray(next);
            bool moved = !result.Equals(board);

            return new MoveResult(result, scoreDelta, moved, merges.ToArray());

            int Read(int line, int position)
            {
                traversal.Map(line, position, out int row, out int column);
                return board[row, column];
            }
        }

        /// <summary>
        /// Places one new tile in a randomly chosen empty cell: a 2 nine times out of
        /// ten, otherwise a 4.
        /// <para>
        /// Exactly two values are drawn from <paramref name="rng"/> on every call - the
        /// cell, then the value - no matter how full the board is. That fixed cost is
        /// deliberate: a replay is a seed plus a list of moves, and it only reproduces
        /// if the number of draws per turn cannot vary with board state.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">The board is full.</exception>
        public static Board SpawnTile(Board board, IRng rng)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            int emptyCount = (board.Rows * board.Columns) - board.TileCount;

            if (emptyCount == 0)
            {
                // The original scans forward from a random cell until it finds a gap,
                // which never terminates on a full board. Refusing outright is louder
                // than hanging, and the caller has to decide what a full board means.
                throw new InvalidOperationException("Cannot spawn a tile: the board is full.");
            }

            int chosen = rng.Next(emptyCount);
            int value = rng.Next(10) == 0 ? 4 : 2;

            int seen = 0;

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    if (board[r, c] != 0)
                    {
                        continue;
                    }

                    if (seen == chosen)
                    {
                        return board.With(r, c, value);
                    }

                    seen++;
                }
            }

            throw new InvalidOperationException("Unreachable: the empty-cell count disagreed with the board.");
        }

        /// <summary>
        /// True when no move would change anything: the board is full and no two
        /// orthogonal neighbours are equal.
        /// <para>
        /// Every neighbour is bounds-checked before it is read. The original does the
        /// reverse - it fetches the right and up neighbours and dereferences them
        /// before testing the index, and it guards the wrong axis for each - so it
        /// throws a null reference exactly when the board fills, which is the only
        /// situation the function exists to answer.
        /// </para>
        /// </summary>
        public static bool IsGameOver(Board board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    int value = board[r, c];

                    if (value == 0)
                    {
                        return false;
                    }

                    if (c + 1 < board.Columns && board[r, c + 1] == value)
                    {
                        return false;
                    }

                    if (r + 1 < board.Rows && board[r + 1, c] == value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Turns a direction into an order to walk the grid in.
        /// <para>
        /// This is the one abstraction in Core that earns its place. The original had
        /// four direction handlers - <c>MoveTilesLeft</c>, <c>Right</c>, <c>Up</c>,
        /// <c>Down</c> - each three near-identical triple loops, about 140 lines that
        /// differ only in which index counts up and which counts down. They were one
        /// function written four times, which means a rule fix had to be applied four
        /// times and could be forgotten three.
        /// </para>
        /// <para>
        /// Mapping coordinates rather than transposing the board keeps merge positions
        /// in real board space, so nothing has to be un-mapped on the way out.
        /// </para>
        /// </summary>
        private readonly struct Traversal
        {
            private readonly Direction _direction;
            private readonly int _rows;
            private readonly int _columns;

            public Traversal(Board board, Direction direction)
            {
                if (direction < Direction.Left || direction > Direction.Down)
                {
                    // An out-of-range cast reaching the rules is a caller bug. Saying so
                    // beats quietly picking Left and returning a board that looks fine.
                    throw new ArgumentOutOfRangeException(
                        nameof(direction), direction, "Not one of the four directions.");
                }

                _direction = direction;
                _rows = board.Rows;
                _columns = board.Columns;
            }

            private bool IsHorizontal => _direction == Direction.Left || _direction == Direction.Right;

            /// <summary>Rows for a horizontal move, columns for a vertical one.</summary>
            public int LineCount => IsHorizontal ? _rows : _columns;

            /// <summary>Cells in one line, counted from the wall tiles slide into.</summary>
            public int LineLength => IsHorizontal ? _columns : _rows;

            /// <summary>
            /// Position 0 is against the wall tiles are moving toward, so a collapse
            /// always walks positions upward regardless of direction.
            /// </summary>
            public void Map(int line, int position, out int row, out int column)
            {
                switch (_direction)
                {
                    case Direction.Left:
                        row = line;
                        column = position;
                        break;

                    case Direction.Right:
                        row = line;
                        column = _columns - 1 - position;
                        break;

                    case Direction.Up:
                        row = position;
                        column = line;
                        break;

                    case Direction.Down:
                        row = _rows - 1 - position;
                        column = line;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(position), _direction, "Unreachable: validated in the constructor.");
                }
            }
        }
    }
}
