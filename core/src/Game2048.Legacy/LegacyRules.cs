using System;
using System.Collections.Generic;

namespace Game2048.Legacy
{
    /// <summary>
    /// A faithful port of two methods from <c>Assets/Src/Managers/LevelManager.cs</c>,
    /// with their defects intact.
    /// <para>
    /// The point of this project is that the same test suite can be pointed at the old
    /// logic and the new one. A claim that the original has bugs is worth less than the
    /// original failing a test in front of you.
    /// </para>
    /// <para>
    /// Nothing here is fixed, tidied or modernised. The loops, the variable names, the
    /// statement order and the guards are the original's. The only changes are the ones
    /// forced by removing Unity: <c>GameObject</c> becomes
    /// <see cref="LegacyTileObject"/>, <c>NumberTile</c> becomes
    /// <see cref="LegacyNumberTile"/>, and the tile list is built from a grid instead
    /// of from a scene. Every <c>!</c> below marks a place the original dereferences a
    /// reference it never checked.
    /// </para>
    /// </summary>
    public static class LegacyRules
    {
        /// <summary>
        /// Port of <c>LevelManager.IsMoveLeft()</c>. Answers "does the player still
        /// have a move?" - and throws <see cref="NullReferenceException"/> on a full
        /// board, which is the only situation it is ever asked about.
        /// <para>
        /// Two defects, one masking the other:
        /// </para>
        /// <para>
        /// 1. The right and up neighbours are fetched and dereferenced before any
        /// bounds test. At the last column, <c>y + 1</c> is off the grid, the lookup
        /// returns null, and <c>GetComponent</c> throws.
        /// </para>
        /// <para>
        /// 2. The guards are on the wrong axes. <c>x != rows - 1</c> gates the
        /// right-hand neighbour, which varies in <c>y</c>; <c>y != columns - 1</c>
        /// gates the up neighbour, which varies in <c>x</c>. Even with the null
        /// dereference fixed, this would skip real merges - so fixing only the crash
        /// would leave a wrong answer behind.
        /// </para>
        /// </summary>
        public static bool IsMoveLeft(int[,] cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            int rows = cells.GetLength(0);
            int columns = cells.GetLength(1);

            // The original's numberTiles list: view objects for occupied cells only.
            var numberTiles = new List<LegacyTileObject>();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (cells[r, c] != 0)
                    {
                        numberTiles.Add(new LegacyTileObject(new LegacyNumberTile(r, c, cells[r, c])));
                    }
                }
            }

            // ---- from here down this is LevelManager.IsMoveLeft(), transcribed ----

            if (numberTiles.Count < rows * columns)
            {
                return true;
            }

            for (int x = 0; x < rows; x++)
            {
                for (int y = 0; y < columns; y++)
                {
                    LegacyNumberTile currentTile = GetNumberTileBasedOnCoord(numberTiles, x, y)!.GetComponent();
                    LegacyNumberTile rightTile = GetNumberTileBasedOnCoord(numberTiles, x, y + 1)!.GetComponent();
                    LegacyNumberTile upTile = GetNumberTileBasedOnCoord(numberTiles, x + 1, y)!.GetComponent();

                    if (x != rows - 1 && currentTile.TileValue == rightTile.TileValue)
                    {
                        return true;
                    }
                    else if (y != columns - 1 && currentTile.TileValue == upTile.TileValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Port of <c>LevelManager.SpawnTileIndex()</c>: maps a tile value to an index
        /// into the prefab list.
        /// <para>
        /// The switch stops at 1024 and the default returns 0 - and index 0 is the "2"
        /// prefab. Reach 2048, the tile the whole game is named after, and it renders
        /// as a 2.
        /// </para>
        /// </summary>
        public static int SpawnTileIndex(int powerOf2Value)
        {
            switch (powerOf2Value)
            {
                case 2: return 0;   //  tile of 2
                case 4: return 1;   //  tile of 4
                case 8: return 2;   //  tile of 8
                case 16: return 3;   //  tile of 16
                case 32: return 4;   //  tile of 32
                case 64: return 5;   //  tile of 64
                case 128: return 6;   //  tile of 128
                case 256: return 7;   //  tile of 256
                case 512: return 8;   //  tile of 512
                case 1024: return 9;   //  tile of 1204
                default:
                    break;
            }

            return 0;
        }

        /// <summary>
        /// Port of <c>LevelManager.GetNumberTileBasedOnCoord()</c>: a linear search of
        /// the tile list, returning null when nothing matches.
        /// <para>
        /// The original runs this - a <c>List.Find</c> with a <c>GetComponent</c> inside
        /// the predicate - from inside nested loops. Kept as a search rather than an
        /// index lookup because the null it returns off the edge of the grid is the
        /// whole mechanism of the crash.
        /// </para>
        /// </summary>
        private static LegacyTileObject? GetNumberTileBasedOnCoord(List<LegacyTileObject> numberTiles, int x, int y)
        {
            return numberTiles.Find(t => t.GetComponent().XCoord == x && t.GetComponent().YCoord == y);
        }
    }
}
