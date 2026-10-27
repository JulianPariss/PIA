using System;

/// <summary>
/// Clase base abstracta no-MonoBehaviour para estados de una FSM.
/// Define el ciclo de vida canónico de ejecución: Enter, Tick y Exit.
/// </summary>
public abstract class State
{
    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}

/// <summary>
/// Clase base genérica para estados asociados a una StateMachine tipada por Enum.
/// Proporciona acceso protegido a la máquina de estados para solicitar transiciones.
/// </summary>
/// <typeparam name="TKey">Tipo de Enum que identifica los estados.</typeparam>
public abstract class State<TKey> : State where TKey : Enum
{
    protected readonly StateMachine<TKey> fsm;

    protected State(StateMachine<TKey> fsm)
    {
        this.fsm = fsm;
    }
}
