using System;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// Board is the single source of truth, so the things it refuses to represent
    /// matter as much as the things it stores. Every test below is a state the old
    /// implementation could reach and this one cannot.
    /// </summary>
    public class BoardTests
    {
        [Fact]
        public void Empty_board_has_the_requested_shape_and_no_tiles()
        {
            Board board = Board.Empty(4, 4);

            Assert.Equal(4, board.Rows);
            Assert.Equal(4, board.Columns);
            Assert.Equal(0, board.TileCount);
            Assert.Equal(0, board.Sum);
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(4, 0)]
        [InlineData(-1, 4)]
        public void Empty_rejects_a_degenerate_shape(int rows, int columns)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Board.Empty(rows, columns));
        }

        [Fact]
        public void FromArray_round_trips_through_ToArray()
        {
            var cells = new[,]
            {
                { 2, 0, 4, 0 },
                { 0, 8, 0, 16 },
                { 32, 0, 64, 0 },
                { 0, 128, 0, 256 },
            };

            int[,] result = Board.FromArray(cells).ToArray();

            Assert.Equal(cells, result);
        }

        [Theory]
        [InlineData(3)]     // not a power of two
        [InlineData(6)]
        [InlineData(1)]     // a power of two, but not a legal tile
        [InlineData(-2)]
        public void FromArray_rejects_a_cell_that_is_not_a_tile_value(int illegal)
        {
            var cells = new[,] { { 2, illegal } };

            Assert.Throws<ArgumentException>(() => Board.FromArray(cells));
        }

        [Fact]
        public void FromArray_rejects_null()
        {
            Assert.Throws<ArgumentNullException>(() => Board.FromArray(null!));
        }

        [Fact]
        public void ToArray_hands_back_a_copy_so_a_caller_cannot_reach_in()
        {
            Board board = Board.FromArray(new[,] { { 2, 0 }, { 0, 4 } });

            int[,] taken = board.ToArray();
            taken[0, 0] = 1024;

            Assert.Equal(2, board[0, 0]);
        }

        [Fact]
        public void FromArray_copies_its_input_so_a_later_edit_cannot_reach_in()
        {
            var cells = new[,] { { 2, 0 }, { 0, 4 } };
            Board board = Board.FromArray(cells);

            cells[0, 0] = 1024;

            Assert.Equal(2, board[0, 0]);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(2, 0)]
        [InlineData(0, 2)]
        public void Indexing_outside_the_grid_throws_rather_than_returning_a_neighbour(int row, int column)
        {
            Board board = Board.Empty(2, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => board[row, column]);
        }

        [Fact]
        public void With_returns_a_new_board_and_leaves_the_original_alone()
        {
            Board original = Board.Empty(2, 2);

            Board changed = original.With(1, 1, 4);

            Assert.Equal(0, original[1, 1]);
            Assert.Equal(4, changed[1, 1]);
            Assert.Equal(0, original.TileCount);
            Assert.Equal(1, changed.TileCount);
        }

        [Fact]
        public void With_rejects_an_illegal_tile_value()
        {
            Board board = Board.Empty(2, 2);

            Assert.Throws<ArgumentException>(() => board.With(0, 0, 3));
        }

        [Fact]
        public void Boards_with_the_same_cells_are_equal()
        {
            var cells = new[,] { { 2, 4 }, { 8, 16 } };

            Board a = Board.FromArray(cells);
            Board b = Board.FromArray(cells);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Boards_differing_in_one_cell_are_not_equal()
        {
            Board a = Board.FromArray(new[,] { { 2, 4 }, { 8, 16 } });
            Board b = Board.FromArray(new[,] { { 2, 4 }, { 8, 32 } });

            Assert.NotEqual(a, b);
            Assert.True(a != b);
        }

        [Fact]
        public void Boards_of_different_shapes_are_not_equal()
        {
            Board a = Board.FromArray(new[,] { { 2, 4 } });
            Board b = Board.FromArray(new[,] { { 2 }, { 4 } });

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Sum_and_TileCount_report_the_grid()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 0, 4 },
                { 0, 8, 0 },
            });

            Assert.Equal(14, board.Sum);
            Assert.Equal(3, board.TileCount);
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(4, true)]
        [InlineData(2048, true)]
        [InlineData(8192, true)]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(3, false)]
        [InlineData(-4, false)]
        public void IsTileValue_accepts_exactly_the_powers_of_two_from_two_up(int value, bool expected)
        {
            Assert.Equal(expected, Board.IsTileValue(value));
        }

        [Fact]
        public void A_board_holding_2048_and_beyond_is_representable()
        {
            // The original renders anything past 1024 as a "2" tile, because
            // SpawnTileIndex's switch stops there and its default returns index 0.
            // Nothing in Core has a table of prefabs to fall off the end of.
            Board board = Board.FromArray(new[,] { { 2048, 4096 }, { 8192, 16384 } });

            Assert.Equal(2048, board[0, 0]);
            Assert.Equal(16384, board[1, 1]);
            Assert.Equal(30720, board.Sum);
        }
    }
}
