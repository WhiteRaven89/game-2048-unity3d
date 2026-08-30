using System;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// <see cref="Rules.IsGameOver"/> - the boundary the original cannot survive.
    /// <para>
    /// <c>LevelManager.IsMoveLeft</c> fetches the right and up neighbours and calls
    /// <c>GetComponent</c> on them before testing any index, and it guards the wrong
    /// axis for each: it checks <c>x != rows - 1</c> before using the right-hand
    /// neighbour, which varies in <c>y</c>. So it throws a null reference exactly
    /// when the board fills - the one case the function exists to answer.
    /// </para>
    /// </summary>
    public class RulesGameOverTests
    {
        [Fact]
        public void A_completely_full_board_with_no_equal_neighbours_is_game_over_and_does_not_throw()
        {
            Board full = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
            });

            Assert.True(Rules.IsGameOver(full));
        }

        [Fact]
        public void A_board_with_an_empty_cell_is_never_over()
        {
            Board nearlyFull = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 4, 0 },
            });

            Assert.False(Rules.IsGameOver(nearlyFull));
        }

        [Fact]
        public void A_full_board_with_a_horizontal_pair_is_not_over()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 2, 8 },   // (3,1) and (3,2)
            });

            Assert.False(Rules.IsGameOver(board));
        }

        [Fact]
        public void A_full_board_with_a_vertical_pair_is_not_over()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 4, 4, 2 },   // (2,1) and (3,1)
            });

            Assert.False(Rules.IsGameOver(board));
        }

        [Fact]
        public void A_pair_in_the_very_last_cells_is_still_found()
        {
            // The corner the original never reaches, because it throws on the way.
            Board horizontal = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 4 },
                { 4, 2, 8, 8 },   // last two cells of the last row
            });

            Board vertical = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4 },
                { 4, 2, 4, 2 },
                { 2, 4, 2, 8 },
                { 4, 2, 4, 8 },   // last two cells of the last column
            });

            Assert.False(Rules.IsGameOver(horizontal));
            Assert.False(Rules.IsGameOver(vertical));
        }

        [Fact]
        public void Game_over_is_decided_correctly_on_a_non_square_board()
        {
            // Rows and columns are swapped in the original's guards, which cannot show
            // up while the board is square.
            Board over = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4, 2 },
                { 4, 2, 4, 2, 4 },
            });

            Board notOver = Board.FromArray(new[,]
            {
                { 2, 4, 2, 4, 2 },
                { 4, 2, 4, 2, 2 },   // (0,4) and (1,4)
            });

            Assert.True(Rules.IsGameOver(over));
            Assert.False(Rules.IsGameOver(notOver));
        }

        [Fact]
        public void A_single_cell_board_is_over_as_soon_as_it_is_occupied()
        {
            Assert.False(Rules.IsGameOver(Board.Empty(1, 1)));
            Assert.True(Rules.IsGameOver(Board.FromArray(new[,] { { 2 } })));
        }

        [Fact]
        public void Game_over_agrees_with_trying_every_direction()
        {
            // The definition that matters: over means no move changes anything. Checked
            // against the moves themselves rather than trusting the neighbour scan.
            //
            // The walk starts from a board with a tile on it, not an empty one. On a
            // completely empty board no move changes anything and yet the game is not
            // over - the two definitions genuinely disagree there, and IsGameOver gives
            // the right answer, since a spawn is what happens next. The equivalence
            // holds for every position actually reachable in play.
            var rng = new SeededRng(1234);
            Board board = Rules.SpawnTile(Board.Empty(3, 3), rng);

            for (int step = 0; step < 2_000; step++)
            {
                bool anyMoveWorks = false;

                for (int d = 0; d < 4; d++)
                {
                    if (Rules.Move(board, (Direction)d).Moved)
                    {
                        anyMoveWorks = true;
                        break;
                    }
                }

                Assert.Equal(!anyMoveWorks, Rules.IsGameOver(board));

                if (!anyMoveWorks)
                {
                    break;
                }

                board = Rules.Move(board, (Direction)rng.Next(4)).Board;

                if (board.TileCount < board.Rows * board.Columns)
                {
                    board = Rules.SpawnTile(board, rng);
                }
            }
        }

        [Fact]
        public void IsGameOver_rejects_null()
        {
            Assert.Throws<ArgumentNullException>(() => Rules.IsGameOver(null!));
        }
    }
}
