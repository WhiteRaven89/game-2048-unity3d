using Game2048.Core;

namespace Game2048.Cli
{
    /// <summary>
    /// Reads a move file: the letters L, R, U, D in any arrangement, with <c>#</c>
    /// starting a comment and all other whitespace and commas ignored.
    /// <para>
    /// WASD is deliberately not accepted here. In a script D would have to mean Down,
    /// while on a WASD keyboard D means Right - one letter, two meanings, and a replay
    /// file that silently plays a different game depending on who wrote it. WASD is a
    /// key layout; this is a file format.
    /// </para>
    /// <para>
    /// A replay is a seed plus this list and nothing else, which only works because
    /// Core draws a fixed number of random values per turn - see
    /// <see cref="Rules.SpawnTile"/> - so spawns never have to be recorded.
    /// </para>
    /// </summary>
    internal static class MoveScript
    {
        public static IReadOnlyList<Direction> Parse(string text)
        {
            var moves = new List<Direction>();
            int line = 1;
            bool inComment = false;

            foreach (char character in text)
            {
                if (character == '\n')
                {
                    line++;
                    inComment = false;
                    continue;
                }

                if (inComment || char.IsWhiteSpace(character) || character == ',')
                {
                    continue;
                }

                if (character == '#')
                {
                    inComment = true;
                    continue;
                }

                moves.Add(ToDirection(character, line));
            }

            return moves;
        }

        private static Direction ToDirection(char character, int line) => char.ToUpperInvariant(character) switch
        {
            'L' => Direction.Left,
            'R' => Direction.Right,
            'U' => Direction.Up,
            'D' => Direction.Down,
            _ => throw new FormatException(
                $"Line {line}: '{character}' is not a move. A move file uses L, R, U and D."),
        };
    }
}
