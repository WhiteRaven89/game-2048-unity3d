using UnityEngine;
using System;

/// <summary>
/// Holds Level informtion
/// Eg. Grid size, if Grid color or other values in the game level
/// </summary>
[Serializable]
public class SingleLevelData
{
    /// <summary>
    /// Grid data for the level
    /// </summary>
    [SerializeField]
    GridData gridData = null;

    public GridData GetGridData()
    {
        return gridData;
    }

    public SingleLevelData()
    {
        gridData = new GridData();
    }
}
