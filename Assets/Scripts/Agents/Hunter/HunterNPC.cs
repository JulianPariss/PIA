using System;
using UnityEngine;

/// <summary>
/// Controlador principal del NPC Cazador. Administra la FSM basada en enum, parámetros de ataque,
/// generación de POIs, blackboard de objetivos y feedback de color por estado.
/// </summary>
[RequireComponent(typeof(SteeringAgent))]
public class HunterNPC : MonoBehaviour
{
    [Header("Parámetros de Ataque")]
    [SerializeField] public float TBA = 2.5f;
    [SerializeField] public float RangeAttackRadius = 10f;
    [SerializeField] public float MeleeAttackRadius = 2.5f;
    private float lastAttackTime = 0f;

    public bool IsTBAReady => Time.time >= lastAttackTime + TBA;
    public void ResetAttackCooldown() => lastAttackTime = Time.time;

    [Header("Ruta de Patrullaje")]
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    public Transform[] Waypoints => waypoints;

    [Header("Prefabs y Spawn de POI")]
    [SerializeField] private GameObject poiPrefab = null;
    [SerializeField] private GameObject[] poiPrefabs = Array.Empty<GameObject>();
    [SerializeField] private float poiSpawnInterval = 5f;
    private float poiTimer = 0f;

    [Header("Detección y Sensor")]
    [SerializeField] private float visionRadius = 15f;
    [SerializeField] private AgentSensor sensor;
    public AgentSensor Sensor => sensor;
    public float VisionRadius => visionRadius;

    [Header("Configuración de Estados")]
    [SerializeField] private PatrolData patrolData = new PatrolData();
    [SerializeField] private AttackData attackData = new AttackData();
    [SerializeField] private GatherData gatherData = new GatherData();

    public PatrolData PatrolConfig => patrolData;
    public AttackData AttackConfig => attackData;
    public GatherData GatherConfig => gatherData;

    // Blackboard
    public Boid CurrentTarget { get; set; }

    public StateMachine<HunterStateId> FSM { get; private set; }
    public SteeringAgent Agent { get; private set; }

    private int currentWaypointIndex = 0;
    private bool isPatrolReversed = false;
    private Renderer[] meshRenderers;

    protected virtual void Awake()
    {
        Agent = GetComponent<SteeringAgent>();

        if (sensor == null)
        {
            sensor = GetComponentInChildren<AgentSensor>();
        }
        if (sensor != null)
        {
            sensor.SetRadius(visionRadius);
        }

        meshRenderers = GetComponentsInChildren<Renderer>();

        if (!CompareTag("Hunter"))
        {
            try { gameObject.tag = "Hunter"; } catch (Exception) { }
        }

        // FSM: Registración única de estados (cero allocations en runtime)
        FSM = new StateMachine<HunterStateId>();
        FSM.Register(HunterStateId.Patrol, new PatrolState(FSM, this, patrolData));
        FSM.Register(HunterStateId.Attack, new AttackState(FSM, this, attackData));
        FSM.Register(HunterStateId.Gather, new GatherState(FSM, this, gatherData));
        FSM.OnStateChanged += HandleStateChanged;
    }

    protected virtual void Start()
    {
        ResetAttackCooldown();
        poiTimer = poiSpawnInterval * 0.6f;

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
        if (meshRenderers == null) return;

        Color targetColor = stateId switch
        {
            HunterStateId.Patrol => Color.green,
            HunterStateId.Attack => Color.red,
            HunterStateId.Gather => Color.yellow,
            _ => Color.white
        };

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].material != null)
            {
                meshRenderers[i].material.color = targetColor;
            }
        }
    }

    public void TickPOITimer(float deltaTime)
    {
        poiTimer += deltaTime;
    }

    public bool CanSpawnPOIByTimer => poiTimer >= poiSpawnInterval;

    public void ResetPOITimer()
    {
        poiTimer = 0f;
    }

    public GameObject SpawnPOI(Vector3 position)
    {
        if (!PointOfInterest.CanSpawnPOI) return null;

        GameObject prefabToInstantiate = poiPrefab;
        if (prefabToInstantiate == null && poiPrefabs != null && poiPrefabs.Length > 0)
        {
            prefabToInstantiate = poiPrefabs[UnityEngine.Random.Range(0, poiPrefabs.Length)];
        }

        if (prefabToInstantiate == null) return null;

        return Instantiate(prefabToInstantiate, position, Quaternion.identity);
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
        else // PingPong
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
