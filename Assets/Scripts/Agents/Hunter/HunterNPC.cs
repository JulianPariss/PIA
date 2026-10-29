using System;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SteeringAgent))]
public class HunterNPC : MonoBehaviour
{
    [Header("Parámetros de Ataque")]
    [FormerlySerializedAs("TBA")]
    [SerializeField] private float tba = 1.8f;
    [FormerlySerializedAs("RangeAttackRadius")]
    [SerializeField] private float rangeAttackRadius = 8f;
    [FormerlySerializedAs("MeleeAttackRadius")]
    [SerializeField] private float meleeAttackRadius = 2.5f;
    private float lastAttackTime = 0f;

    public float TBA => tba;
    public float RangeAttackRadius => rangeAttackRadius;
    public float MeleeAttackRadius => meleeAttackRadius;

    public bool IsTBAReady => Time.time >= lastAttackTime + TBA;
    public float AttackCooldownRemaining => Mathf.Max(0f, (lastAttackTime + TBA) - Time.time);
    public void ResetAttackCooldown() => lastAttackTime = Time.time;

    [Header("Ruta de Patrullaje")]
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    public Transform[] Waypoints => waypoints;

    [Header("Spawner de POI")]
    [SerializeField] private POISpawner poiSpawner;
    public POISpawner POISpawner => poiSpawner;

    [Header("Detección y Sensor")]
    [SerializeField] private float visionRadius = 14f;
    [SerializeField] private AgentSensor sensor;
    [SerializeField] private LayerMask obstacleMask = 0;
    [SerializeField] private float eyeHeight = 0.8f;
    public AgentSensor Sensor => sensor;
    public float VisionRadius => visionRadius;
    public LayerMask ObstacleMask => obstacleMask;

    [Header("Configuración de Estados")]
    [SerializeField] private PatrolData patrolData = new PatrolData();
    [SerializeField] private AttackData attackData = new AttackData();
    [SerializeField] private GatherData gatherData = new GatherData();

    public PatrolData PatrolConfig => patrolData;
    public AttackData AttackConfig => attackData;
    public GatherData GatherConfig => gatherData;

    public Boid CurrentTarget { get; set; }
    public Boid LastDamagedTarget { get; set; }

    public event Action<string, Boid> OnActionExecuted;
    public void NotifyAction(string actionName, Boid target) => OnActionExecuted?.Invoke(actionName, target);

    public StateMachine<HunterStateId> FSM { get; private set; }
    public SteeringAgent Agent { get; private set; }

    private int currentWaypointIndex = 0;
    private bool isPatrolReversed = false;
    private Renderer[] meshRenderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public HunterVisualMode CurrentVisualMode { get; private set; } = HunterVisualMode.Patrol;
    public float GatherProgress => (FSM != null && FSM.CurrentState is GatherState gs) ? gs.Progress : 0f;

    protected virtual void Awake()
    {
        Agent = GetComponent<SteeringAgent>();

        if (obstacleMask == 0)
        {
            if (Agent != null && Agent.ObstacleMask != 0)
            {
                obstacleMask = Agent.ObstacleMask;
            }
            else
            {
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0) obstacleMask = 1 << obstacleLayer;
            }
        }

        if (sensor == null)
        {
            sensor = GetComponentInChildren<AgentSensor>();
        }

        if (sensor != null)
        {
            sensor.SetRadius(visionRadius);
        }

        if (poiSpawner == null)
        {
            poiSpawner = GetComponent<POISpawner>();
        }

        meshRenderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();

        if (!TryGetComponent<HunterVisuals>(out _)) gameObject.AddComponent<HunterVisuals>();
        if (!TryGetComponent<HunterStateLabel>(out _)) gameObject.AddComponent<HunterStateLabel>();
        if (FindFirstObjectByType<SimulationHUD>() == null && !TryGetComponent<SimulationHUD>(out _)) gameObject.AddComponent<SimulationHUD>();

        if (!CompareTag("Hunter"))
        {
            try { gameObject.tag = "Hunter"; } catch (Exception) { }
        }

        FSM = new StateMachine<HunterStateId>();
        FSM.Register(HunterStateId.Patrol, new PatrolState(FSM, this, patrolData));
        FSM.Register(HunterStateId.Attack, new AttackState(FSM, this, attackData));
        FSM.Register(HunterStateId.Gather, new GatherState(FSM, this, gatherData));
        FSM.OnStateChanged += HandleStateChanged;
    }

    protected virtual void Start()
    {
        ResetAttackCooldown();
        FSM.ChangeState(HunterStateId.Patrol);
        UpdateStateColor(HunterStateId.Patrol);
    }

    protected virtual void Update()
    {
        FSM.Tick();
    }

    private void OnDestroy()
    {
        if (FSM != null)
        {
            FSM.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(HunterStateId previousState, HunterStateId newState)
    {
        UpdateStateColor(newState);
    }

    private void UpdateStateColor(HunterStateId stateId)
    {
        HunterVisualMode mode = stateId switch
        {
            HunterStateId.Patrol => HunterVisualMode.Patrol,
            HunterStateId.Attack => HunterVisualMode.Pursuit,
            HunterStateId.Gather => HunterVisualMode.Gather,
            _ => HunterVisualMode.Patrol
        };
        SetVisualMode(mode);
    }

    public void SetVisualMode(HunterVisualMode mode)
    {
        if (meshRenderers == null) return;

        Color targetColor = mode switch
        {
            HunterVisualMode.Patrol => Color.green,
            HunterVisualMode.Pursuit => new Color(0.1f, 0.85f, 0.75f),
            HunterVisualMode.Attack => Color.red,
            HunterVisualMode.Gather => Color.yellow,
            _ => Color.white
        };

        CurrentVisualMode = mode;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].sharedMaterial != null)
            {
                meshRenderers[i].GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, targetColor);
                propBlock.SetColor(ColorId, targetColor);
                meshRenderers[i].SetPropertyBlock(propBlock);
            }
        }
    }

    public Boid SelectAttackTarget()
    {
        if (LastDamagedTarget != null && LastDamagedTarget.IsAlive && !LastDamagedTarget.isRespawning && IsInVision(LastDamagedTarget))
        {
            return LastDamagedTarget;
        }

        Boid closest = null;
        float minSqr = float.MaxValue;

        if (sensor != null)
        {
            var boids = sensor.Boids;
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.IsAlive || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;

                if (sqr < minSqr)
                {
                    Vector3 targetCenter = b.transform.position + Vector3.up * 0.5f;
                    if (LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask))
                    {
                        minSqr = sqr;
                        closest = b;
                    }
                }
            }
        }

        return closest;
    }

    public Boid FindCollectableBoid()
    {
        Boid closest = null;
        float minSqr = float.MaxValue;

        if (sensor != null)
        {
            var boids = sensor.Boids;
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.isDead || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;

                if (sqr < minSqr)
                {
                    Vector3 targetCenter = b.transform.position + Vector3.up * 0.5f;
                    if (LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask))
                    {
                        minSqr = sqr;
                        closest = b;
                    }
                }
            }
        }

        return closest;
    }

    public bool IsInVision(Boid boid)
    {
        if (boid == null || !boid.gameObject.activeInHierarchy || boid.isRespawning) return false;

        Vector3 diff = boid.transform.position - transform.position;
        diff.y = 0f;
        if (diff.sqrMagnitude > (visionRadius * visionRadius)) return false;

        if (sensor != null)
        {
            var list = sensor.Boids;
            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == boid)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetCenter = boid.transform.position + Vector3.up * 0.5f;
        return LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask);
    }

    public Transform GetCurrentWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return null;

        int checkedCount = 0;
        while (checkedCount < waypoints.Length)
        {
            if (currentWaypointIndex >= waypoints.Length) currentWaypointIndex = 0;
            if (currentWaypointIndex < 0) currentWaypointIndex = waypoints.Length - 1;

            if (waypoints[currentWaypointIndex] != null)
            {
                return waypoints[currentWaypointIndex];
            }

            AdvanceToNextWaypoint(patrolData.mode);
            checkedCount++;
        }

        return null;
    }

    public void AdvanceToNextWaypoint(PatrolMode mode)
    {
        if (waypoints == null || waypoints.Length <= 1) return;

        if (mode == PatrolMode.Loop)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            if (isPatrolReversed)
            {
                currentWaypointIndex--;
                if (currentWaypointIndex <= 0)
                {
                    currentWaypointIndex = 0;
                    isPatrolReversed = false;
                }
            }
            else
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length - 1)
                {
                    currentWaypointIndex = waypoints.Length - 1;
                    isPatrolReversed = true;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, RangeAttackRadius);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRadius);

        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                int nextIndex = (i + 1) % waypoints.Length;
                if (waypoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                }
            }
        }
    }
}
