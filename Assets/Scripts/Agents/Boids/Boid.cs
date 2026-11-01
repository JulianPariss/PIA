using System;
using UnityEngine;

public enum BoidLifeState
{
    Alive,
    Dead,
    Collected
}

[RequireComponent(typeof(Health), typeof(FlockingBehaviour), typeof(BoidRespawner))]
[RequireComponent(typeof(BoidVisuals))]
public class Boid : SteeringAgent
{
    [Header("Referencias")]
    [SerializeField] private Health health;
    [SerializeField] private FlockingBehaviour flocking;
    [SerializeField] private BoidRespawner respawner;
    [SerializeField] private AgentSensor sensor;
    [SerializeField] private SimulationBounds bounds;

    [Header("POI y Pesos")]
    [SerializeField] private float poiInteractionRadius = 2.8f;
    [SerializeField] private float poiSlowingRadius = 5f;
    [SerializeField] private float poiDamageAmount = 15f;
    [SerializeField] private float poiDamageInterval = 1f;
    [SerializeField] private float evadeWeight = 2.5f;
    [SerializeField] private float poiWeight = 2f;
    [SerializeField] private float boundaryWeight = 1.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float poiSeparationDamping = 0.6f;

    private BoidLifeState lifeState = BoidLifeState.Alive;
    private float lastPOIDamageTime;

    public bool IsEvading { get; private set; }
    public BoidLifeState LifeState => lifeState;
    public bool IsAlive => lifeState == BoidLifeState.Alive;
    public bool IsCollectable => lifeState == BoidLifeState.Dead;
    public bool IsDead => !IsAlive;
    public bool IsRespawning => lifeState == BoidLifeState.Collected;
    public bool isDead => IsDead;
    public bool isRespawning => IsRespawning;
    public Health Health => health;
    public AgentSensor Sensor => sensor;

    public event Action<Boid> OnCollected;

    protected override void Awake()
    {
        base.Awake();
        health = health ? health : GetComponent<Health>();
        flocking = flocking ? flocking : GetComponent<FlockingBehaviour>();
        respawner = respawner ? respawner : GetComponent<BoidRespawner>();
        sensor = sensor ? sensor : GetComponentInChildren<AgentSensor>();
        bounds = bounds ? bounds : SimulationBounds.Instance;

        if (sensor && flocking)
        {
            sensor.SetRadius(Mathf.Max(flocking.AlignmentRadius, flocking.CohesionRadius));
        }

        if (!TryGetComponent<WorldHealthBar>(out _))
        {
            gameObject.AddComponent<WorldHealthBar>();
        }

        health.OnDied += Die;
    }

    protected virtual void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= Die;
        }
    }

    protected virtual void Start()
    {
        if (bounds == null)
        {
            bounds = SimulationBounds.Instance;
        }

        if (Velocity.sqrMagnitude < 0.001f)
        {
            velocity = transform.forward * (MaxSpeed * 0.5f);
        }
    }

    protected override void Update()
    {
        if (!IsAlive) return;

        ApplyForce(ComputeSteering());
        base.Update();
    }

    protected virtual void LateUpdate()
    {
        if (!IsAlive) return;

        if (bounds != null)
        {
            bounds.EnforceContainment(this);
        }
    }

    private Vector3 ComputeSteering()
    {
        bool isIsolated = (sensor == null || sensor.Boids.Count == 0);
        Vector3 bound = bounds ? bounds.ComputeContainmentForce(this, boundaryWeight, isIsolated) : Vector3.zero;

        if (TryEvade(out Vector3 evade))
        {
            IsEvading = true;
            return evade + bound * 3.5f;
        }

        IsEvading = false;

        if (TrySeekPOI(out Vector3 poi))
        {
            return poi + bound;
        }

        Vector3 flock = flocking.ComputeFlocking(this, sensor ? sensor.Boids : null);

        return flock + bound;
    }

    private bool TryEvade(out Vector3 force)
    {
        force = Vector3.zero;
        if (sensor == null || sensor.Hunter == null || sensor.Hunter.IsDead) return false;

        HunterNPC hunter = sensor.Hunter;
        Vector3 hPos = hunter.transform.position;
        hPos.y = transform.position.y;
        Vector3 hVel = hunter.Agent ? hunter.Agent.Velocity : Vector3.zero;

        force = (SteeringBehaviors.Evade(this, hPos, hVel) * evadeWeight) +
                flocking.ComputeSeparationOnly(this, sensor.Boids);

        return true;
    }

    private bool TrySeekPOI(out Vector3 force)
    {
        force = Vector3.zero;
        PointOfInterest poi = GetNearestLocalPOI();
        if (poi == null) return false;

        Vector3 toPOI = poi.transform.position - transform.position;
        toPOI.y = 0f;

        if (toPOI.sqrMagnitude <= poiInteractionRadius * poiInteractionRadius)
        {
            Stop();

            if (toPOI.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPOI.normalized), RotationSpeed * Time.deltaTime);
            }

            if (Time.time >= lastPOIDamageTime + poiDamageInterval)
            {
                poi.TakeDamage(poiDamageAmount);
                lastPOIDamageTime = Time.time;
            }

            return true;
        }

        force = SteeringBehaviors.Arrive(this, poi.transform.position, poiSlowingRadius) * poiWeight +
                flocking.ComputeSeparationOnly(this, sensor.Boids) * poiSeparationDamping;

        return true;
    }

    private PointOfInterest GetNearestLocalPOI()
    {
        if (sensor == null) return null;

        PointOfInterest nearest = null;
        float minSqr = float.MaxValue;
        Vector3 pos = transform.position;
        var pois = sensor.POIs;

        for (int i = 0; i < pois.Count; i++)
        {
            PointOfInterest p = pois[i];
            if (p == null || (p.Health != null && !p.Health.IsAlive)) continue;

            float sqr = (p.transform.position - pos).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr = sqr;
                nearest = p;
            }
        }

        return nearest;
    }

    public void TakeDamage(float amount)
    {
        health.TakeDamage(amount);
    }

    public void StartDelayedRespawn()
    {
        Collect();
    }

    private void Die()
    {
        lifeState = BoidLifeState.Dead;
        ResetMotion();
    }

    public void Collect()
    {
        if (lifeState == BoidLifeState.Collected) return;

        lifeState = BoidLifeState.Collected;
        ResetMotion();
        sensor?.Clear();
        OnCollected?.Invoke(this);
        respawner.BeginRespawn(OnRespawnCompleted);
    }

    public void ResetState()
    {
        lifeState = BoidLifeState.Alive;
        health.ResetHealth();
        ResetMotion();
    }

    private void OnRespawnCompleted()
    {
        ResetState();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!IsAlive) return;

        if (IsEvading && sensor != null && sensor.Hunter != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Gizmos.DrawLine(transform.position, sensor.Hunter.transform.position);
        }
    }
}
