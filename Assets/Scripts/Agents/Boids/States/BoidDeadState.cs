public class BoidDeadState : BoidStateBase
{
    public BoidDeadState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
    {
    }

    public override void Enter()
    {
        boid.SetEvading(false);
        boid.ResetMotion();
    }

    public override void Tick()
    {
        boid.ResetMotion();
    }
}
