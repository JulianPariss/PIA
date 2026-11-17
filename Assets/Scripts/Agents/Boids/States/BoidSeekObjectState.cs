public class BoidSeekObjectState : BoidStateBase
{
    public BoidSeekObjectState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
    {
    }

    public override void Tick()
    {
        if (CheckDeathAndThreatTransition()) return;

        if (!boid.TryGetTargetPOI(out PointOfInterest poi))
        {
            fsm.ChangeState(BoidStateId.Flocking);
            return;
        }

        if (boid.IsWithinConsumeRadius(poi))
        {
            fsm.ChangeState(BoidStateId.Consume);
            return;
        }

        boid.ApplySeekPOISteering(poi);
    }
}
