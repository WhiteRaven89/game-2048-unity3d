using System;
using System.Linq;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// <see cref="Rules.Move"/>: the collapse, the merge rule, the score, and the
    /// coordinate mapping that lets one implementation serve four directions.
    /// </summary>
    public class RulesMoveTests
    {
        // ---------------------------------------------------------------- collapse

        [Theory]
        // Gaps close.
        [InlineData(new[] { 0, 0, 0, 2 }, new[] { 2, 0, 0, 0 })]
        [InlineData(new[] { 0, 2, 0, 4 }, new[] { 2, 4, 0, 0 })]
        // A gap between two equal tiles does not stop them merging.
        [InlineData(new[] { 2, 0, 0, 2 }, new[] { 4, 0, 0, 0 })]
        // Already packed and unmergeable: unchanged.
        [InlineData(new[] { 2, 4, 8, 16 }, new[] { 2, 4, 8, 16 })]
        // Empty stays empty.
        [InlineData(new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 })]
        public void Move_left_collapses_a_row(int[] before, int[] expected)
        {
            MoveResult result = Rules.Move(Row(before), Direction.Left);

            Assert.Equal(expected, RowOf(result.Board));
        }

        // ------------------------------------------------------------- merge rules

        [Theory]
        // A tile merges at most once per move. Not [8,0,0,0].
        [InlineData(new[] { 2, 2, 4, 0 }, new[] { 4, 4, 0, 0 })]
        // Two independent pairs, not a cascade. Not [8,0,0,0].
        [InlineData(new[] { 2, 2, 2, 2 }, new[] { 4, 4, 0, 0 })]
        // Merges resolve in the direction of travel: the pair nearest the wall goes
        // first, so the leading pair merges and the third tile follows.
        [InlineData(new[] { 2, 2, 2, 0 }, new[] { 4, 2, 0, 0 })]
        [InlineData(new[] { 0, 2, 2, 2 }, new[] { 4, 2, 0, 0 })]
        // The product of a merge cannot merge again in the same move.
        [InlineData(new[] { 4, 4, 8, 0 }, new[] { 8, 8, 0, 0 })]
        [InlineData(new[] { 2, 2, 4, 4 }, new[] { 4, 8, 0, 0 })]
        // Merging across a gap. These are here in number because the property tests
        // cannot cover them: a move that wrongly declines to merge still conserves
        // the sum and still adds no tiles, so every invariant stays satisfied. Only
        // a stated example catches an under-merge.
        [InlineData(new[] { 2, 0, 2, 0 }, new[] { 4, 0, 0, 0 })]
        [InlineData(new[] { 0, 2, 0, 2 }, new[] { 4, 0, 0, 0 })]
        [InlineData(new[] { 2, 0, 2, 2 }, new[] { 4, 2, 0, 0 })]
        [InlineData(new[] { 4, 0, 4, 8 }, new[] { 8, 8, 0, 0 })]
        [InlineData(new[] { 0, 0, 4, 4 }, new[] { 8, 0, 0, 0 })]
        public void A_tile_merges_at_most_once_per_move(int[] before, int[] expected)
        {
            MoveResult result = Rules.Move(Row(before), Direction.Left);

            Assert.Equal(expected, RowOf(result.Board));
        }

        [Fact]
        public void Merges_are_reported_with_the_cell_they_landed_in_and_the_value_produced()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 2, 0, 0 },
                { 0, 0, 0, 0 },
                { 4, 4, 8, 8 },
                { 0, 0, 0, 0 },
            });

            MoveResult result = Rules.Move(board, Direction.Left);

            Assert.Collection(
                result.Merges,
                m => AssertMerge(m, 0, 0, 4),
                m => AssertMerge(m, 2, 0, 8),
                m => AssertMerge(m, 2, 1, 16));
        }

        // -------------------------------------------------------------------- score

        [Theory]
        [InlineData(new[] { 2, 2, 0, 0 }, 4)]
        [InlineData(new[] { 2, 2, 2, 2 }, 8)]      // 4 + 4
        [InlineData(new[] { 4, 4, 8, 8 }, 24)]     // 8 + 16
        [InlineData(new[] { 2, 4, 8, 16 }, 0)]     // nothing merged
        public void Score_delta_is_the_sum_of_the_tiles_the_merges_created(int[] before, int expected)
        {
            MoveResult result = Rules.Move(Row(before), Direction.Left);

            Assert.Equal(expected, result.ScoreDelta);
            Assert.Equal(expected, result.Merges.Sum(m => m.Value));
        }

        // --------------------------------------------------------------- moved flag

        [Fact]
        public void Moved_is_false_when_nothing_can_shift_and_the_board_is_untouched()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 4, 8, 16 },
                { 4, 8, 16, 32 },
                { 8, 16, 32, 64 },
                { 16, 32, 64, 128 },
            });

            MoveResult result = Rules.Move(board, Direction.Left);

            Assert.False(result.Moved);
            Assert.Equal(board, result.Board);
            Assert.Empty(result.Merges);
            Assert.Equal(0, result.ScoreDelta);
        }

        [Fact]
        public void Moved_is_true_when_a_single_tile_shifts()
        {
            MoveResult result = Rules.Move(Row(0, 0, 0, 2), Direction.Left);

            Assert.True(result.Moved);
        }

        [Fact]
        public void Moved_is_true_when_tiles_merge_without_any_gap_to_close()
        {
            MoveResult result = Rules.Move(Row(2, 2, 4, 8), Direction.Left);

            Assert.True(result.Moved);
        }

        // ------------------------------------------------------------------ purity

        [Fact]
        public void Move_does_not_modify_the_board_it_was_given()
        {
            var cells = new[,]
            {
                { 2, 2, 0, 4 },
                { 0, 8, 8, 0 },
                { 0, 0, 0, 0 },
                { 16, 0, 16, 2 },
            };

            Board board = Board.FromArray(cells);
            Board snapshot = Board.FromArray(cells);

            Rules.Move(board, Direction.Left);

            Assert.Equal(snapshot, board);
        }

        [Fact]
        public void Move_rejects_a_null_board()
        {
            Assert.Throws<ArgumentNullException>(() => Rules.Move(null!, Direction.Left));
        }

        [Fact]
        public void A_direction_outside_the_enum_throws_rather_than_defaulting_to_left()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rules.Move(Board.Empty(4, 4), (Direction)99));
        }

        // -------------------------------------------------------------- invariants
        //
        // Swept over every board reachable by a run of random moves, rather than
        // asserted on a handful of fixtures. These are the properties that have to
        // hold for any board at all, which is exactly the kind of claim example-based
        // tests are worst at.

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(12345)]
        [InlineData(0)]
        public void Move_holds_its_invariants_across_a_long_random_walk(int seed)
        {
            var rng = new SeededRng(seed);
            Board board = Board.Empty(4, 4);

            for (int step = 0; step < 500; step++)
            {
                if (board.TileCount < board.Rows * board.Columns)
                {
                    board = Rules.SpawnTile(board, rng);
                }

                var direction = (Direction)rng.Next(4);
                MoveResult result = Rules.Move(board, direction);

                AssertEveryCellIsATileValue(result.Board);
                Assert.True(
                    result.Board.TileCount <= board.TileCount,
                    "Move created a tile. Only spawning may do that.\n" + board + "->\n" + result.Board);
                Assert.True(
                    result.Board.Sum == board.Sum,
                    "Move changed the total value on the board.\n" + board + "->\n" + result.Board);
                Assert.Equal(result.Merges.Sum(m => m.Value), result.ScoreDelta);
                Assert.Equal(board.TileCount - result.Merges.Count, result.Board.TileCount);

                if (!result.Moved)
                {
                    Assert.Equal(board, result.Board);
                }

                board = result.Board;
            }
        }

        // -------------------------------------------------------------- directions
        //
        // One collapse serves all four directions, so the risk is not in the merge
        // rule - that is already pinned above - but in the coordinate mapping. Two
        // kinds of test, because they fail differently. The stated grids catch a
        // transform that is wrong in a self-consistent way; the symmetry properties
        // catch the boards nobody thought to write down.

        /// <summary>
        /// Deliberately asymmetric under every reflection, so a transposed or
        /// reversed mapping cannot pass by coincidence.
        /// </summary>
        private static Board Fixture() => Board.FromArray(new[,]
        {
            { 2, 2, 0, 4 },
            { 0, 0, 8, 0 },
            { 16, 0, 0, 16 },
            { 4, 0, 2, 0 },
        });

        [Fact]
        public void Move_left_on_the_fixture()
        {
            MoveResult result = Rules.Move(Fixture(), Direction.Left);

            AssertGrid(result.Board, new[,]
            {
                { 4, 4, 0, 0 },
                { 8, 0, 0, 0 },
                { 32, 0, 0, 0 },
                { 4, 2, 0, 0 },
            });
            Assert.Equal(36, result.ScoreDelta);   // 4 + 32
        }

        [Fact]
        public void Move_right_on_the_fixture()
        {
            MoveResult result = Rules.Move(Fixture(), Direction.Right);

            AssertGrid(result.Board, new[,]
            {
                { 0, 0, 4, 4 },
                { 0, 0, 0, 8 },
                { 0, 0, 0, 32 },
                { 0, 0, 4, 2 },
            });
            Assert.Equal(36, result.ScoreDelta);
        }

        [Fact]
        public void Move_up_on_the_fixture()
        {
            MoveResult result = Rules.Move(Fixture(), Direction.Up);

            AssertGrid(result.Board, new[,]
            {
                { 2, 2, 8, 4 },
                { 16, 0, 2, 16 },
                { 4, 0, 0, 0 },
                { 0, 0, 0, 0 },
            });
            Assert.Equal(0, result.ScoreDelta);   // no column has an adjacent pair
        }

        [Fact]
        public void Move_down_on_the_fixture()
        {
            MoveResult result = Rules.Move(Fixture(), Direction.Down);

            AssertGrid(result.Board, new[,]
            {
                { 0, 0, 0, 0 },
                { 2, 0, 0, 0 },
                { 16, 0, 8, 4 },
                { 4, 2, 2, 16 },
            });
            Assert.Equal(0, result.ScoreDelta);
        }

        [Fact]
        public void Merge_coordinates_are_reported_in_board_space_for_a_vertical_move()
        {
            // The collapse walks from the wall outward, so for Down the wall is the
            // last row. If the mapping leaked into the reported Merge, this would come
            // back as row 0 and an animation would play at the wrong end of the board.
            Board board = Board.FromArray(new[,]
            {
                { 2, 0 },
                { 2, 0 },
            });

            MoveResult result = Rules.Move(board, Direction.Down);

            Merge only = Assert.Single(result.Merges);
            AssertMerge(only, 1, 0, 4);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(19)]
        [InlineData(77)]
        public void Moving_right_equals_mirroring_horizontally_and_moving_left(int seed)
        {
            Board board = RandomBoard(seed);

            MoveResult right = Rules.Move(board, Direction.Right);
            MoveResult mirrored = Rules.Move(MirrorHorizontally(board), Direction.Left);

            Assert.Equal(right.Board, MirrorHorizontally(mirrored.Board));
            Assert.Equal(right.ScoreDelta, mirrored.ScoreDelta);
            Assert.Equal(right.Moved, mirrored.Moved);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(19)]
        [InlineData(77)]
        public void Moving_up_equals_transposing_and_moving_left(int seed)
        {
            Board board = RandomBoard(seed);

            MoveResult up = Rules.Move(board, Direction.Up);
            MoveResult transposed = Rules.Move(Transpose(board), Direction.Left);

            Assert.Equal(up.Board, Transpose(transposed.Board));
            Assert.Equal(up.ScoreDelta, transposed.ScoreDelta);
            Assert.Equal(up.Moved, transposed.Moved);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(19)]
        [InlineData(77)]
        public void Moving_down_equals_transposing_mirroring_and_moving_left(int seed)
        {
            Board board = RandomBoard(seed);

            MoveResult down = Rules.Move(board, Direction.Down);
            MoveResult folded = Rules.Move(MirrorHorizontally(Transpose(board)), Direction.Left);

            Assert.Equal(down.Board, Transpose(MirrorHorizontally(folded.Board)));
            Assert.Equal(down.ScoreDelta, folded.ScoreDelta);
            Assert.Equal(down.Moved, folded.Moved);
        }

        [Theory]
        [InlineData(Direction.Left, 31)]
        [InlineData(Direction.Right, 31)]
        [InlineData(Direction.Up, 31)]
        [InlineData(Direction.Down, 31)]
        [InlineData(Direction.Left, 404)]
        [InlineData(Direction.Right, 404)]
        [InlineData(Direction.Up, 404)]
        [InlineData(Direction.Down, 404)]
        public void After_a_move_every_line_is_packed_against_the_wall(Direction direction, int seed)
        {
            // No gap may survive a move. Note what this does NOT claim: that a second
            // move in the same direction is a no-op. That is false in 2048 and worth
            // being explicit about - [2,2,2,2] gives [4,4,0,0], and moving left again
            // legitimately gives [8,0,0,0]. Packing is the invariant; idempotence is not.
            Board moved = Rules.Move(RandomBoard(seed), direction).Board;
            Board asLeft = ReorientToLeft(moved, direction);

            for (int r = 0; r < asLeft.Rows; r++)
            {
                bool seenGap = false;

                for (int c = 0; c < asLeft.Columns; c++)
                {
                    if (asLeft[r, c] == 0)
                    {
                        seenGap = true;
                    }
                    else
                    {
                        Assert.False(seenGap, "A tile sits beyond a gap after moving " + direction + ":\n" + moved);
                    }
                }
            }
        }

        [Theory]
        [InlineData(Direction.Left)]
        [InlineData(Direction.Right)]
        [InlineData(Direction.Up)]
        [InlineData(Direction.Down)]
        public void Every_direction_works_on_a_non_square_board(Direction direction)
        {
            // Rows and columns are swapped in the original's IsMoveLeft bounds checks,
            // which only shows up when they differ. Here they differ.
            Board board = Board.FromArray(new[,]
            {
                { 2, 2, 0, 4, 0 },
                { 0, 8, 8, 0, 2 },
            });

            MoveResult result = Rules.Move(board, direction);

            Assert.Equal(board.Sum, result.Board.Sum);
            AssertEveryCellIsATileValue(result.Board);
            Assert.Equal(2, result.Board.Rows);
            Assert.Equal(5, result.Board.Columns);
        }

        [Fact]
        public void A_board_holding_2048_is_played_by_the_same_rules_as_any_other()
        {
            // The original's SpawnTileIndex switch stops at 1024 and its default returns
            // index 0, the "2" prefab - so the winning tile renders as a 2. Core has no
            // table to fall off the end of, and 2048 merges like every other value.
            Board board = Board.FromArray(new[,] { { 2048, 2048, 0, 0 } });

            MoveResult result = Rules.Move(board, Direction.Left);

            Assert.Equal(4096, result.Board[0, 0]);
            Assert.Equal(4096, result.ScoreDelta);
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

        private static int[] RowOf(Board board)
        {
            var values = new int[board.Columns];

            for (int c = 0; c < board.Columns; c++)
            {
                values[c] = board[0, c];
            }

            return values;
        }

        private static void AssertMerge(Merge merge, int row, int column, int value)
        {
            Assert.Equal(row, merge.Row);
            Assert.Equal(column, merge.Column);
            Assert.Equal(value, merge.Value);
        }

        private static void AssertGrid(Board actual, int[,] expected)
        {
            Assert.Equal(Board.FromArray(expected), actual);
        }

        internal static void AssertEveryCellIsATileValue(Board board)
        {
            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    int value = board[r, c];

                    Assert.True(
                        value == 0 || Board.IsTileValue(value),
                        "Cell (" + r + "," + c + ") holds " + value + ", which is not a tile value.");
                }
            }
        }

        /// <summary>
        /// Rewrites a board so the wall the given direction packs against becomes the
        /// left edge, letting one packing check serve all four directions.
        /// </summary>
        private static Board ReorientToLeft(Board board, Direction direction) => direction switch
        {
            Direction.Left => board,
            Direction.Right => MirrorHorizontally(board),
            Direction.Up => Transpose(board),
            Direction.Down => MirrorHorizontally(Transpose(board)),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        private static Board MirrorHorizontally(Board board)
        {
            var cells = new int[board.Rows, board.Columns];

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    cells[r, board.Columns - 1 - c] = board[r, c];
                }
            }

            return Board.FromArray(cells);
        }

        private static Board Transpose(Board board)
        {
            var cells = new int[board.Columns, board.Rows];

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    cells[c, r] = board[r, c];
                }
            }

            return Board.FromArray(cells);
        }

        private static Board RandomBoard(int seed)
        {
            var rng = new SeededRng(seed);
            var cells = new int[4, 4];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    // Weighted toward small values and empties, so pairs are common
                    // and merges actually happen.
                    int roll = rng.Next(6);
                    cells[r, c] = roll switch
                    {
                        0 => 0,
                        1 => 0,
                        2 => 2,
                        3 => 2,
                        4 => 4,
                        _ => 8,
                    };
                }
            }

            return Board.FromArray(cells);
        }
    }
}
