public class BoidConsumeState : BoidStateBase
{
    public BoidConsumeState(StateMachine<BoidStateId> fsm, Boid boid) : base(fsm, boid)
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

        if (!boid.IsWithinConsumeRadius(poi))
        {
            fsm.ChangeState(BoidStateId.SeekObject);
            return;
        }

        boid.ConsumePOI(poi);
    }
}
