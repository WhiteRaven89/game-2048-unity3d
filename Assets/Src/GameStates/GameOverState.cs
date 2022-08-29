using Patterns;

namespace mrathod
{
    public class GameOverState : State<EGameState>
    {
        GameHandler gameHandler;

        public GameOverState(GameHandler gameHandler) : base(EGameState.GAME_OVER)
        {
            this.gameHandler = gameHandler;
        }

        public override void Enter()
        {

        }

        public override void Exit()
        {

        }

        public override void Update(float dt)
        {

        }
    }
}
