using mrathod.Input;
using Patterns;

namespace mrathod
{
    public class ExecuteAlgorithmState : State<EGameState>
    {
        GameHandler gameHandler;

        public ExecuteAlgorithmState(GameHandler gameHandler) : base(EGameState.EXECUTE_ALOGRITHM)
        {
            this.gameHandler = gameHandler;
        }

        public override void Enter()
        {
            gameHandler.ProcessTileMoveAlgorithm();
        }

        public override void Exit()
        {

        }

        public override void Update(float dt)
        {

        }
    }
}
