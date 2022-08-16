using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds User profile information
/// </summary>
public class ProfileManager : MonoBehaviour
{
    /// <summary>
    /// Holds user level can be save any where on the server or locally
    /// </summary>
    [SerializeField]
    int userCurrentLevel = 0;

    GameHandler gameHandlerRef = null;

    public void SetUserlevel(int aUserCurrentLevel)
    {
        userCurrentLevel = aUserCurrentLevel;
    }

    public int GetUserLevel()
    {
        return userCurrentLevel;
    }

    public void IncreaseUserLevel()
    {
        userCurrentLevel++;
    }

    public void SetGameHandler(GameHandler handler)
    {
        gameHandlerRef = handler;
    }

    void OnEnable()
    {

    }
}
