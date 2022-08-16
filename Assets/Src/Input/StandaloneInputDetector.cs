namespace Arma.Input
{
    using UnityEngine;
    using System;

    public class StandaloneInputDetector : MonoBehaviour, IInputDetector
    {
        private State state = State.SwipeNotStarted;
        private Vector2 startPoint;
        private DateTime timeSwipeStarted;
        private TimeSpan minSwipeDuration = TimeSpan.FromMilliseconds(100);
        private TimeSpan maxSwipeDuration = TimeSpan.FromSeconds(1);

        public InputType GetInputType()
        {
            if (state == State.SwipeNotStarted)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    timeSwipeStarted = DateTime.Now;
                    state = State.SwipeStarted;
                    startPoint = Input.mousePosition;
                }
            }
            else if (state == State.SwipeStarted)
            {
                //  Can check for other conditions like hold or drag

                //////
                if (Input.GetMouseButtonUp(0))
                {
                    TimeSpan timeDifference = DateTime.Now - timeSwipeStarted;
                    if (timeDifference >= minSwipeDuration && timeDifference <= maxSwipeDuration)
                    {
                        Vector2 mousePosition = Input.mousePosition;
                        Vector2 differenceVector = mousePosition - startPoint;
                        float angle = Vector2.Angle(differenceVector, Vector2.right);
                        angle = 360 - angle;

                        state = State.SwipeNotStarted;

                        if (angle >= 315 && angle < 360 || angle >= 0 && angle <= 45) return InputType.Right;
                        else if (angle > 45 && angle <= 135) return InputType.Up;
                        else if (angle > 135 && angle <= 225) return InputType.Left;
                        else return InputType.Down;
                    }
                }
            }
            return InputType.None;
        }
    }
}
