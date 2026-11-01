using UnityEngine;

public class GatherState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly GatherData data;
    private float gatherTimer = 0f;

    public float Progress => data.duration > 0f ? Mathf.Clamp01(gatherTimer / data.duration) : 1f;

    public GatherState(StateMachine<HunterStateId> fsm, HunterNPC hunter, GatherData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        gatherTimer = 0f;
        Boid target = hunter.CurrentTarget;

        if (target == null || !target.isDead || target.isRespawning)
        {
            target = hunter.FindCollectableBoid();
            hunter.CurrentTarget = target;
        }

        if (target == null || !target.isDead || target.isRespawning)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Search);
        }
    }

    public override void Tick()
    {
        if (hunter.TryConsumePendingCapture())
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Dead);
            return;
        }

        if (hunter.ShouldFleeFromWolf())
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Flee);
            return;
        }

        Boid target = hunter.CurrentTarget;
        if (target == null || !target.isDead || target.isRespawning || !target.gameObject.activeInHierarchy)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Search);
            return;
        }

        hunter.SetVisualMode(HunterVisualMode.Gather);

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);
        float cancelDistance = data.arrivalDistance * 1.8f;

        if (distance > data.arrivalDistance)
        {
            if (distance > cancelDistance)
            {
                gatherTimer = 0f;
            }

            if (hunter.NavAgent != null)
            {
                hunter.NavAgent.NavigateToPosition(target.transform.position, useArriveAtEnd: true);
            }

            if (gatherTimer <= 0f) return;
        }

        hunter.NavAgent?.StopNavigation();
        hunter.Agent.Stop();
        if (hunter.Agent.Velocity.sqrMagnitude < 1.0f)
        {
            hunter.Agent.ResetMotion();
        }

        Vector3 toTarget = targetFlat - hunterFlat;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(toTarget.normalized);
            hunter.transform.rotation = Quaternion.Slerp(
                hunter.transform.rotation,
                lookRotation,
                hunter.Agent.RotationSpeed * Time.deltaTime);
        }

        gatherTimer += Time.deltaTime;

        if (gatherTimer >= data.duration)
        {
            target.Collect();
            hunter.IncrementGathers();
            hunter.NotifyAction("GatherCompleted", target);

            if (hunter.LastDamagedTarget == target)
            {
                hunter.LastDamagedTarget = null;
            }

            hunter.CurrentTarget = null;

            if (hunter.HasCompletedRequiredGathers)
            {
                fsm.ChangeState(HunterStateId.Exit);
            }
            else
            {
                fsm.ChangeState(HunterStateId.Search);
            }
        }
    }

    public override void Exit()
    {
        gatherTimer = 0f;
        hunter.NavAgent?.StopNavigation();
    }
}
