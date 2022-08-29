using Patterns;

namespace mrathod
{
    public class WaitingForInputState : State<EGameState>
    {
        GameHandler gameHandler;

        public WaitingForInputState(GameHandler gameHandler) : base(EGameState.WAITING_FOR_INPUT)
        {
            this.gameHandler = gameHandler;
        }

        public override void Enter()
        {
            gameHandler.RegisterInput();
        }

        public override void Exit()
        {
            gameHandler.DeRegisterInput();
        }

        public override void Update(float dt)
        {

        }
    }
}
