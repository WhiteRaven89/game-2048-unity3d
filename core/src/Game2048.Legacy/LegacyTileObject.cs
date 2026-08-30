namespace Game2048.Legacy
{
    /// <summary>
    /// Stands in for the <c>GameObject</c> a tile hangs off.
    /// <para>
    /// It exists so the port can reproduce the original's two-step lookup exactly:
    /// find an object, then call <c>GetComponent</c> on it. That second step is where
    /// the crash lives, and collapsing the two into one array access would quietly
    /// engineer the bug away.
    /// </para>
    /// </summary>
    public sealed class LegacyTileObject
    {
        private readonly LegacyNumberTile _component;

        public LegacyTileObject(LegacyNumberTile component)
        {
            _component = component;
        }

        /// <summary>Stands in for <c>GetComponent&lt;NumberTile&gt;()</c>.</summary>
        public LegacyNumberTile GetComponent() => _component;
    }
}
