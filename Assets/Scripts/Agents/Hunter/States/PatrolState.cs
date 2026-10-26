/// <summary>
/// Estado de patrullaje para el Cazador. Recorre waypoints en el plano XZ y genera POIs periódicamente sin obstruir el movimiento.
/// </summary>
using UnityEngine;

public class PatrolState : IState
{
    private readonly HunterNPC hunter;
    private readonly float waypointArrivalDistance;

    public PatrolState(HunterNPC hunter, float waypointArrivalDistance = 1.5f)
    {
        this.hunter = hunter;
        this.waypointArrivalDistance = waypointArrivalDistance;
    }

    public void Enter() { }

    public void UpdateState()
    {
        hunter.poiTimer += Time.deltaTime;
        if (hunter.poiTimer >= hunter.poiSpawnInterval)
        {
            hunter.poiTimer = 0f;
            if (PointOfInterest.CanSpawnPOI)
            {
                Vector3 spawnPos = hunter.transform.position - (hunter.transform.forward * 1.5f);
                hunter.SpawnPOI(spawnPos);
            }
        }

        Collider[] hits = Physics.OverlapSphere(hunter.transform.position, hunter.visionRadius);
        Boid closestLivingBoid = null;
        Boid closestDeadBoid = null;
        float minLivingDistSqr = float.MaxValue;
        float minDeadDistSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Boid boid = hits[i].GetComponent<Boid>() ?? hits[i].GetComponentInParent<Boid>();
            if (boid != null)
            {
                Vector3 toBoid = boid.transform.position - hunter.transform.position;
                toBoid.y = 0f;
                float distSqr = toBoid.sqrMagnitude;

                if (!boid.isDead && !boid.isRespawning)
                {
                    if (distSqr < minLivingDistSqr)
                    {
                        minLivingDistSqr = distSqr;
                        closestLivingBoid = boid;
                    }
                }
                else if (boid.isDead && !boid.isRespawning)
                {
                    if (distSqr < minDeadDistSqr)
                    {
                        minDeadDistSqr = distSqr;
                        closestDeadBoid = boid;
                    }
                }
            }
        }

        if (hunter.FSM.CanChangeState)
        {
            if (closestLivingBoid != null && hunter.IsTBAReady)
            {
                hunter.FSM.ChangeState(new AttackState(hunter, closestLivingBoid));
                return;
            }

            if (closestDeadBoid != null)
            {
                hunter.FSM.ChangeState(new GatherState(hunter, closestDeadBoid));
                return;
            }
        }

        Transform currentWaypoint = hunter.GetCurrentWaypoint();
        if (currentWaypoint != null)
        {
            Vector3 hunterPosFlat = hunter.transform.position;
            Vector3 wpPosFlat = currentWaypoint.position;
            hunterPosFlat.y = 0f;
            wpPosFlat.y = 0f;

            float distanceToWp = Vector3.Distance(hunterPosFlat, wpPosFlat);

            if (distanceToWp <= waypointArrivalDistance)
            {
                hunter.AdvanceToNextWaypoint();
                currentWaypoint = hunter.GetCurrentWaypoint();
            }

            if (currentWaypoint != null)
            {
                Vector3 targetWp = currentWaypoint.position;
                targetWp.y = hunter.transform.position.y;
                Vector3 moveForce = SteeringBehaviors.Seek(hunter.Agent, targetWp);
                moveForce.y = 0f;
                hunter.Agent.ApplyForce(moveForce);
            }
        }
    }

    public void Exit() { }
}
