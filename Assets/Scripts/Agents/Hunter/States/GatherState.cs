using UnityEngine;

/// <summary>
/// Estado de recolección para el Cazador. Se desplaza mediante Arrive hacia un Boid caído (Blackboard)
/// y permanece detenido en el lugar durante un tiempo determinado antes de recolectarlo (haciéndolo desaparecer).
/// Expone el progreso de recolección y aborta limpiamente si el objetivo deja de estar disponible.
/// </summary>
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
            fsm.ChangeState(HunterStateId.Patrol);
        }
    }

    public override void Tick()
    {
        Boid target = hunter.CurrentTarget;
        if (target == null || !target.isDead || target.isRespawning)
        {
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        if (distance > data.arrivalDistance)
        {
            gatherTimer = 0f;
            Vector3 targetMovePos = target.transform.position;
            targetMovePos.y = hunter.transform.position.y;
            Vector3 arriveForce = SteeringBehaviors.Arrive(hunter.Agent, targetMovePos, data.arrivalDistance * 1.5f);
            arriveForce.y = 0f;
            hunter.Agent.ApplyForce(arriveForce);
            return;
        }

        hunter.Agent.Stop();
        gatherTimer += Time.deltaTime;

        if (gatherTimer >= data.duration)
        {
            target.Collect();
            hunter.NotifyAction("GatherCompleted", target);

            if (hunter.LastDamagedTarget == target)
            {
                hunter.LastDamagedTarget = null;
            }

            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Patrol);
        }
    }

    public override void Exit()
    {
        gatherTimer = 0f;
    }
}
