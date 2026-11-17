public class BoidFlockingState : BoidStateBase
{
    public BoidFlockingState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
    {
    }

    public override void Enter()
    {
        boid.SetEvading(false);
    }

    public override void Tick()
    {
        if (CheckDeathAndThreatTransition()) return;

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
            return;
        }

        boid.ApplyFlockingSteering();
    }
}
