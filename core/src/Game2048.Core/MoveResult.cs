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
    }
}
