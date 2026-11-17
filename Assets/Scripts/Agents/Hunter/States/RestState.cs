using UnityEngine;

public class RestState : State<HunterStateId>
{
    private readonly HunterNPC hunter;

    public RestState(StateMachine<HunterStateId> fsm, HunterNPC hunter) : base(fsm)
    {
        this.hunter = hunter;
    }

    public override void Enter()
    {
        hunter.CurrentTarget = null;
        hunter.Agent.ResetMotion();
        hunter.SetVisualMode(HunterVisualMode.Rest);
    }

    public override void Tick()
    {
        hunter.Agent.ResetMotion();
        hunter.SetVisualMode(HunterVisualMode.Rest);
        hunter.RechargeEnergy(Time.deltaTime);

        if (hunter.Energy >= hunter.MaxEnergy)
        {
            fsm.ChangeState(HunterStateId.Patrol);
        }
    }

    public override void Exit()
    {
        hunter.Agent.ResetMotion();
    }
}
