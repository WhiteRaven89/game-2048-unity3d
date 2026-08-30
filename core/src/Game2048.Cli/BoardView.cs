using System.Text;
using Game2048.Core;

namespace Game2048.Cli
{
    /// <summary>
    /// Turns a board into text. The only place in this solution that decides what a
    /// grid looks like - Core returns numbers and has no opinion.
    /// </summary>
    internal static class BoardView
    {
        public static string Render(Board board)
        {
            int width = CellWidth(board);
            string rule = "+" + string.Join("+", Enumerable.Repeat(new string('-', width + 2), board.Columns)) + "+";

            var text = new StringBuilder();
            text.AppendLine(rule);

            for (int r = 0; r < board.Rows; r++)
            {
                text.Append('|');

                for (int c = 0; c < board.Columns; c++)
                {
                    int value = board[r, c];
                    string cell = value == 0 ? "." : value.ToString();

                    text.Append(' ').Append(cell.PadLeft(width)).Append(" |");
                }

                text.AppendLine();
                text.AppendLine(rule);
            }

            return text.ToString();
        }

        /// <summary>
        /// A single line naming the whole grid, for a harness to assert against
        /// without parsing the box drawing.
        /// </summary>
        public static string Flatten(Board board)
        {
            var cells = new List<string>(board.Rows * board.Columns);

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    cells.Add(board[r, c].ToString());
                }
            }

            return string.Join(",", cells);
        }

        private static int CellWidth(Board board)
        {
            int widest = 1;

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    widest = Math.Max(widest, board[r, c].ToString().Length);
                }
            }

            return widest;
        }
    }
}
