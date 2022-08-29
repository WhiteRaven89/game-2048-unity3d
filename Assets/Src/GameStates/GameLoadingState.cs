using Patterns;

namespace mrathod
{
    public class GameLoadingState : State<EGameState>
    {
        GameHandler gameHandler;

        public GameLoadingState(GameHandler gameHandler) : base(EGameState.LOADING)
        {
            this.gameHandler = gameHandler;
        }

        public override void Enter()
        {
            gameHandler.LoadGame();
        }

        public override void Exit()
        {
            
        }

        public override void Update(float dt)
        {
            
        }
    }
}
