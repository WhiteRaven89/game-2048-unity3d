using System;
using Game2048.Core;
using Game2048.Legacy;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// The same questions, asked of the original logic and of the extracted rules.
    /// <para>
    /// Every test here passes. What they assert about
    /// <see cref="LegacyRules"/> is that it is broken - the crash and the wrong answer
    /// are pinned as facts, not described in a document. If someone ever fixes
    /// <c>LevelManager</c>, these go red and say so.
    /// </para>
    /// </summary>
    public class LegacyComparisonTests
    {
        /// <summary>Full 4x4, no two orthogonal neighbours equal: a finished game.</summary>
        private static int[,] FinishedGame() => new[,]
        {
            { 2, 4, 2, 4 },
            { 4, 2, 4, 2 },
            { 2, 4, 2, 4 },
            { 4, 2, 4, 2 },
        };

        // ------------------------------------------------- bug 1: the lose condition

        [Fact]
        public void Legacy_throws_when_asked_whether_a_full_board_is_finished()
        {
            // The scan reaches (0,3), looks up the neighbour at column 4, gets nothing
            // back, and calls GetComponent on it. In Unity this is the null reference
            // that ends the game session at the moment the game should end politely.
            Assert.Throws<NullReferenceException>(() => LegacyRules.IsMoveLeft(FinishedGame()));
        }

        [Fact]
        public void Core_answers_the_same_question_and_says_the_game_is_over()
        {
            Assert.True(Rules.IsGameOver(Board.FromArray(FinishedGame())));
        }

        [Fact]
        public void Legacy_also_throws_when_a_move_is_available_but_not_in_the_first_row()
        {
            // Worse than the plain crash: the board is not finished at all. There is a
            // merge waiting in the last row, and the player never gets to make it.
            var cells = new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 8, 8 },   // (3,2) and (3,3) can merge
            };

            Assert.Throws<NullReferenceException>(() => LegacyRules.IsMoveLeft(cells));
            Assert.False(Rules.IsGameOver(Board.FromArray(cells)));
        }

        [Fact]
        public void Legacy_survives_only_when_the_answer_arrives_before_the_last_column()
        {
            // Not uniformly broken - and that is exactly why it shipped. A merge in the
            // first row returns true before the scan ever reaches the edge, so ordinary
            // play looks fine right up until the board fills.
            var cells = new[,]
            {
                { 2, 2, 4, 8 },   // (0,0) and (0,1) merge; found at y = 0
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
            };

            Assert.True(LegacyRules.IsMoveLeft(cells));
            Assert.False(Rules.IsGameOver(Board.FromArray(cells)));
        }

        [Fact]
        public void Both_agree_that_a_board_with_a_free_cell_still_has_a_move()
        {
            // The early return fires before any lookup happens, so the whole of normal
            // play takes a path that cannot crash.
            var cells = new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 4, 0 },
            };

            Assert.True(LegacyRules.IsMoveLeft(cells));
            Assert.False(Rules.IsGameOver(Board.FromArray(cells)));
        }

        // -------------------------------------------------- bug 2: the win condition

        [Theory]
        [InlineData(2, 0)]
        [InlineData(4, 1)]
        [InlineData(1024, 9)]
        public void Legacy_maps_the_values_its_switch_covers(int value, int expectedIndex)
        {
            Assert.Equal(expectedIndex, LegacyRules.SpawnTileIndex(value));
        }

        [Theory]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(8192)]
        public void Legacy_renders_the_winning_tile_as_a_two(int value)
        {
            // The default case returns 0, and prefab 0 is the "2" tile. Reach the tile
            // the game is named after and it draws as the smallest one on the board.
            Assert.Equal(LegacyRules.SpawnTileIndex(2), LegacyRules.SpawnTileIndex(value));
        }

        [Fact]
        public void Core_has_no_such_ceiling_because_it_has_no_table_to_fall_off()
        {
            // Nothing in Core maps a value to a resource. A tile is an int, and 2048
            // merges into 4096 by the same rule that turns two 2s into a 4.
            MoveResult result = Rules.Move(Board.FromArray(new[,] { { 2048, 2048, 0, 0 } }), Direction.Left);

            Assert.Equal(4096, result.Board[0, 0]);
            Assert.True(Board.IsTileValue(result.Board[0, 0]));
        }

        // ------------------------------------------------------ where they do agree

        [Theory]
        [InlineData(2)]
        [InlineData(11)]
        [InlineData(97)]
        public void While_a_cell_is_free_the_two_implementations_agree(int seed)
        {
            // Which is why this shipped. The entire span of ordinary play takes the
            // early-return path, where the old code is correct and cannot crash.
            var rng = new SeededRng(seed);
            Board board = Rules.SpawnTile(Board.Empty(4, 4), rng);

            for (int step = 0; step < 300; step++)
            {
                if (board.TileCount == board.Rows * board.Columns)
                {
                    return;
                }

                Assert.True(LegacyRules.IsMoveLeft(board.ToArray()));
                Assert.False(Rules.IsGameOver(board));

                board = Rules.Move(board, (Direction)rng.Next(4)).Board;

                if (board.TileCount < board.Rows * board.Columns)
                {
                    board = Rules.SpawnTile(board, rng);
                }
            }
        }

        [Fact]
        public void Legacy_can_never_report_that_the_game_is_over()
        {
            // The sharpest way to state bug 1. On a full board the scan reaches the
            // last column of the first row and crashes - unless it returned true
            // earlier, having found a pair. So there is no path on which it returns
            // false. The lose condition is not merely buggy, it is unreachable: this
            // function cannot express the answer it exists to give.
            var rng = new SeededRng(5);

            int crashed = 0;
            int foundAMove = 0;
            int genuinelyOverButCrashed = 0;

            for (int i = 0; i < 500; i++)
            {
                int[,] cells = RandomFullBoard(rng);
                Board board = Board.FromArray(cells);

                try
                {
                    // If it answers at all, the answer is true, and it is right.
                    Assert.True(LegacyRules.IsMoveLeft(cells));
                    Assert.False(Rules.IsGameOver(board));
                    foundAMove++;
                }
                catch (NullReferenceException)
                {
                    crashed++;

                    if (Rules.IsGameOver(board))
                    {
                        genuinelyOverButCrashed++;
                    }
                }
            }

            Assert.Equal(500, crashed + foundAMove);
            Assert.True(crashed > 0, "No sampled full board reached the crash.");
            Assert.True(foundAMove > 0, "No sampled full board took the early-true path.");
            Assert.True(
                genuinelyOverButCrashed > 0,
                "The sample never included a finished game, so it does not demonstrate the bug.");
        }

        private static int[,] RandomFullBoard(IRng rng)
        {
            var cells = new int[4, 4];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    // 2 through 32: a small range, so genuinely finished boards and
                    // boards with a merge available both turn up in the sample.
                    cells[r, c] = 2 << rng.Next(5);
                }
            }

            return cells;
        }
    }
}
