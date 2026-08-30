namespace Game2048.Core
{
    /// <summary>
    /// The four moves a player can make. Nothing here is direction-specific beyond
    /// this enum: <see cref="Rules.Move"/> normalises every direction to a single
    /// left-collapse and reverses the normalisation on the way out.
    /// </summary>
    public enum Direction
    {
        Left,
        Right,
        Up,
        Down,
    }
}
