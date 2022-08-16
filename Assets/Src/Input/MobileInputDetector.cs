
namespace Arma.Input
{
    using System;
    using UnityEngine;

    public class MobileInputDetector : MonoBehaviour, IInputDetector
    {
        private State state = State.SwipeNotStarted;
        private Vector2 startPoint;
        private DateTime timeSwipeStarted;
        private TimeSpan minSwipeDuration = TimeSpan.FromMilliseconds(100);
        private TimeSpan maxSwipeDuration = TimeSpan.FromSeconds(1);

        public InputType GetInputType()
        {
            if (Input.touches.Length > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (state == State.SwipeNotStarted)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        timeSwipeStarted = DateTime.Now;
                        state = State.SwipeStarted;
                        startPoint = touch.position;
                    }
                }
                else if (state == State.SwipeStarted)
                {
                    //  Can check for other conditions like hold or drag

                    //////
                    if (touch.phase == TouchPhase.Ended)
                    {
                        TimeSpan timeDifference = DateTime.Now - timeSwipeStarted;
                        if (timeDifference >= minSwipeDuration && timeDifference <= maxSwipeDuration)
                        {
                            Vector2 touchPosition = touch.position;
                            Vector2 differenceVector = touchPosition - startPoint;
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
            }
            return InputType.None;
        }
    }
}

