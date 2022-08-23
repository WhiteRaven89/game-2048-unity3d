using System.Collections.Generic;

//Reference :  https://faramira.com/generic-finite-state-machine-using-csharp/
namespace Patterns
{
    public class GenericFSM<T>
    {
        protected Dictionary<T, State<T>> mStates;

        protected State<T> mPreviousState;

        protected State<T> mCurrentState;

        public GenericFSM()
        {
            mStates = new Dictionary<T, State<T>>();
        }

        public void Add(State<T> state)
        {
            mStates.Add(state.ID, state);
        }

        public void Add(T stateID, State<T> state)
        {
            mStates.Add(stateID, state);
        }

        public State<T> GetState(T stateID)
        {
            if(mStates.ContainsKey(stateID))
                return mStates[stateID];
            return null;
        }

        public void SetCurrentState(T stateID)
        {
            State<T> state = mStates[stateID];
            SetCurrentState(state);
        }

        public State<T> GetPreviousState()
        {
            return mPreviousState;
        }

        public State<T> GetCurrentState()
        {
            return mCurrentState;
        }

        public void SetCurrentState(State<T> state)
        {
            if (mCurrentState == state)
            {
                return;
            }

            if (mCurrentState != null)
            {
                mCurrentState.Exit();
            }

            mPreviousState = mCurrentState != null ? mCurrentState : state;
            mCurrentState = state;

            if (mCurrentState != null)
            {
                mCurrentState.Enter();
            }
        }

        public void Update(float dt)
        {
            if (mCurrentState != null)
            {
                mCurrentState.Update(dt);
            }
        }
    }
}
