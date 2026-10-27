using System.Collections.Generic;
using UnityEngine;

public class FlockingBehaviour : MonoBehaviour
{
    [Header("Radios de Interacción")]
    [SerializeField] private float separationRadius = 2.5f;
    [SerializeField] private float alignmentRadius = 6f;
    [SerializeField] private float cohesionRadius = 6f;

    [Header("Pesos Ponderados")]
    [Range(0f, 3f)]
    [SerializeField] private float alignmentWeight = 1f;

    [Range(0f, 3f)]
    [SerializeField] private float cohesionWeight = 1f;

    [Range(0f, 3f)]
    [SerializeField] private float separationWeight = 1.8f;

    public float SeparationRadius => separationRadius;
    public float AlignmentRadius => alignmentRadius;
    public float CohesionRadius => cohesionRadius;

    private void OnValidate()
    {
        if (separationRadius >= alignmentRadius)
        {
            separationRadius = Mathf.Max(0.5f, alignmentRadius - 0.5f);
        }
        if (separationRadius >= cohesionRadius)
        {
            separationRadius = Mathf.Max(0.5f, cohesionRadius - 0.5f);
        }
    }

    public Vector3 ComputeFlocking(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        if (self == null || neighbors == null || neighbors.Count == 0) return Vector3.zero;

        Vector3 separationForce = Separation(self, neighbors) * separationWeight;
        Vector3 alignmentForce = Alignment(self, neighbors) * alignmentWeight;
        Vector3 cohesionForce = Cohesion(self, neighbors) * cohesionWeight;

        return separationForce + alignmentForce + cohesionForce;
    }

    public Vector3 ComputeSeparationOnly(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        if (self == null || neighbors == null || neighbors.Count == 0) return Vector3.zero;
        return Separation(self, neighbors) * separationWeight;
    }

    private Vector3 Separation(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        Vector3 repulsion = Vector3.zero;
        int count = 0;
        float sqrSeparation = separationRadius * separationRadius;
        Vector3 selfPos = self.transform.position;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Boid other = neighbors[i];
            if (other == null || other == self || !other.IsAlive) continue;

            Vector3 diff = selfPos - other.transform.position;
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist > 0.0001f && sqrDist <= sqrSeparation)
            {
                float dist = Mathf.Sqrt(sqrDist);
                repulsion += diff / (dist * dist);
                count++;
            }
        }

        if (count == 0 || repulsion.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 desired = SteeringBehaviors.DesiredVelocity(self, repulsion);
        return SteeringBehaviors.Steer(self, desired);
    }

    private Vector3 Alignment(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        Vector3 avgVelocity = Vector3.zero;
        int count = 0;
        float sqrAlignment = alignmentRadius * alignmentRadius;
        Vector3 selfPos = self.transform.position;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Boid other = neighbors[i];
            if (other == null || other == self || !other.IsAlive) continue;

            Vector3 diff = other.transform.position - selfPos;
            diff.y = 0f;

            if (diff.sqrMagnitude <= sqrAlignment)
            {
                avgVelocity += other.Velocity;
                count++;
            }
        }

        if (count == 0 || avgVelocity.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 desired = SteeringBehaviors.DesiredVelocity(self, avgVelocity);
        return SteeringBehaviors.Steer(self, desired);
    }

    private Vector3 Cohesion(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        Vector3 centerOfMass = Vector3.zero;
        int count = 0;
        float sqrCohesion = cohesionRadius * cohesionRadius;
        Vector3 selfPos = self.transform.position;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Boid other = neighbors[i];
            if (other == null || other == self || !other.IsAlive) continue;

            Vector3 diff = other.transform.position - selfPos;
            diff.y = 0f;

            if (diff.sqrMagnitude <= sqrCohesion)
            {
                centerOfMass += other.transform.position;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;

        centerOfMass /= count;
        centerOfMass.y = selfPos.y;
        return SteeringBehaviors.Seek(self, centerOfMass);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(pos, separationRadius);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireSphere(pos, alignmentRadius);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(pos, cohesionRadius);
    }
}
