using System;
using UnityEngine;
using PIA1.UI;

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

    [Header("Anti-Superposición")]
    [SerializeField] private float minSeparationDistance = 1.15f;
    [SerializeField] private float poiMinDistance = 1.35f;

    private BoidLifeState lifeState = BoidLifeState.Alive;
    private float lastPOIDamageTime;
    private bool _exitedBoundsThisHunt;

    public StateMachine<BoidStateId> FSM { get; private set; }
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
    public FlockingBehaviour Flocking => flocking;
    public BoidRespawner Respawner => respawner;

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
            sensor.SetRadius(flocking.VisionRadius);
        }

        if (!TryGetComponent<WorldHealthBar>(out _))
        {
            gameObject.AddComponent<WorldHealthBar>();
        }

        if (!TryGetComponent<BoidStateLabel>(out _))
        {
            gameObject.AddComponent<BoidStateLabel>();
        }

        health.OnDied += Die;
        InitializeFSM();
    }

    private void InitializeFSM()
    {
        FSM = new StateMachine<BoidStateId>();
        FSM.Register(BoidStateId.Flocking, new BoidFlockingState(FSM, this));
        FSM.Register(BoidStateId.SeekObject, new BoidSeekObjectState(FSM, this));
        FSM.Register(BoidStateId.Consume, new BoidConsumeState(FSM, this));
        FSM.Register(BoidStateId.Evade, new BoidEvadeState(FSM, this));
        FSM.Register(BoidStateId.Dead, new BoidDeadState(FSM, this));
        FSM.Register(BoidStateId.Respawn, new BoidRespawnState(FSM, this));
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

        if (FSM != null && !FSM.HasState)
        {
            FSM.ChangeState(BoidStateId.Flocking);
        }
    }

    protected override void Update()
    {
        FSM?.Tick();

        if (!IsAlive) return;

        base.Update();
    }

    protected virtual void LateUpdate()
    {
        if (!IsAlive) return;

        ResolveAntiOverlap();

        if (bounds != null)
        {
            bounds.EnforceContainment(this);
        }
    }

    public void SetEvading(bool evading)
    {
        IsEvading = evading;
    }

    public bool HasThreatInVision()
    {
        return sensor != null && sensor.Hunter != null && !sensor.Hunter.IsDead;
    }

    public bool TryGetTargetPOI(out PointOfInterest poi)
    {
        poi = GetNearestLocalPOI();
        return poi != null;
    }

    public bool IsWithinConsumeRadius(PointOfInterest poi)
    {
        if (poi == null) return false;

        Vector3 toPOI = poi.transform.position - transform.position;
        toPOI.y = 0f;
        return toPOI.sqrMagnitude <= poiInteractionRadius * poiInteractionRadius;
    }

    public void ApplyFlockingSteering()
    {
        bool isIsolated = sensor == null || sensor.Boids.Count == 0;
        Vector3 bound = bounds ? bounds.ComputeContainmentForce(this, boundaryWeight, isIsolated) : Vector3.zero;
        Vector3 flock = flocking.ComputeFlocking(this, sensor ? sensor.Boids : null);
        ApplyForce(flock + bound);
    }

    public void ApplyEvadeSteering()
    {
        if (!HasThreatInVision()) return;

        Vector3 bound = bounds ? bounds.ComputeContainmentForce(this, boundaryWeight, false) : Vector3.zero;

        HunterNPC hunter = sensor.Hunter;
        Vector3 hPos = hunter.transform.position;
        hPos.y = transform.position.y;
        Vector3 hVel = hunter.Agent ? hunter.Agent.Velocity : Vector3.zero;

        Vector3 force = (SteeringBehaviors.Evade(this, hPos, hVel) * evadeWeight) +
                        flocking.ComputeSeparationOnly(this, sensor.Boids);

        ApplyForce(force + bound * 3.5f);
    }

    public void ApplySeekPOISteering(PointOfInterest poi)
    {
        if (poi == null) return;

        bool isIsolated = sensor == null || sensor.Boids.Count == 0;
        Vector3 bound = bounds ? bounds.ComputeContainmentForce(this, boundaryWeight, isIsolated) : Vector3.zero;

        Vector3 force = SteeringBehaviors.Arrive(this, poi.transform.position, poiSlowingRadius) * poiWeight +
                        flocking.ComputeSeparationOnly(this, sensor ? sensor.Boids : null) * poiSeparationDamping;

        ApplyForce(force + bound);
    }

    public void ConsumePOI(PointOfInterest poi)
    {
        if (poi == null) return;

        Vector3 toPOI = poi.transform.position - transform.position;
        toPOI.y = 0f;
        float distSqr = toPOI.sqrMagnitude;

        Stop();

        if (sensor != null && flocking != null)
        {
            ApplyForce(flocking.ComputeSeparationOnly(this, sensor.Boids) * poiSeparationDamping);
        }

        if (distSqr < poiMinDistance * poiMinDistance && distSqr > 0.0001f)
        {
            Vector3 pushOut = (-toPOI / Mathf.Sqrt(distSqr)) * MaxSteering * 0.5f;
            ApplyForce(pushOut);
        }

        if (distSqr > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toPOI.normalized),
                RotationSpeed * Time.deltaTime);
        }

        if (Time.time >= lastPOIDamageTime + poiDamageInterval)
        {
            poi.TakeDamage(poiDamageAmount);
            lastPOIDamageTime = Time.time;
        }
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

    private void ResolveAntiOverlap()
    {
        if (sensor == null) return;

        Vector3 pos = transform.position;
        float minBoidSqr = minSeparationDistance * minSeparationDistance;
        var neighbors = sensor.Boids;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Boid other = neighbors[i];
            if (other == null || other == this || !other.IsAlive) continue;

            Vector3 diff = pos - other.transform.position;
            diff.y = 0f;
            float sqr = diff.sqrMagnitude;

            if (sqr < minBoidSqr && sqr > 0.00001f)
            {
                float dist = Mathf.Sqrt(sqr);
                float overlap = (minSeparationDistance - dist) * 0.5f;
                Vector3 correction = (diff / dist) * overlap;
                ApplySafePositionOffset(correction);
                pos = transform.position;
            }
        }

        if (TryGetTargetPOI(out PointOfInterest poi))
        {
            Vector3 diffPOI = pos - poi.transform.position;
            diffPOI.y = 0f;
            float sqrPOI = diffPOI.sqrMagnitude;

            if (sqrPOI < poiMinDistance * poiMinDistance && sqrPOI > 0.00001f)
            {
                float distPOI = Mathf.Sqrt(sqrPOI);
                Vector3 correction = (diffPOI / distPOI) * (poiMinDistance - distPOI);
                ApplySafePositionOffset(correction);
            }
        }
    }

    private void ApplySafePositionOffset(Vector3 offset)
    {
        offset.y = 0f;
        Vector3 candidate = transform.position + offset;

        if (obstacleMask != 0 &&
            Physics.CheckSphere(candidate, bodyRadius * 0.85f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        transform.position = candidate;
    }

    public void TakeDamage(float amount)
    {
        health.TakeDamage(amount);
    }

    public void TakeHits(int hits)
    {
        if (!IsAlive || hits <= 0) return;

        float damagePerHit = health.Max * 0.5f;
        health.TakeDamage(damagePerHit * hits);
    }

    public void StartDelayedRespawn()
    {
        Collect();
    }

    private void Die()
    {
        lifeState = BoidLifeState.Dead;
        ResetMotion();
        FSM?.ChangeState(BoidStateId.Dead);
    }

    public void Collect()
    {
        if (lifeState == BoidLifeState.Collected) return;

        lifeState = BoidLifeState.Collected;
        ResetMotion();
        sensor?.Clear();
        OnCollected?.Invoke(this);
        FSM?.ChangeState(BoidStateId.Respawn);
    }

    public void StartRespawnSequence()
    {
        ResetMotion();
        sensor?.Clear();
        respawner.BeginRespawn(OnRespawnCompleted);
    }

    public void ResetState()
    {
        lifeState = BoidLifeState.Alive;
        _exitedBoundsThisHunt = false;
        health.ResetHealth();
        ResetMotion();
        FSM?.ChangeState(BoidStateId.Flocking);
    }

    private void OnRespawnCompleted()
    {
        ResetState();
    }

    public void NotifyExitedBounds()
    {
        _exitedBoundsThisHunt = true;
    }

    public void ClearBoundsExitFlag()
    {
        _exitedBoundsThisHunt = false;
    }

    public bool ConsumeBoundsExitFlag()
    {
        if (!_exitedBoundsThisHunt) return false;
        _exitedBoundsThisHunt = false;
        return true;
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
