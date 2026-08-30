using System;

namespace Game2048.Core
{
    /// <summary>
    /// One game in progress: the board, the score, and the turn sequence that ties
    /// a move to a spawn.
    /// <para>
    /// Deliberately thin. Everything that decides an outcome lives in
    /// <see cref="Rules"/> as a function; this type only holds what has to persist
    /// between turns. If a rule ends up here, it has become untestable without
    /// constructing a game to reach it.
    /// </para>
    /// </summary>
    public sealed class Game
    {
        private readonly IRng _rng;

        /// <summary>
        /// Starts a game with two tiles on the board, as the original does. The same
        /// seed always produces the same game.
        /// </summary>
        public Game(int rows, int columns, int seed)
        {
            _rng = new SeededRng(seed);

            Board board = Board.Empty(rows, columns);
            board = Rules.SpawnTile(board, _rng);
            board = Rules.SpawnTile(board, _rng);

            Board = board;
        }

        public Board Board { get; private set; }

        public int Score { get; private set; }

        /// <summary>
        /// Recomputed on each read rather than cached. A cached flag is a second
        /// source of truth about the board, which is the mistake being corrected
        /// here; on a 4x4 grid the scan costs nothing worth having a bug for.
        /// </summary>
        public bool IsOver => Rules.IsGameOver(Board);

        /// <summary>
        /// Plays one turn. Returns false when the move changes nothing, in which case
        /// the board and score are untouched and no tile is spawned.
        /// <para>
        /// That guard is the point of <see cref="MoveResult.Moved"/>. The original
        /// spawns after every accepted input without asking whether anything actually
        /// shifted, so holding a direction against a wall hands the player free tiles
        /// and fills the board.
        /// </para>
        /// </summary>
        public bool TryMove(Direction direction)
        {
            MoveResult result = Rules.Move(Board, direction);

            if (!result.Moved)
            {
                return false;
            }

            Board = result.Board;
            Score += result.ScoreDelta;

            // The spawn cannot fail here, and the reason is worth writing down rather
            // than guarding against. A move never creates a tile, so the count after
            // is at most the count before. If the board was already full, the only way
            // anything could shift is a merge, which frees a cell. If it was not full,
            // it still is not. Either way there is somewhere to spawn.
            Board = Rules.SpawnTile(Board, _rng);

            return true;
        }
    }
}
