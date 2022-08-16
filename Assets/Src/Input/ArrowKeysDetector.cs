namespace Arma.Input
{
    using UnityEngine;

    public class ArrowKeysDetector : MonoBehaviour, IInputDetector
    {
        public InputType GetInputType()
        {
            if (Input.GetKeyUp(KeyCode.UpArrow)) return InputType.Up;
            else if (Input.GetKeyUp(KeyCode.DownArrow)) return InputType.Down;
            else if (Input.GetKeyUp(KeyCode.RightArrow)) return InputType.Right;
            else if (Input.GetKeyUp(KeyCode.LeftArrow)) return InputType.Left;
            else return InputType.None;
        }
    }
}
