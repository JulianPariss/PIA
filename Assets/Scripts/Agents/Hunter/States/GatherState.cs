/// <summary>
/// Estado de recolección para el Cazador. Navega en el plano XZ hacia un Boid muerto y lo recolecta al cabo de un tiempo determinado.
/// </summary>
using UnityEngine;

public class GatherState : IState
{
    private readonly HunterNPC hunter;
    private readonly Boid target;

    public float gatherDuration = 3f;
    public float gatherArrivalDistance = 2f;

    private float gatherTimer = 0f;

    public GatherState(HunterNPC hunter, Boid target, float gatherDuration = 3f, float gatherArrivalDistance = 2f)
    {
        this.hunter = hunter;
        this.target = target;
        this.gatherDuration = gatherDuration;
        this.gatherArrivalDistance = gatherArrivalDistance;
    }

    public void Enter()
    {
        gatherTimer = 0f;
    }

    public void UpdateState()
    {
        if (target == null || !target.isDead)
        {
            hunter.FSM.ChangeState(new PatrolState(hunter), force: true);
            return;
        }

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        if (distance > gatherArrivalDistance)
        {
            gatherTimer = 0f;
            Vector3 targetMovePos = target.transform.position;
            targetMovePos.y = hunter.transform.position.y;
            Vector3 arriveForce = SteeringBehaviors.Arrive(hunter.Agent, targetMovePos, gatherArrivalDistance * 1.5f);
            arriveForce.y = 0f;
            hunter.Agent.ApplyForce(arriveForce);
            return;
        }

        hunter.Agent.velocity = Vector3.MoveTowards(hunter.Agent.velocity, Vector3.zero, hunter.Agent.maxSpeed * 3f * Time.deltaTime);
        gatherTimer += Time.deltaTime;

        if (gatherTimer >= gatherDuration)
        {
            target.StartDelayedRespawn();
            hunter.FSM.ChangeState(new PatrolState(hunter), force: true);
        }
    }

    public void Exit()
    {
        gatherTimer = 0f;
    }
}
