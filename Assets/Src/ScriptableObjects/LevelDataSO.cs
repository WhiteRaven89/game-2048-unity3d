using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds level information
/// </summary>
[CreateAssetMenu(fileName = "LevelData", menuName = "ScriptableObjects/CreateLevelSO", order = 1)]
public class LevelDataSO : ScriptableObject
{
    /// <summary>
    /// Holds List of level details
    /// </summary>
    [SerializeField]
    List<SingleLevelData> lstLevelInfo = new List<SingleLevelData>();

    public SingleLevelData GetLevelInfo(int aLevel)
    {
        if (aLevel > 0 && aLevel <= lstLevelInfo.Count) return lstLevelInfo[aLevel - 1];

        return null;
    }

    public List<SingleLevelData> GetAllLevelInfo()
    {
        return lstLevelInfo;
    }
}
