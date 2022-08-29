using mrathod;
using mrathod.Input;
using Patterns;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    GenericFSM<EGameState> fsm = null;

    public void RegisterInput()
    {
        if (inputManager != null)
        {
            InputManager.RegisterEvent(InputReciever);
        }
    }

    public void DeRegisterInput()
    {
        if (inputManager != null)
        {
            InputManager.DeRegisterEvent(InputReciever);
        }
    }

    void Start()
    {
        profileManagerRef.SetGameHandler(this);
        levelManagerRef.SetGameHandler(this);
        InitializeFSM();
    }

    void OnGameStateChangedReciever(EGameState changedState)
    {
        if(changedState == EGameState.WAITING_FOR_INPUT) waitingForInput = true;
        else waitingForInput = false;

        if (changedState == EGameState.LOADED) ChangeGameStateTo(EGameState.WAITING_FOR_INPUT);
    }

    void InitializeFSM()
    {
        fsm = new GenericFSM<EGameState>();
        fsm.Add(new GameLoadingState(this));
        //fsm.Add(new GameLoadedState(this));
        fsm.Add(new WaitingForInputState(this));
        fsm.Add(new ExecuteAlgorithmState(this));
        fsm.Add(new GameOverState(this));

        ChangeGameStateTo(EGameState.LOADING);
    }

    public int GetUserLevel()
    {
        return profileManagerRef.GetUserLevel();
    }

    void InputReciever(InputType inputType)
    {
        levelManagerRef.OnInputRecieved(inputType);
    }

    void ChangeGameStateTo(EGameState newState)
    {
        //stateManager.ChangeStateTo(newState);
        fsm.SetCurrentState(newState);
    }

    public void LoadGame()
    {
        levelManagerRef.LoadGameLevel();
    }

    public void OnSetupCompleted()
    {
        //ChangeGameStateTo(EGameState.LOADED);
        ChangeGameStateTo(EGameState.WAITING_FOR_INPUT);
    }

    public void NoMovesLeft()
    {
        ChangeGameStateTo(EGameState.GAME_OVER);
    }

    public void MovesAvailable()
    {
        ChangeGameStateTo(EGameState.WAITING_FOR_INPUT);
    }

    public void OnInputProcessed()
    {
        ChangeGameStateTo(EGameState.EXECUTE_ALOGRITHM);
    }

    public void ProcessTileMoveAlgorithm()
    {
        levelManagerRef.ProcessTileShiftAlgorithm();
    }

    public void OnGameFinished()
    {
        Debug.Log(":: GameHandler.cs :: OnGameFinished :: Reloading scene");
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}
