public class BoidRespawnState : BoidStateBase
{
    public BoidRespawnState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
    {
    }

    public override void Enter()
    {
        boid.SetEvading(false);
        boid.StartRespawnSequence();
    }
}
