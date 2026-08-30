using System;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// <see cref="Rules.SpawnTile"/>: the only place randomness enters the rules, and
    /// therefore the only place a replay can go wrong.
    /// </summary>
    public class RulesSpawnTests
    {
        [Fact]
        public void SpawnTile_adds_exactly_one_tile_and_leaves_the_rest_alone()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 4, 0, 8 },
                { 0, 16, 32, 0 },
                { 64, 0, 128, 0 },
                { 0, 256, 0, 512 },
            });

            Board spawned = Rules.SpawnTile(board, new SeededRng(1));

            Assert.Equal(board.TileCount + 1, spawned.TileCount);

            int changed = 0;

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (board[r, c] != spawned[r, c])
                    {
                        changed++;
                        Assert.Equal(0, board[r, c]);
                        Assert.Contains(spawned[r, c], new[] { 2, 4 });
                    }
                }
            }

            Assert.Equal(1, changed);
        }

        [Fact]
        public void SpawnTile_does_not_modify_the_board_it_was_given()
        {
            Board board = Board.Empty(4, 4);

            Rules.SpawnTile(board, new SeededRng(1));

            Assert.Equal(0, board.TileCount);
        }

        [Fact]
        public void SpawnTile_draws_exactly_twice_however_full_the_board_is()
        {
            // A replay is a seed plus a list of moves. It only reproduces if the number
            // of draws per turn is fixed - a scan-until-empty spawn would consume a
            // different count depending on the board, and every replay would drift.
            Board sparse = Board.Empty(4, 4);
            Board crowded = Board.FromArray(new[,]
            {
                { 2, 4, 8, 16 },
                { 32, 64, 128, 256 },
                { 512, 1024, 2048, 4096 },
                { 2, 4, 8, 0 },
            });

            var onSparse = new CountingRng(0, 5);
            var onCrowded = new CountingRng(0, 5);

            Rules.SpawnTile(sparse, onSparse);
            Rules.SpawnTile(crowded, onCrowded);

            Assert.Equal(2, onSparse.Calls);
            Assert.Equal(2, onCrowded.Calls);

            // First draw picks among empty cells, second picks the value.
            Assert.Equal(new[] { 16, 10 }, onSparse.Bounds);
            Assert.Equal(new[] { 1, 10 }, onCrowded.Bounds);
        }

        [Fact]
        public void SpawnTile_places_the_nth_empty_cell_in_reading_order()
        {
            Board board = Board.FromArray(new[,]
            {
                { 2, 0, 0 },
                { 0, 4, 0 },
            });

            // Empty cells in reading order: (0,1) (0,2) (1,0) (1,2). Ask for the third.
            Board spawned = Rules.SpawnTile(board, new CountingRng(2, 5));

            Assert.Equal(2, spawned[1, 0]);
            Assert.Equal(0, spawned[0, 1]);
            Assert.Equal(0, spawned[0, 2]);
            Assert.Equal(0, spawned[1, 2]);
        }

        [Theory]
        [InlineData(0, 4)]      // second draw 0 -> a 4
        [InlineData(1, 2)]
        [InlineData(9, 2)]
        public void The_second_draw_decides_the_value_with_a_four_one_time_in_ten(int roll, int expected)
        {
            Board spawned = Rules.SpawnTile(Board.Empty(1, 1), new CountingRng(0, roll));

            Assert.Equal(expected, spawned[0, 0]);
        }

        [Fact]
        public void Roughly_one_spawn_in_ten_is_a_four()
        {
            int fours = 0;
            var rng = new SeededRng(8);

            for (int i = 0; i < 10_000; i++)
            {
                if (Rules.SpawnTile(Board.Empty(1, 1), rng)[0, 0] == 4)
                {
                    fours++;
                }
            }

            Assert.InRange(fours, 850, 1150);
        }

        [Fact]
        public void Every_empty_cell_can_be_spawned_into()
        {
            // Guards against an off-by-one in the walk that would leave one cell - most
            // likely the last - permanently unreachable.
            var seen = new bool[4, 4];
            var rng = new SeededRng(77);

            for (int i = 0; i < 5_000; i++)
            {
                Board spawned = Rules.SpawnTile(Board.Empty(4, 4), rng);

                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        if (spawned[r, c] != 0)
                        {
                            seen[r, c] = true;
                        }
                    }
                }
            }

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Assert.True(seen[r, c], "Cell (" + r + "," + c + ") was never spawned into.");
                }
            }
        }

        [Fact]
        public void SpawnTile_on_a_full_board_refuses_rather_than_looping_forever()
        {
            // The original walks forward from a random cell until it finds a gap. On a
            // full board that loop has no exit.
            Board full = Board.FromArray(new[,] { { 2, 4 }, { 8, 16 } });

            Assert.Throws<InvalidOperationException>(() => Rules.SpawnTile(full, new SeededRng(1)));
        }

        [Fact]
        public void SpawnTile_rejects_null_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => Rules.SpawnTile(null!, new SeededRng(1)));
            Assert.Throws<ArgumentNullException>(() => Rules.SpawnTile(Board.Empty(2, 2), null!));
        }
    }
}
