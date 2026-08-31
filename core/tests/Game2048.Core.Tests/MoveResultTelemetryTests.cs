using System;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="MoveResult.MaxTile"/> and
    /// <see cref="MoveResult.TilesMerged"/>, which an agent wrote and the harness
    /// passed green without exercising a line of.
    /// <para>
    /// That is not a hole in the guardrails, it is their shape. Agents cannot write
    /// to <c>core/tests/**</c>, so anything an agent adds is untested by
    /// construction: the build proves it compiles and the existing suite proves it
    /// broke nothing, and neither says the new code is right. Writing this file is
    /// the other half of the loop, and it is a human's half on purpose.
    /// </para>
    /// </summary>
    public class MoveResultTelemetryTests
    {
        // ------------------------------------------------------------- TilesMerged

        [Theory]
        [InlineData(new[] { 2, 2, 0, 0 }, 1)]
        [InlineData(new[] { 2, 2, 2, 2 }, 2)]   // two pairs, not four tiles
        [InlineData(new[] { 2, 4, 8, 16 }, 0)]
        [InlineData(new[] { 0, 0, 0, 0 }, 0)]
        public void TilesMerged_counts_pairs_collapsed_not_tiles_consumed(int[] row, int expected)
        {
            MoveResult result = Rules.Move(Row(row), Direction.Left);

            Assert.Equal(expected, result.TilesMerged);
        }

        [Fact]
        public void TilesMerged_always_agrees_with_the_merge_list()
        {
            // The count is derived from the list rather than tracked alongside it, so
            // the two cannot drift. This pins that, since a later "optimisation" to a
            // stored counter is exactly the change that would break it silently.
            var rng = new SeededRng(31);
            Board board = Rules.SpawnTile(Board.Empty(4, 4), rng);

            for (int step = 0; step < 300; step++)
            {
                MoveResult result = Rules.Move(board, (Direction)rng.Next(4));

                Assert.Equal(result.Merges.Count, result.TilesMerged);

                board = result.Board;

                if (board.TileCount < board.Rows * board.Columns)
                {
                    board = Rules.SpawnTile(board, rng);
                }
            }
        }

        // ----------------------------------------------------------------- MaxTile

        [Fact]
        public void MaxTile_reports_the_largest_tile_on_the_resulting_board()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 0, 4, 0 },
                { 0, 1024, 0, 8 },
                { 16, 0, 32, 0 },
                { 0, 64, 0, 128 },
            });

            MoveResult result = Rules.Move(board, Direction.Left);

            Assert.Equal(1024, result.MaxTile);
        }

        [Fact]
        public void MaxTile_reflects_the_board_after_the_move_not_before_it()
        {
            // The distinction that matters for a "new best tile" callout: merging two
            // 1024s must report 2048, not the 1024 that was there when the move began.
            MoveResult result = Rules.Move(Row(1024, 1024, 0, 0), Direction.Left);

            Assert.Equal(2048, result.MaxTile);
            Assert.Equal(1024, Row(1024, 1024, 0, 0).ToArray()[0, 0]);
        }

        [Fact]
        public void MaxTile_is_zero_on_an_empty_board()
        {
            MoveResult result = Rules.Move(Board.Empty(4, 4), Direction.Left);

            Assert.Equal(0, result.MaxTile);
        }

        [Fact]
        public void MaxTile_finds_the_largest_tile_in_the_last_cell()
        {
            // A scan that stops one short of the end is the ordinary way to get this
            // wrong, and it would be invisible on any board whose biggest tile is not
            // in the final position.
            Board board = Board.FromArray(new[,]
            {
                { 2, 4 },
                { 8, 4096 },
            });

            Assert.Equal(4096, Rules.Move(board, Direction.Up).MaxTile);
        }

        [Fact]
        public void MaxTile_works_on_a_non_square_board()
        {
            Board board = Board.FromArray(new[,] { { 2, 4, 8, 16, 8192 } });

            Assert.Equal(8192, Rules.Move(board, Direction.Left).MaxTile);
        }

        [Fact]
        public void MaxTile_never_disagrees_with_the_board_it_describes()
        {
            var rng = new SeededRng(77);
            Board board = Rules.SpawnTile(Board.Empty(4, 4), rng);

            for (int step = 0; step < 300; step++)
            {
                MoveResult result = Rules.Move(board, (Direction)rng.Next(4));

                Assert.Equal(LargestOn(result.Board), result.MaxTile);

                board = result.Board;

                if (board.TileCount < board.Rows * board.Columns)
                {
                    board = Rules.SpawnTile(board, rng);
                }
            }
        }

        // ------------------------------------------------------- the default value

        [Fact]
        public void A_default_MoveResult_reports_zero_rather_than_throwing()
        {
            // default(MoveResult) leaves Board null despite its non-nullable
            // declaration - a struct's zero value ignores that. Merges already
            // guarded against it; both new members have to as well, or reading
            // telemetry off an uninitialised result is a null reference.
            MoveResult uninitialised = default;

            Assert.Equal(0, uninitialised.MaxTile);
            Assert.Equal(0, uninitialised.TilesMerged);
        }

        // ----------------------------------------------------------------- helpers

        private static Board Row(params int[] values)
        {
            var grid = new int[1, values.Length];

            for (int c = 0; c < values.Length; c++)
            {
                grid[0, c] = values[c];
            }

            return Board.FromArray(grid);
        }

        private static int LargestOn(Board board)
        {
            int largest = 0;

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    largest = Math.Max(largest, board[r, c]);
                }
            }

            return largest;
        }
    }
}
