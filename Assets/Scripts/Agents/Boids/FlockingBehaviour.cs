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

    [Header("Gizmos de Flocking y Percepción")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showOnlyWhenSelected = false;
    [SerializeField] private Color separationColor = new Color(1f, 0.25f, 0.25f, 0.7f);
    [SerializeField] private Color alignmentColor = new Color(0.25f, 0.65f, 1f, 0.65f);
    [SerializeField] private Color cohesionColor = new Color(0.25f, 1f, 0.45f, 0.55f);
    [SerializeField] private Color visionColor = new Color(1f, 0.92f, 0.016f, 0.4f);

    private AgentSensor cachedSensor;

    public float SeparationRadius => separationRadius;
    public float AlignmentRadius => alignmentRadius;
    public float CohesionRadius => cohesionRadius;
    public bool ShowGizmos { get => showGizmos; set => showGizmos = value; }
    public bool ShowOnlyWhenSelected { get => showOnlyWhenSelected; set => showOnlyWhenSelected = value; }

    private void Awake()
    {
        cachedSensor = GetComponentInChildren<AgentSensor>();
    }

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
        if (self == null || neighbors == null || neighbors.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 separationForce = Separation(self, neighbors) * separationWeight;
        Vector3 alignmentForce = Alignment(self, neighbors) * alignmentWeight;
        Vector3 cohesionForce = Cohesion(self, neighbors) * cohesionWeight;

        return separationForce + alignmentForce + cohesionForce;
    }

    public Vector3 ComputeSeparationOnly(SteeringAgent self, IReadOnlyList<Boid> neighbors)
    {
        if (self == null || neighbors == null || neighbors.Count == 0)
        {
            return Vector3.zero;
        }

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
            if (other == null || other == self || !other.IsAlive)
            {
                continue;
            }

            Vector3 diff = selfPos - other.transform.position;
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist > 0.0001f && sqrDist <= sqrSeparation)
            {
                repulsion += diff / sqrDist;
                count++;
            }
        }

        if (count == 0 || repulsion.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

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
            if (other == null || other == self || !other.IsAlive)
            {
                continue;
            }

            Vector3 diff = other.transform.position - selfPos;
            diff.y = 0f;

            if (diff.sqrMagnitude <= sqrAlignment)
            {
                avgVelocity += other.Velocity;
                count++;
            }
        }

        if (count == 0 || avgVelocity.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

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
            if (other == null || other == self || !other.IsAlive)
            {
                continue;
            }

            Vector3 diff = other.transform.position - selfPos;
            diff.y = 0f;

            if (diff.sqrMagnitude <= sqrCohesion)
            {
                centerOfMass += other.transform.position;
                count++;
            }
        }

        if (count == 0)
        {
            return Vector3.zero;
        }

        centerOfMass /= count;
        centerOfMass.y = selfPos.y;
        return SteeringBehaviors.Seek(self, centerOfMass);
    }

    private void OnDrawGizmos()
    {
        if (showGizmos && !showOnlyWhenSelected)
        {
            DrawFlockingGizmosInternal();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos && showOnlyWhenSelected)
        {
            DrawFlockingGizmosInternal();
        }
    }

    private void DrawFlockingGizmosInternal()
    {
        Vector3 pos = transform.position;

        Gizmos.color = separationColor;
        Gizmos.DrawWireSphere(pos, separationRadius);

        Gizmos.color = alignmentColor;
        Gizmos.DrawWireSphere(pos, alignmentRadius);

        Gizmos.color = cohesionColor;
        Gizmos.DrawWireSphere(pos, cohesionRadius);

        float maxFlock = Mathf.Max(alignmentRadius, cohesionRadius);
        if (cachedSensor == null)
        {
            cachedSensor = GetComponentInChildren<AgentSensor>();
        }
        float visRadius = cachedSensor != null ? cachedSensor.Radius : maxFlock;

        Gizmos.color = visionColor;
        Gizmos.DrawWireSphere(pos, visRadius);
    }
}
