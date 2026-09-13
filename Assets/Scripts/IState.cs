/// <summary>
/// Interfaz base para los estados de la Máquina de Estados Finita (FSM).
/// </summary>
public interface IState
{
    void Enter();
    void UpdateState();
    void Exit();
}
