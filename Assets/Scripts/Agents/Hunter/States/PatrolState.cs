using UnityEngine;

/// <summary>
/// Estado de patrullaje para el Cazador. Recorre waypoints según el modo configurado y suelta POIs periódicamente.
/// Utiliza AgentSensor para detectar boids vivos o muertos y transiciona por enum a Attack o Gather.
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

        CheckSensorTargets(out Boid closestLiving, out Boid closestDead);

        // Prioridad 1: Si hay un boid muerto para recolectar
        if (closestDead != null)
        {
            hunter.CurrentTarget = closestDead;
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        // Prioridad 2: Si el TBA está listo y hay un boid vivo
        if (closestLiving != null && hunter.IsTBAReady)
        {
            hunter.CurrentTarget = closestLiving;
            fsm.ChangeState(HunterStateId.Attack);
            return;
        }

        FollowWaypoints();
    }

    private void UpdatePOISpawn()
    {
        hunter.TickPOITimer(Time.deltaTime);
        if (!hunter.CanSpawnPOIByTimer) return;

        hunter.ResetPOITimer();
        if (PointOfInterest.CanSpawnPOI)
        {
            Vector3 spawnPos = hunter.transform.position - (hunter.transform.forward * data.poiDropOffset);
            hunter.SpawnPOI(spawnPos);
        }
    }

    private void CheckSensorTargets(out Boid closestLiving, out Boid closestDead)
    {
        closestLiving = null;
        closestDead = null;

        if (hunter.Sensor == null) return;

        float minLivingDistSqr = float.MaxValue;
        float minDeadDistSqr = float.MaxValue;
        var boids = hunter.Sensor.Boids;

        for (int i = 0; i < boids.Count; i++)
        {
            Boid boid = boids[i];
            if (boid == null || boid.isRespawning) continue;

            Vector3 diff = boid.transform.position - hunter.transform.position;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            if (boid.IsAlive && distSqr < minLivingDistSqr)
            {
                minLivingDistSqr = distSqr;
                closestLiving = boid;
            }
            else if (boid.isDead && distSqr < minDeadDistSqr)
            {
                minDeadDistSqr = distSqr;
                closestDead = boid;
            }
        }
    }

    private void FollowWaypoints()
    {
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
