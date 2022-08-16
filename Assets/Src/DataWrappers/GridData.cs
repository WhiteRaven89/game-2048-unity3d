using UnityEngine;
using System;

/// <summary>
/// Holds grid information
/// </summary>
[Serializable]
public class GridData
{
    /// <summary>
    /// No of Rows in the grid
    /// </summary>
    [Range(4,10)]
    [SerializeField]
    int size = 4;

    public GridData() { }

    public GridData(int aSize)
    {
        SetDimension(aSize);
    }

    public void SetDimension(int aSize)
    {
        size = aSize;
    }

    public Vector2 GetDimension()
    {
        return new Vector2(size, size);
    }
}
