using Game2048.Core;

namespace Game2048.Cli
{
    /// <summary>
    /// A front-end for the same rules the Unity project would use, running on plain
    /// .NET with no engine present.
    /// <para>
    /// The replay mode is the load-bearing one. It turns "did that change break the
    /// game?" into a sub-second command, which is what makes an agent loop practical -
    /// the alternative is opening the editor and playing.
    /// </para>
    /// </summary>
    internal static class Program
    {
        private const int Ok = 0;
        private const int UsageError = 2;

        private static int Main(string[] args)
        {
            Options options;

            try
            {
                options = Options.Parse(args);
            }
            catch (FormatException error)
            {
                Console.Error.WriteLine(error.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(Options.Usage);
                return UsageError;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(Options.Usage);
                return Ok;
            }

            try
            {
                // A move file given with --replay, or piped in. Reading stdin when it
                // is redirected means `dotnet run < moves.txt` works without a flag,
                // which is how the harness will call it.
                string? script = options.ReplayPath switch
                {
                    "-" => Console.In.ReadToEnd(),
                    { } path => File.ReadAllText(path),
                    null when Console.IsInputRedirected => Console.In.ReadToEnd(),
                    _ => null,
                };

                return script is null ? PlayInteractively(options) : Replay(options, script);
            }
            catch (FormatException error)
            {
                Console.Error.WriteLine(error.Message);
                return UsageError;
            }
            catch (IOException error)
            {
                // Covers a missing file, an unreadable one, and a directory given where
                // a file was expected. A stack trace is not a useful thing to hand a
                // harness that is deciding whether a task succeeded.
                Console.Error.WriteLine(error.Message);
                return UsageError;
            }
            catch (UnauthorizedAccessException error)
            {
                Console.Error.WriteLine(error.Message);
                return UsageError;
            }
            catch (ArgumentOutOfRangeException error)
            {
                // --rows 0 and friends reach Core, which refuses them.
                Console.Error.WriteLine(error.Message);
                return UsageError;
            }
            catch (InvalidOperationException error)
            {
                // A 1x1 board cannot hold the two opening tiles.
                Console.Error.WriteLine(error.Message);
                return UsageError;
            }
        }

        private static int Replay(Options options, string script)
        {
            IReadOnlyList<Direction> moves = MoveScript.Parse(script);
            var game = new Game(options.Rows, options.Columns, options.Seed);

            int applied = 0;

            foreach (Direction move in moves)
            {
                if (game.TryMove(move))
                {
                    applied++;
                }
            }

            // Key=value first so a harness can grep without parsing the grid.
            Console.WriteLine($"seed={options.Seed}");
            Console.WriteLine($"size={options.Rows}x{options.Columns}");
            Console.WriteLine($"moves={moves.Count}");
            Console.WriteLine($"applied={applied}");
            Console.WriteLine($"rejected={moves.Count - applied}");
            Console.WriteLine($"score={game.Score}");
            Console.WriteLine($"over={game.IsOver.ToString().ToLowerInvariant()}");
            Console.WriteLine($"cells={BoardView.Flatten(game.Board)}");
            Console.Write(BoardView.Render(game.Board));

            return Ok;
        }

        private static int PlayInteractively(Options options)
        {
            var game = new Game(options.Rows, options.Columns, options.Seed);

            Console.WriteLine($"2048 - seed {options.Seed}. WASD or arrow keys to move, Q to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine($"Score: {game.Score}");
                Console.Write(BoardView.Render(game.Board));

                if (game.IsOver)
                {
                    Console.WriteLine($"No moves left. Final score {game.Score}.");
                    return Ok;
                }

                Direction? move = ReadMove();

                if (move is null)
                {
                    Console.WriteLine($"Stopped. Score {game.Score}.");
                    return Ok;
                }

                Console.WriteLine();

                if (!game.TryMove(move.Value))
                {
                    // Saying so matters: the original silently spawns a tile here, which
                    // punishes the player for an input that did nothing.
                    Console.WriteLine($"{move} changes nothing - no tile spawned.");
                }
            }
        }

        /// <summary>Null means the player asked to stop.</summary>
        private static Direction? ReadMove()
        {
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                switch (key.Key)
                {
                    case ConsoleKey.A:
                    case ConsoleKey.LeftArrow:
                        return Direction.Left;

                    case ConsoleKey.D:
                    case ConsoleKey.RightArrow:
                        return Direction.Right;

                    case ConsoleKey.W:
                    case ConsoleKey.UpArrow:
                        return Direction.Up;

                    case ConsoleKey.S:
                    case ConsoleKey.DownArrow:
                        return Direction.Down;

                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        return null;
                }
            }
        }

        private sealed class Options
        {
            public const string Usage = """
                Usage: Game2048.Cli [options]

                  --seed <n>       Seed for the game. Defaults to a random one; pass a
                                   value to make the run reproducible.
                  --rows <n>       Board rows (default 4).
                  --cols <n>       Board columns (default 4).
                  --replay <path>  Play a move file and print the end state, then exit.
                                   Use - to read the file from stdin. A redirected stdin
                                   is treated as a replay even without this flag.
                  --help           This text.

                A move file contains the letters L R U D; # starts a comment.
                """;

            public int Rows { get; private set; } = 4;

            public int Columns { get; private set; } = 4;

            public int Seed { get; private set; } = Environment.TickCount;

            public string? ReplayPath { get; private set; }

            public bool ShowHelp { get; private set; }

            public static Options Parse(string[] args)
            {
                var options = new Options();

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--help":
                        case "-h":
                            options.ShowHelp = true;
                            break;

                        case "--seed":
                            options.Seed = Number(args, ++i, "--seed");
                            break;

                        case "--rows":
                            options.Rows = Number(args, ++i, "--rows");
                            break;

                        case "--cols":
                            options.Columns = Number(args, ++i, "--cols");
                            break;

                        case "--replay":
                            options.ReplayPath = Value(args, ++i, "--replay");
                            break;

                        default:
                            throw new FormatException($"Unknown option '{args[i]}'.");
                    }
                }

                return options;
            }

            private static string Value(string[] args, int index, string option) =>
                index < args.Length ? args[index] : throw new FormatException($"{option} needs a value.");

            private static int Number(string[] args, int index, string option) =>
                int.TryParse(Value(args, index, option), out int value)
                    ? value
                    : throw new FormatException($"{option} needs a whole number.");
        }
    }
}
