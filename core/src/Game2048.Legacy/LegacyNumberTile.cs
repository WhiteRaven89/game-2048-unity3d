namespace Game2048.Legacy
{
    /// <summary>
    /// Stands in for the <c>NumberTile</c> MonoBehaviour: a tile's coordinates and
    /// value, stored on the view object rather than in the board.
    /// <para>
    /// Field names match the original so the port can be read side by side with
    /// <c>Assets/Src/Managers/LevelManager.cs</c>.
    /// </para>
    /// </summary>
    public sealed class LegacyNumberTile
    {
        public LegacyNumberTile(int xCoord, int yCoord, int tileValue)
        {
            XCoord = xCoord;
            YCoord = yCoord;
            TileValue = tileValue;
        }

        public int XCoord { get; }

        public int YCoord { get; }

        public int TileValue { get; }
    }
}
