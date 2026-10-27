using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Máquina de Estados Finita (FSM) genérica, reutilizable y desacoplada de MonoBehaviour.
/// Gestiona estados indexados por Enum, evitando instanciaciones en runtime y sobrecarga del GC.
/// </summary>
/// <typeparam name="TKey">Tipo de Enum que representa los identificadores de estado.</typeparam>
public class StateMachine<TKey> where TKey : Enum
{
    private readonly Dictionary<TKey, State<TKey>> states = new Dictionary<TKey, State<TKey>>();

    public TKey CurrentKey { get; private set; }
    public State<TKey> CurrentState { get; private set; }
    public TKey PreviousKey { get; private set; }
    public bool HasState => CurrentState != null;

    public event Action<TKey, TKey> OnStateChanged;

    public void Register(TKey key, State<TKey> state)
    {
        if (state == null)
        {
            Debug.LogWarning($"[StateMachine] Intento de registrar un estado nulo para la clave {key}.");
            return;
        }

        states[key] = state;
    }

    public void ChangeState(TKey newKey)
    {
        if (HasState && EqualityComparer<TKey>.Default.Equals(CurrentKey, newKey))
        {
            return;
        }

        if (!states.TryGetValue(newKey, out var newState))
        {
            Debug.LogWarning($"[StateMachine] Estado no registrado para la clave {newKey}.");
            return;
        }

        CurrentState?.Exit();
        PreviousKey = CurrentKey;
        CurrentKey = newKey;
        CurrentState = newState;
        CurrentState.Enter();

        OnStateChanged?.Invoke(PreviousKey, CurrentKey);
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }
}
