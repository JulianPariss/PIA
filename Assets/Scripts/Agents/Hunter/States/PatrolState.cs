using UnityEngine;

public class PatrolState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly PatrolData data;

    public PatrolState(StateMachine<HunterStateId> fsm, HunterNPC hunter, PatrolData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        hunter.CurrentTarget = null;
    }

    public override void Tick()
    {
        UpdatePOISpawn();

        Boid deadBoid = hunter.FindCollectableBoid();
        if (deadBoid != null)
        {
            hunter.CurrentTarget = deadBoid;
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        if (hunter.IsTBAReady)
        {
            Boid attackTarget = hunter.SelectAttackTarget();
            if (attackTarget != null)
            {
                hunter.CurrentTarget = attackTarget;
                fsm.ChangeState(HunterStateId.Attack);
                return;
            }
        }

        if (data.pursuitWhileCooldown && hunter.LastDamagedTarget != null &&
            hunter.LastDamagedTarget.IsAlive && !hunter.LastDamagedTarget.isRespawning &&
            hunter.IsInVision(hunter.LastDamagedTarget))
        {
            MaintainPursuit(hunter.LastDamagedTarget);
            return;
        }

        FollowWaypoints();
    }

    private void UpdatePOISpawn()
    {
        if (hunter.POISpawner != null)
        {
            Vector3 spawnPos = hunter.transform.position - (hunter.transform.forward * data.poiDropOffset);
            hunter.POISpawner.Tick(Time.deltaTime, spawnPos);
        }
    }

    private void MaintainPursuit(Boid target)
    {
        hunter.SetVisualMode(HunterVisualMode.Pursuit);
        Vector3 targetPos = target.transform.position;
        targetPos.y = hunter.transform.position.y;
        Vector3 seekForce = SteeringBehaviors.Seek(hunter.Agent, targetPos);
        seekForce.y = 0f;
        hunter.Agent.ApplyForce(seekForce * data.pursuitDamping);
    }

    private void FollowWaypoints()
    {
        hunter.SetVisualMode(HunterVisualMode.Patrol);
        Transform currentWp = hunter.GetCurrentWaypoint();
        if (currentWp == null) return;

        Vector3 hunterPosFlat = hunter.transform.position;
        Vector3 wpPosFlat = currentWp.position;
        hunterPosFlat.y = 0f;
        wpPosFlat.y = 0f;

        if (Vector3.Distance(hunterPosFlat, wpPosFlat) <= data.arrivalDistance)
        {
            hunter.AdvanceToNextWaypoint(data.mode);
            currentWp = hunter.GetCurrentWaypoint();
        }

        if (currentWp != null)
        {
            Vector3 targetPos = currentWp.position;
            targetPos.y = hunter.transform.position.y;
            Vector3 seekForce = SteeringBehaviors.Seek(hunter.Agent, targetPos);
            seekForce.y = 0f;
            hunter.Agent.ApplyForce(seekForce);
        }
    }
}
