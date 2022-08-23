
namespace mrathod.Input
{
    public interface IInputDetector
    {
        InputType GetInputType();
    }

    public enum InputType { None, Up, Down, Left, Right };

    public enum State
    {
        SwipeNotStarted,
        SwipeStarted
    }
}

