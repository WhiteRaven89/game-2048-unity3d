using System;
using System.Linq;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// Determinism is the property every replay in this project rests on, so it is
    /// tested rather than assumed.
    /// </summary>
    public class SeededRngTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void The_same_seed_produces_the_same_sequence(int seed)
        {
            int[] first = Draw(new SeededRng(seed), 200);
            int[] second = Draw(new SeededRng(seed), 200);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Different_seeds_produce_different_sequences()
        {
            int[] a = Draw(new SeededRng(1), 200);
            int[] b = Draw(new SeededRng(2), 200);

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Seed_zero_is_a_usable_seed_and_not_a_stream_of_zeroes()
        {
            // xorshift has a fixed point at zero. Without the substitution in the
            // constructor, seed 0 would return 0 forever and every "random" spawn
            // would land in the same cell.
            int[] values = Draw(new SeededRng(0), 100);

            Assert.True(values.Distinct().Count() > 1, "Seed 0 produced a constant sequence.");
        }

        [Fact]
        public void Values_stay_inside_the_requested_range()
        {
            var rng = new SeededRng(99);

            for (int i = 0; i < 10_000; i++)
            {
                int value = rng.Next(16);

                Assert.InRange(value, 0, 15);
            }
        }

        [Fact]
        public void A_bound_of_one_always_returns_zero()
        {
            var rng = new SeededRng(5);

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(0, rng.Next(1));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void A_non_positive_bound_throws(int bound)
        {
            var rng = new SeededRng(5);

            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(bound));
        }

        [Fact]
        public void Every_value_in_the_range_is_reachable()
        {
            // Not a distribution test - just a guard against an off-by-one that would
            // make the last cell of a board unspawnable.
            var rng = new SeededRng(2024);
            var seen = new bool[16];

            for (int i = 0; i < 10_000; i++)
            {
                seen[rng.Next(16)] = true;
            }

            Assert.All(seen, hit => Assert.True(hit));
        }

        [Fact]
        public void The_distribution_is_close_enough_to_flat_that_no_cell_is_favoured()
        {
            // Modulo without rejection sampling biases the low end. With 10 buckets
            // over 2^32 the bias is far too small to catch this way, so this test is
            // a smoke check on gross skew, not a proof: it would catch a generator
            // stuck in a short cycle or a bound applied wrongly.
            const int Buckets = 10;
            const int Draws = 100_000;

            var counts = new int[Buckets];
            var rng = new SeededRng(7);

            for (int i = 0; i < Draws; i++)
            {
                counts[rng.Next(Buckets)]++;
            }

            int expected = Draws / Buckets;

            Assert.All(counts, count => Assert.InRange(count, expected * 9 / 10, expected * 11 / 10));
        }

        private static int[] Draw(IRng rng, int count)
        {
            var values = new int[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = rng.Next(1000);
            }

            return values;
        }
    }
}
