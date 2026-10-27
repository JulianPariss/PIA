using System;

public abstract class State
{
    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}

public abstract class State<TKey> : State where TKey : Enum
{
    protected readonly StateMachine<TKey> fsm;

    protected State(StateMachine<TKey> fsm)
    {
        this.fsm = fsm;
    }
}
