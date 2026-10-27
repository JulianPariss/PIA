using UnityEngine;

/// <summary>
/// Estado de patrullaje para el Cazador. Gestiona la generación de POIs mediante POISpawner,
/// prioriza la recolección de presas caídas (D1) y la reanudación del ataque sobre presas heridas (D2).
/// Recorre la ruta de waypoints según el modo configurado (Loop o PingPong).
/// </summary>
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

        // Prioridad 1 (D1): Recolectar presa caída observable
        Boid deadBoid = hunter.FindCollectableBoid();
        if (deadBoid != null)
        {
            hunter.CurrentTarget = deadBoid;
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        // Prioridad 2: Atacar si el TBA ha transcurrido y hay presa viva
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

        // Decisión D2: Persecución de mantenimiento si el TBA está en enfriamiento pero la presa sigue visible
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
        hunter.Agent.ApplyForce(seekForce * 0.8f);
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
