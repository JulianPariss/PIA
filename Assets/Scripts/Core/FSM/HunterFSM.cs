/// <summary>
/// Controlador de la Máquina de Estados Finita (FSM) para el Cazador. Administra estados con ventana mínima de permanencia para evitar oscilaciones.
/// </summary>
using System;
using UnityEngine;

public class HunterFSM : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private string currentStateName = "None";

    [Header("Estabilidad de Estados")]
    public float minStateDuration = 0.8f;
    public float StateTime { get; private set; } = 0f;

    public bool CanChangeState => CurrentState == null || StateTime >= minStateDuration;

    public IState CurrentState { get; private set; }
    public IState PreviousState { get; private set; }

    public event Action<IState, IState> OnStateChanged;

    public SteeringAgent Agent { get; private set; }

    protected virtual void Awake()
    {
        Agent = GetComponent<SteeringAgent>();
    }

    protected virtual void Update()
    {
        StateTime += Time.deltaTime;
        CurrentState?.UpdateState();
    }

    public void ChangeState(IState newState, bool force = false)
    {
        if (newState == null || CurrentState == newState) return;
        if (!force && CurrentState != null && StateTime < minStateDuration) return;

        CurrentState?.Exit();
        PreviousState = CurrentState;
        CurrentState = newState;
        currentStateName = newState.GetType().Name;
        StateTime = 0f;
        CurrentState.Enter();
        OnStateChanged?.Invoke(PreviousState, CurrentState);
    }
}
