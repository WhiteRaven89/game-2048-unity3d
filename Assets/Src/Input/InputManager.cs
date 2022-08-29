using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace mrathod.Input
{
    public delegate void InputEventListeners(InputType inputType);

    public class InputManager : MonoBehaviour
    {
        /// <summary>
        /// Input for platform
        /// </summary>
        IInputDetector inputDetector = null;

        /// <summary>
        /// Event listeners for input
        /// </summary>
        static List<InputEventListeners> inputListeners = null;

        // Start is called before the first frame update
        void Start()
        {
            #if UNITY_EDITOR
                inputDetector = gameObject.AddComponent<ArrowKeysDetector>();
            #elif UNITY_ANDROID || UNITY_IOS
                inputDetector = gameObject.AddComponent<MobileInputDetector>();
            #else
                inputDetector = gameObject.AddComponent<StandaloneInputDetector>();
            #endif
        }

        // Update is called once per frame
        void Update()
        {
            if(inputDetector != null)
            {
                InputType inputType = inputDetector.GetInputType();
                if(inputType != InputType.None)
                {
                    RaiseEvent(inputType);
                }
            }
        }

        #region Events Register/Deregister

        /// <summary>
        /// Register to listener to send input events
        /// </summary>
        /// <param name="listener"></param>
        public static void RegisterEvent(InputEventListeners listener)
        {
            if (inputListeners == null)
            {
                inputListeners = new List<InputEventListeners>();
            }
            if (!inputListeners.Contains(listener))
            {
                inputListeners.Add(listener);
            }
        }

        /// <summary>
        /// Remove listener from input events
        /// </summary>
        /// <param name="listener"></param>
        public static void DeRegisterEvent(InputEventListeners listener)
        {
            if (!inputListeners.Contains(listener))
                return;

            inputListeners.Remove(listener);
        }

        /// <summary>
        /// Trigger event with corresponding action.
        /// </summary>
        void RaiseEvent(InputType inputEvent, params object[] args)
        {
            Debug.Log(":: InputManager :: Input raised : " + inputEvent);

            if(inputListeners != null)
            {
                foreach (InputEventListeners listener in inputListeners.ToList())
                {
                    listener(inputEvent);
                }
            }
        }
        #endregion
    }
}
