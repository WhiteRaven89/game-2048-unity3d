namespace Game2048.Core
{
    /// <summary>
    /// One merge that happened during a move: where the combined tile ended up, and
    /// what it became. A view layer can animate from this; a test can sum
    /// <see cref="Value"/> across the list and check it equals the move's score
    /// delta.
    /// </summary>
    public readonly struct Merge
    {
        public Merge(int row, int column, int value)
        {
            Row = row;
            Column = column;
            Value = value;
        }

        /// <summary>Row of the cell the merged tile occupies after the move.</summary>
        public int Row { get; }

        /// <summary>Column of the cell the merged tile occupies after the move.</summary>
        public int Column { get; }

        /// <summary>Value of the tile produced by the merge, i.e. twice the operands.</summary>
        public int Value { get; }

        public override string ToString() => $"Merge({Row},{Column})={Value}";
    }
}
