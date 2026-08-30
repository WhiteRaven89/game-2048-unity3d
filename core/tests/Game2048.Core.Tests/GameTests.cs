using System;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    public class GameTests
    {
        [Fact]
        public void A_new_game_starts_with_two_tiles_as_the_original_does()
        {
            var game = new Game(4, 4, seed: 1);

            Assert.Equal(2, game.Board.TileCount);
            Assert.Equal(0, game.Score);
            Assert.False(game.IsOver);
        }

        [Fact]
        public void The_same_seed_produces_the_same_opening_board()
        {
            Assert.Equal(new Game(4, 4, 42).Board, new Game(4, 4, 42).Board);
        }

        [Fact]
        public void Different_seeds_produce_different_openings()
        {
            // Two tiles on sixteen cells: a collision is possible but not across this
            // many seeds, and the point is that the seed reaches the board at all.
            var boards = new System.Collections.Generic.HashSet<Board>();

            for (int seed = 0; seed < 20; seed++)
            {
                boards.Add(new Game(4, 4, seed).Board);
            }

            Assert.True(boards.Count > 1, "Every seed produced the same opening board.");
        }

        [Fact]
        public void The_same_seed_and_the_same_moves_produce_the_same_game()
        {
            Direction[] script =
            {
                Direction.Left, Direction.Up, Direction.Right, Direction.Down,
                Direction.Left, Direction.Left, Direction.Up, Direction.Right,
                Direction.Down, Direction.Down, Direction.Left, Direction.Up,
            };

            var first = new Game(4, 4, 7);
            var second = new Game(4, 4, 7);

            foreach (Direction direction in script)
            {
                bool a = first.TryMove(direction);
                bool b = second.TryMove(direction);

                Assert.Equal(a, b);
                Assert.Equal(first.Board, second.Board);
                Assert.Equal(first.Score, second.Score);
            }
        }

        [Fact]
        public void A_move_that_changes_nothing_is_refused_and_costs_the_player_nothing()
        {
            // This is the third defect in the original, and the reason MoveResult
            // carries a Moved flag at all. LevelManager spawns a tile after every
            // accepted input without asking whether the board changed, so holding a
            // direction against a wall hands out free tiles.
            var game = new Game(4, 4, 3);

            // Drive left until nothing more can happen in that direction.
            while (game.TryMove(Direction.Left))
            {
            }

            Board before = game.Board;
            int scoreBefore = game.Score;

            Assert.False(game.TryMove(Direction.Left));
            Assert.Equal(before, game.Board);
            Assert.Equal(scoreBefore, game.Score);
            Assert.Equal(before.TileCount, game.Board.TileCount);
        }

        [Fact]
        public void A_successful_move_spawns_exactly_one_tile()
        {
            var game = new Game(4, 4, 11);

            for (int i = 0; i < 200; i++)
            {
                Board before = game.Board;
                var direction = (Direction)(i % 4);

                int expectedMerges = Rules.Move(before, direction).Merges.Count;

                if (!game.TryMove(direction))
                {
                    Assert.Equal(before, game.Board);
                    continue;
                }

                Assert.Equal(before.TileCount - expectedMerges + 1, game.Board.TileCount);
            }
        }

        [Fact]
        public void Score_accumulates_the_move_deltas_and_never_decreases()
        {
            var game = new Game(4, 4, 5);
            int previous = 0;

            for (int i = 0; i < 300; i++)
            {
                var direction = (Direction)(i % 4);
                int expected = game.Score + Rules.Move(game.Board, direction).ScoreDelta;

                if (game.TryMove(direction))
                {
                    Assert.Equal(expected, game.Score);
                }

                Assert.True(game.Score >= previous, "Score went backwards.");
                previous = game.Score;
            }
        }

        [Fact]
        public void A_game_played_to_the_end_reports_over_without_throwing()
        {
            // The whole point of the boundary. Play until no direction does anything,
            // then ask the question the original cannot survive being asked.
            var game = new Game(4, 4, 2024);

            for (int turn = 0; turn < 20_000 && !game.IsOver; turn++)
            {
                bool anyMoved = false;

                for (int d = 0; d < 4 && !anyMoved; d++)
                {
                    anyMoved = game.TryMove((Direction)((turn + d) % 4));
                }

                if (!anyMoved)
                {
                    break;
                }
            }

            Assert.True(game.IsOver, "The game never reached a finished position.");
            Assert.Equal(game.Board.Rows * game.Board.Columns, game.Board.TileCount);
            Assert.False(game.TryMove(Direction.Left));
            Assert.False(game.TryMove(Direction.Right));
            Assert.False(game.TryMove(Direction.Up));
            Assert.False(game.TryMove(Direction.Down));
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(4, 0)]
        [InlineData(-2, 4)]
        public void A_game_cannot_be_started_on_a_degenerate_board(int rows, int columns)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Game(rows, columns, 1));
        }

        [Fact]
        public void A_board_too_small_to_hold_two_opening_tiles_fails_at_construction()
        {
            // A 1x1 game cannot exist. Failing in the constructor beats handing back a
            // Game that is already over.
            Assert.Throws<InvalidOperationException>(() => new Game(1, 1, 1));
        }
    }
}
