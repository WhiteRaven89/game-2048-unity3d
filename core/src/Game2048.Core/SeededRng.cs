using System;

namespace Game2048.Core
{
    /// <summary>
    /// A deterministic generator: the same seed yields the same sequence, on every
    /// runtime, forever.
    /// <para>
    /// This is written out longhand rather than wrapping <see cref="System.Random"/>
    /// deliberately. <c>System.Random</c>'s algorithm is explicitly not part of its
    /// contract, and it changed in .NET 6 - so a replay file recorded under Unity's
    /// Mono would not reproduce under .NET 8, and the determinism this whole design
    /// is built around would hold only by accident. Twenty lines of xorshift is the
    /// cost of the guarantee being real.
    /// </para>
    /// </summary>
    public sealed class SeededRng : IRng
    {
        // Marsaglia's xorshift32. Chosen because it is short enough to read in one
        // sitting and its period (2^32-1) is far beyond anything a 2048 game needs.
        private uint _state;

        public SeededRng(int seed)
        {
            // xorshift32 is a fixed point at zero: seed 0 would emit nothing but 0s
            // forever. Substitute an arbitrary non-zero constant so that seed 0 is a
            // usable seed rather than a silent trap.
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Bound must be positive.");
            }

            // Plain `% maxExclusive` biases toward low values whenever maxExclusive
            // does not divide 2^32. Reject the short tail at the top of the range so
            // the distribution stays flat - it matters because SpawnTile picks a cell
            // this way, and a biased pick would quietly favour one corner.
            ulong bound = (ulong)maxExclusive;
            ulong limit = (0x1_0000_0000UL / bound) * bound;

            uint sample;

            do
            {
                sample = NextUInt();
            }
            while (sample >= limit);

            return (int)(sample % bound);
        }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
