using System;
using System.Collections.Generic;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// A hand-driven <see cref="IRng"/>: returns a scripted sequence and records every
    /// bound it was asked for.
    /// <para>
    /// This is the second implementation that justifies <see cref="IRng"/> existing at
    /// all. Without it, "SpawnTile draws exactly twice" is not a statement any test can
    /// make, and the determinism the replay format depends on would rest on inspection
    /// of the source instead of on a check that runs.
    /// </para>
    /// </summary>
    public sealed class CountingRng : IRng
    {
        private readonly int[] _values;
        private int _index;

        public CountingRng(params int[] values)
        {
            _values = values;
        }

        /// <summary>The bound passed to each call, in order.</summary>
        public List<int> Bounds { get; } = new List<int>();

        public int Calls => _index;

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Bound must be positive.");
            }

            if (_index >= _values.Length)
            {
                throw new InvalidOperationException(
                    "The code under test drew " + (_index + 1) + " values but only " + _values.Length + " were scripted.");
            }

            Bounds.Add(maxExclusive);

            int value = _values[_index++];

            if (value >= maxExclusive)
            {
                throw new InvalidOperationException(
                    "Scripted value " + value + " is not valid for a bound of " + maxExclusive + ".");
            }

            return value;
        }
    }
}
