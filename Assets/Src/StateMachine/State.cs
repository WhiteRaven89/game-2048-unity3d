namespace Patterns
{
    public abstract class State<T>
    {
        public string Name { get; set; }

        public T ID { get; private set; }

        public State(T id)
        {
            ID = id;
        }
        public State(T id, string name) : this(id)
        {
            Name = name;
        }

        public abstract void Enter();
        public abstract void Exit();
        /// <summary>
        /// Updates States
        /// </summary>
        /// <param name="dt">delta time</param>
        public abstract void Update(float dt);
    }
}

