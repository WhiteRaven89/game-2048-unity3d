using System;
using System.Text;

namespace Game2048.Core
{
    /// <summary>
    /// An immutable 2048 grid, and the single source of truth for tile values.
    /// <para>
    /// The original implementation kept tile values on <c>NumberTile</c> components
    /// hanging off scene objects, and tracked occupancy separately in an
    /// <c>availableSlots</c> int array. Two structures describing one fact can
    /// disagree, and when they disagree there is no way to say which is right - so
    /// nothing about the board could be asserted. There is one array here and
    /// nothing else stores position or value.
    /// </para>
    /// </summary>
    public sealed class Board : IEquatable<Board>
    {
        // Row-major, length Rows*Columns. Never handed out; ToArray copies.
        private readonly int[] _cells;

        private Board(int rows, int columns, int[] cells)
        {
            Rows = rows;
            Columns = columns;
            _cells = cells;
        }

        public int Rows { get; }

        public int Columns { get; }

        /// <summary>
        /// Value at a cell: 0 for empty, otherwise a power of two of at least 2.
        /// </summary>
        public int this[int row, int column]
        {
            get
            {
                if (row < 0 || row >= Rows)
                {
                    throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be in [0," + Rows + ").");
                }

                if (column < 0 || column >= Columns)
                {
                    throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be in [0," + Columns + ").");
                }

                return _cells[(row * Columns) + column];
            }
        }

        public static Board Empty(int rows, int columns)
        {
            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "Rows must be positive.");
            }

            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "Columns must be positive.");
            }

            return new Board(rows, columns, new int[rows * columns]);
        }

        /// <summary>
        /// Builds a board from a literal grid - the form test fixtures are written in.
        /// <para>
        /// Every cell is validated here, so "each non-zero cell is a power of two" is
        /// true by construction rather than by convention. A fixture with a typo in it
        /// fails at the line that wrote it, not three asserts later.
        /// </para>
        /// </summary>
        public static Board FromArray(int[,] cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            int rows = cells.GetLength(0);
            int columns = cells.GetLength(1);

            if (rows <= 0 || columns <= 0)
            {
                throw new ArgumentException("Board must have at least one row and one column.", nameof(cells));
            }

            var flat = new int[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int value = cells[r, c];

                    if (value != 0 && !IsTileValue(value))
                    {
                        throw new ArgumentException(
                            "Cell (" + r + "," + c + ") is " + value + ". Cells must be 0 or a power of two of at least 2.",
                            nameof(cells));
                    }

                    flat[(r * columns) + c] = value;
                }
            }

            return new Board(rows, columns, flat);
        }

        public int[,] ToArray()
        {
            var result = new int[Rows, Columns];

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    result[r, c] = _cells[(r * Columns) + c];
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a copy with one cell replaced. Used by spawning; moves rebuild the
        /// whole grid instead.
        /// </summary>
        public Board With(int row, int column, int value)
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be in [0," + Rows + ").");
            }

            if (column < 0 || column >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be in [0," + Columns + ").");
            }

            if (value != 0 && !IsTileValue(value))
            {
                throw new ArgumentException(value + " is not a legal tile value.", nameof(value));
            }

            var copy = (int[])_cells.Clone();
            copy[(row * Columns) + column] = value;
            return new Board(Rows, Columns, copy);
        }

        /// <summary>Count of occupied cells.</summary>
        public int TileCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] != 0)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Sum of all tile values. Conserved by a move - merging two 2s into a 4 does
        /// not change it - so only spawning can move this number.
        /// </summary>
        public int Sum
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < _cells.Length; i++)
                {
                    sum += _cells[i];
                }

                return sum;
            }
        }

        /// <summary>True for a power of two of at least 2. False for 0.</summary>
        public static bool IsTileValue(int value) => value >= 2 && (value & (value - 1)) == 0;

        public bool Equals(Board? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Rows != other.Rows || Columns != other.Columns)
            {
                return false;
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != other._cells[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as Board);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Rows * 397) ^ Columns;

                for (int i = 0; i < _cells.Length; i++)
                {
                    hash = (hash * 397) ^ _cells[i];
                }

                return hash;
            }
        }

        public static bool operator ==(Board? left, Board? right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(Board? left, Board? right) => !(left == right);

        /// <summary>
        /// A grid rendering, for assertion messages. Core does not print anything -
        /// this returns a string and leaves the decision to write it to the caller.
        /// </summary>
        public override string ToString()
        {
            var text = new StringBuilder();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (c > 0)
                    {
                        text.Append(' ');
                    }

                    text.Append(_cells[(r * Columns) + c].ToString().PadLeft(5));
                }

                text.Append('\n');
            }

            return text.ToString();
        }
    }
}
