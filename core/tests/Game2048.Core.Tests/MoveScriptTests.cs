using System;
using Game2048.Cli;
using Game2048.Core;

namespace Game2048.Core.Tests
{
    /// <summary>
    /// The replay file format. Tested because a misparse is silent: the run still
    /// finishes, still prints a board, and simply played a different game.
    /// </summary>
    public class MoveScriptTests
    {
        [Fact]
        public void The_four_letters_map_to_the_four_directions()
        {
            Assert.Equal(
                new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down },
                MoveScript.Parse("LRUD"));
        }

        [Fact]
        public void Case_does_not_matter()
        {
            Assert.Equal(MoveScript.Parse("LRUD"), MoveScript.Parse("lrud"));
        }

        [Theory]
        [InlineData("L R U D")]
        [InlineData("L,R,U,D")]
        [InlineData("L\nR\nU\nD\n")]
        [InlineData("  L\t R \r\n U D  ")]
        public void Whitespace_and_commas_are_separators_not_moves(string text)
        {
            Assert.Equal(new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down }, MoveScript.Parse(text));
        }

        [Fact]
        public void A_hash_comments_out_the_rest_of_the_line_only()
        {
            IReadOnlyList<Direction> moves = MoveScript.Parse("L # this R is ignored\nU");

            Assert.Equal(new[] { Direction.Left, Direction.Up }, moves);
        }

        [Fact]
        public void A_whole_line_can_be_a_comment()
        {
            Assert.Equal(new[] { Direction.Left }, MoveScript.Parse("# opening notes\nL"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   \n\n  ")]
        [InlineData("# nothing but a comment")]
        public void An_empty_script_is_an_empty_list_not_an_error(string text)
        {
            Assert.Empty(MoveScript.Parse(text));
        }

        [Fact]
        public void An_unknown_character_is_refused_rather_than_skipped()
        {
            // Skipping it would turn a typo into a different game that still reports
            // success, which is the failure mode a harness cannot see.
            FormatException error = Assert.Throws<FormatException>(() => MoveScript.Parse("LRXD"));

            Assert.Contains("'X'", error.Message);
        }

        [Fact]
        public void The_error_names_the_line_the_bad_character_is_on()
        {
            FormatException error = Assert.Throws<FormatException>(() => MoveScript.Parse("L\nR\n\nQ"));

            Assert.Contains("Line 4", error.Message);
        }

        [Theory]
        [InlineData('W')]
        [InlineData('A')]
        [InlineData('S')]
        public void WASD_is_not_accepted_in_a_file_because_D_would_be_ambiguous(char key)
        {
            // On a WASD keyboard D is Right; in a move file D is Down. Accepting both
            // notations would make the meaning of a file depend on who wrote it, so
            // the file format takes L R U D and the keyboard layout lives in the CLI.
            Assert.Throws<FormatException>(() => MoveScript.Parse(key.ToString()));
        }
    }
}
