public abstract class BoidStateBase : State<BoidStateId>
{
    protected readonly Boid boid;

    protected BoidStateBase(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm)
    {
        this.boid = boid;
    }

    protected bool CheckDeathAndThreatTransition()
    {
        if (!boid.Health.IsAlive)
        {
            fsm.ChangeState(BoidStateId.Dead);
            return true;
        }

        if (boid.HasThreatInVision())
        {
            fsm.ChangeState(BoidStateId.Evade);
            return true;
        }

        return false;
    }
}
