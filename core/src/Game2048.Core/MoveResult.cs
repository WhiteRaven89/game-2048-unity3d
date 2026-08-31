using System;
using System.Collections.Generic;

namespace Game2048.Core
{
    /// <summary>
    /// Everything one move produced. The board is new; the board handed to
    /// <see cref="Rules.Move"/> is not touched.
    /// <para>
    /// Note what is absent: no spawned tile. Spawning is a separate call taking an
    /// <see cref="IRng"/>, which is what makes a move sequence replayable - the
    /// randomness enters at one named place instead of in the middle of the rules.
    /// </para>
    /// </summary>
    public readonly struct MoveResult
    {
        private readonly IReadOnlyList<Merge>? _merges;

        public MoveResult(Board board, int scoreDelta, bool moved, IReadOnlyList<Merge> merges)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            ScoreDelta = scoreDelta;
            Moved = moved;
            _merges = merges ?? throw new ArgumentNullException(nameof(merges));
        }

        /// <summary>The board after the move.</summary>
        public Board Board { get; }

        /// <summary>Points scored: the sum of the values of tiles created by merges.</summary>
        public int ScoreDelta { get; }

        /// <summary>
        /// False when no tile changed position or value. A caller that spawns on every
        /// move regardless of this flag will eventually fill a board that had a legal
        /// move left in it.
        /// </summary>
        public bool Moved { get; }

        /// <summary>
        /// The merges this move performed. Empty, never null - including on a
        /// <c>default(MoveResult)</c>.
        /// </summary>
        public IReadOnlyList<Merge> Merges => _merges ?? Array.Empty<Merge>();

        /// <summary>
        /// How many merges this move performed - pairs collapsed, not tiles consumed.
        /// A move that turns [2,2,2,2] into [4,4,0,0] reports 2, not 4.
        /// <para>
        /// This is <see cref="Merges"/>.Count and nothing else. It is here so a caller
        /// that only wants the number - a combo counter, an analytics event - does not
        /// have to touch the list, and so the count cannot drift from the list it
        /// counts.
        /// </para>
        /// </summary>
        public int TilesMerged => Merges.Count;

        /// <summary>
        /// The largest tile on <see cref="Board"/>, or 0 when there is no tile on it.
        /// <para>
        /// Scanned on each read rather than stored beside the board, for the reason
        /// given on <see cref="Game.IsOver"/>: a stored copy is a second thing
        /// claiming to describe the board, and two such things can disagree. A 4x4
        /// grid is sixteen reads, which is not worth a class of bug.
        /// </para>
        /// </summary>
        public int MaxTile
        {
            get
            {
                // default(MoveResult) leaves Board null despite the non-nullable
                // declaration - the same hole Merges covers above.
                Board board = Board;

                if (board is null)
                {
                    return 0;
                }

                int max = 0;

                for (int r = 0; r < board.Rows; r++)
                {
                    for (int c = 0; c < board.Columns; c++)
                    {
                        int value = board[r, c];

                        if (value > max)
                        {
                            max = value;
                        }
                    }
                }

                // Empty cells are 0 and tiles are at least 2, so an all-empty board
                // falls out as 0 without a special case.
                return max;
            }
        }
    }
}
