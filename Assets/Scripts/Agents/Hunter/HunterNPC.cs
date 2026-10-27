using System;
using UnityEngine;

/// <summary>
/// Controlador principal del NPC Cazador. Administra la FSM basada en enum, parámetros de ataque,
/// generación de POIs mediante POISpawner, blackboard de objetivos con persistencia de LastDamagedTarget,
/// y pipeline de eventos de acción.
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
    public float AttackCooldownRemaining => Mathf.Max(0f, (lastAttackTime + TBA) - Time.time);
    public void ResetAttackCooldown() => lastAttackTime = Time.time;

    [Header("Ruta de Patrullaje")]
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    public Transform[] Waypoints => waypoints;

    [Header("Spawner de POI")]
    [SerializeField] private POISpawner poiSpawner;
    public POISpawner POISpawner => poiSpawner;

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
    public Boid LastDamagedTarget { get; set; }

    // Eventos de acción (trazabilidad y feedback)
    public event Action<string, Boid> OnActionExecuted;
    public void NotifyAction(string actionName, Boid target) => OnActionExecuted?.Invoke(actionName, target);

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

        if (poiSpawner == null)
        {
            poiSpawner = GetComponent<POISpawner>();
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

    /// <summary>
    /// Configura el color visual del Cazador para reflejar su acción en curso:
    /// Verde (Patrulla waypoints), Verde Agua (Persecución/Aproximación), Rojo (Ataque) o Amarillo (Recolección).
    /// </summary>
    public void SetVisualMode(HunterVisualMode mode)
    {
        if (meshRenderers == null) return;

        Color targetColor = mode switch
        {
            HunterVisualMode.Patrol => Color.green,
            HunterVisualMode.Pursuit => new Color(0.1f, 0.85f, 0.75f), // Verde agua (Teal)
            HunterVisualMode.Attack => Color.red,
            HunterVisualMode.Gather => Color.yellow,
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

    /// <summary>
    /// Selecciona el objetivo de ataque dando prioridad al último objetivo dañado (R2) si sigue vivo y en visión.
    /// </summary>
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
            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.IsAlive || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;

                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    closest = b;
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Localiza el Boid caído más cercano dentro de la percepción que esté listo para ser recolectado.
    /// </summary>
    public Boid FindCollectableBoid()
    {
        Boid closest = null;
        float minSqr = float.MaxValue;

        if (sensor != null)
        {
            var boids = sensor.Boids;
            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.isDead || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;

                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    closest = b;
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Determina si un Boid se encuentra dentro del rango de percepción del Cazador.
    /// </summary>
    public bool IsInVision(Boid boid)
    {
        if (boid == null || !boid.gameObject.activeInHierarchy) return false;
        if (sensor != null)
        {
            var list = sensor.Boids;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == boid) return true;
            }
        }

        Vector3 diff = boid.transform.position - transform.position;
        diff.y = 0f;
        return diff.sqrMagnitude <= (visionRadius * visionRadius);
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
