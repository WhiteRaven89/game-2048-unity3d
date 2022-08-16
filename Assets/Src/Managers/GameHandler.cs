using Arma.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GameHandler : MonoBehaviour
{
    [SerializeField]
    ProfileManager profileManagerRef = null;

    [SerializeField]
    LevelManager levelManagerRef = null;

    [SerializeField]
    InputManager inputManager = null;

    [SerializeField]
    StateManager stateManager = null;

    [SerializeField]
    bool waitingForInput = false;

    void OnEnable()
    {
        if (stateManager != null)
        {
            stateManager.RegisterEvent(OnGameStateChangedReciever);
        }
        if (inputManager != null)
        {
            InputManager.RegisterEvent(InputReciever);
        }
    }

    void OnDisable()
    {
        if (stateManager != null)
        {
            stateManager.DeRegisterEvent(OnGameStateChangedReciever);
        }
        if (inputManager != null)
        {
            InputManager.DeRegisterEvent(InputReciever);
        }
    }

    void Start()
    {
        Initialize();
    }

    void OnGameStateChangedReciever(StateManager.GameState changedState)
    {
        if(changedState == StateManager.GameState.WaitingForInput) waitingForInput = true;
        else waitingForInput = false;

        if (changedState == StateManager.GameState.Loaded) ChangeGameStateTo(StateManager.GameState.WaitingForInput);
    }

    void Initialize()
    {
        profileManagerRef.SetGameHandler(this);
        levelManagerRef.SetGameHandler(this);
        ChangeGameStateTo(StateManager.GameState.Loading);
    }

    public int GetUserLevel()
    {
        return profileManagerRef.GetUserLevel();
    }

    void InputReciever(InputType inputType)
    {
        if(waitingForInput)
        {
            levelManagerRef.OnInputRecieved(inputType);
        }
    }

    void ChangeGameStateTo(StateManager.GameState newState)
    {
        stateManager.ChangeStateTo(newState);
    }

    public void OnSetupCompleted()
    {
        ChangeGameStateTo(StateManager.GameState.Loaded);
    }

    public void NoMovesLeft()
    {
        ChangeGameStateTo(StateManager.GameState.GameOver);
    }

    public void MovesAvailable()
    {
        ChangeGameStateTo(StateManager.GameState.WaitingForInput);
    }

    public void CheckingMatches()
    {
        ChangeGameStateTo(StateManager.GameState.CheckingMatches);
    }
}
