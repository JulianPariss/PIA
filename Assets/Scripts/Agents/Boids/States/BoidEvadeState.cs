public class BoidEvadeState : BoidStateBase
{
    public BoidEvadeState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
    {
    }

    public override void Enter()
    {
        boid.SetEvading(true);
    }

    public override void Tick()
    {
        if (!boid.Health.IsAlive)
        {
            fsm.ChangeState(BoidStateId.Dead);
            return;
        }

        if (!boid.HasThreatInVision())
        {
            if (boid.TryGetTargetPOI(out PointOfInterest poi))
            {
                if (boid.IsWithinConsumeRadius(poi))
                {
                    fsm.ChangeState(BoidStateId.Consume);
                }
                else
                {
                    fsm.ChangeState(BoidStateId.SeekObject);
                }
            }
            else
            {
                fsm.ChangeState(BoidStateId.Flocking);
            }
            return;
        }

        boid.ApplyEvadeSteering();
    }

    public override void Exit()
    {
        boid.SetEvading(false);
    }
}
