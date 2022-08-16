using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public delegate void OnGameStateChanged(StateManager.GameState changedState);

/// <summary>
/// Game State Manager
/// </summary>
public class StateManager : MonoBehaviour
{
    /// <summary>
    /// TODO Later need to convert it to State Machine System
    /// </summary>
    public enum GameState
    {
        None,
        Loading,
        Loaded,
        WaitingForInput,
        CheckingMatches,
        GameOver
    }
    
    private GameState currentGameState = GameState.None;

    /// <summary>
    /// Event listeners for game state changed
    /// </summary>
    List<OnGameStateChanged> gameStateChangedListener = null;

    #region Events Register/Deregister

    /// <summary>
    /// Register to listener for game change events
    /// </summary>
    /// <param name="listener"></param>
    public void RegisterEvent(OnGameStateChanged listener)
    {
        if (gameStateChangedListener == null)
        {
            gameStateChangedListener = new List<OnGameStateChanged>();
        }
        if (!gameStateChangedListener.Contains(listener))
        {
            gameStateChangedListener.Add(listener);
        }
    }

    /// <summary>
    /// Remove listener from game change events events
    /// </summary>
    /// <param name="listener"></param>
    public void DeRegisterEvent(OnGameStateChanged listener)
    {
        if (!gameStateChangedListener.Contains(listener))
            return;

        gameStateChangedListener.Remove(listener);
    }

    /// <summary>
    /// Trigger event with corresponding action.
    /// </summary>
    void RaiseEvent(GameState changedState, params object[] args)
    {
        Debug.Log(":: StateManager :: Raised game state changed event of " + changedState);
        if (gameStateChangedListener != null)
        {
            foreach (OnGameStateChanged listener in gameStateChangedListener)
            {
                listener(changedState);
            }
        }
    }
    #endregion
    
    public void ChangeStateTo(GameState newState)
    {
        Debug.Log(":: StateManager :: Trying to change Game state from " + currentGameState + " to " + newState);
        if (currentGameState == newState)
        {
            Debug.LogWarning(":: StateManager :: Game is already in state : "+ newState);
            return;
        }

        Debug.Log(":: StateManager :: Game state changed from " + currentGameState + " to " + newState);
        currentGameState = newState;
        
        switch (currentGameState)
        {
            case GameState.Loading:
                break;
            case GameState.Loaded:
                break;
            case GameState.WaitingForInput:
                break;
            case GameState.CheckingMatches:
                break;
            case GameState.GameOver:
                break;
            default:
                break;
        }

        RaiseEvent(currentGameState);
    }
}
