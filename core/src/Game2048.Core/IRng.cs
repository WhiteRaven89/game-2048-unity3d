namespace Game2048.Core
{
    /// <summary>
    /// The one source of randomness in the rules. It is an interface because
    /// deterministic replay requires the caller to supply the sequence, and because
    /// there are genuinely two implementations: a seeded generator for play and
    /// replay, and hand-written stubs in tests that return a fixed sequence.
    /// </summary>
    public interface IRng
    {
        /// <summary>
        /// Returns a value in <c>[0, maxExclusive)</c>.
        /// Throws if <paramref name="maxExclusive"/> is not positive.
        /// </summary>
        int Next(int maxExclusive);
    }
}
