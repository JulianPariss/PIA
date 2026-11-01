using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using PIA1.Agents.Wolves;
using PIA1.Navigation;
using PIA1.Sensors;
using PIA1.UI;
using PIA1.World;

[RequireComponent(typeof(SteeringAgent))]
[RequireComponent(typeof(NavigationAgent))]
public class HunterNPC : MonoBehaviour
{
    public static HunterNPC Instance { get; private set; }

    [FormerlySerializedAs("RequiredGathers")]
    [SerializeField] private int requiredGathers = 3;

    [FormerlySerializedAs("RespawnTime")]
    [SerializeField] private float respawnTime = 5f;

    [FormerlySerializedAs("SafeZoneWaitTime")]
    [SerializeField] private float safeZoneWaitTime = 3.5f;

    [SerializeField] private bool spawnInInitialZoneOnStart = true;

    [FormerlySerializedAs("TBA")]
    [SerializeField] private float tba = 1.8f;

    [FormerlySerializedAs("RangeAttackRadius")]
    [SerializeField] private float rangeAttackRadius = 8f;

    [FormerlySerializedAs("MeleeAttackRadius")]
    [SerializeField] private float meleeAttackRadius = 2.5f;

    private float lastAttackTime = -999f;

    [SerializeField] private POISpawner poiSpawner;

    [FormerlySerializedAs("VisionRadius")]
    [SerializeField] private float visionRadius = 14f;
    [SerializeField] private AgentSensor sensor;
    [SerializeField] private LayerMask obstacleMask = 0;
    [SerializeField] private float eyeHeight = 0.8f;

    [FormerlySerializedAs("patrolData")]
    [SerializeField] private PatrolData searchData = new PatrolData();
    [SerializeField] private AttackData attackData = new AttackData();
    [SerializeField] private GatherData gatherData = new GatherData();

    private HunterSensor hunterSensor;
    private ISpawnPositionStrategy initialZoneStrategy;
    private HunterVisuals hunterVisuals;
    private Rigidbody _rigidbody;
    private bool _hasPendingCapture = false;
    private bool _isHiddenOrDead = false;

    public int RequiredGathers => Mathf.Max(1, requiredGathers);
    public int CurrentGathers { get; private set; } = 0;
    public bool HasCompletedRequiredGathers => CurrentGathers >= RequiredGathers;
    public float RespawnTime => Mathf.Max(0.1f, respawnTime);
    public float SafeZoneWaitTime => Mathf.Max(0.1f, safeZoneWaitTime);
    public float SafeZoneWaitRemaining { get; set; } = 0f;
    public float RespawnRemaining { get; set; } = 0f;
    public bool IsWaitingInSafeZone { get; set; } = false;

    public float TBA => tba;
    public float RangeAttackRadius => rangeAttackRadius;
    public float MeleeAttackRadius => meleeAttackRadius;
    public float VisionRadius => visionRadius;
    public float EyeHeight => eyeHeight;
    public LayerMask ObstacleMask => obstacleMask;
    public AgentSensor Sensor => sensor;
    public POISpawner POISpawner => poiSpawner;

    public bool IsTBAReady => Time.time >= lastAttackTime + TBA;
    public float AttackCooldownRemaining => Mathf.Max(0f, (lastAttackTime + TBA) - Time.time);
    public void ResetAttackCooldown() => lastAttackTime = Time.time;

    public PatrolData SearchConfig => searchData;
    public AttackData AttackConfig => attackData;
    public GatherData GatherConfig => gatherData;

    public Boid CurrentTarget { get; set; }
    public Boid LastDamagedTarget { get; set; }
    public ZoneArea ChosenSafeZone { get; set; }

    public StateMachine<HunterStateId> FSM { get; private set; }
    public SteeringAgent Agent { get; private set; }
    public NavigationAgent NavAgent { get; private set; }

    public HunterVisualMode CurrentVisualMode => hunterVisuals != null ? hunterVisuals.CurrentVisualMode : HunterVisualMode.Search;
    public float GatherProgress => (FSM != null && FSM.CurrentState is GatherState gs) ? gs.Progress : 0f;

    public bool IsDead => _isHiddenOrDead || _hasPendingCapture ||
                          (FSM != null && FSM.HasState && FSM.CurrentKey == HunterStateId.Dead);

    public bool IsInSafeZone => ZoneArea.IsPositionInSafeZone(transform.position);

    public bool IsInCoto
    {
        get
        {
            ZoneArea zone = ZoneArea.GetZoneAt(transform.position);
            return zone != null && zone.Type == ZoneType.Coto;
        }
    }

    public bool IsInExitZone
    {
        get
        {
            ZoneArea zone = ZoneArea.GetZoneAt(transform.position);
            return zone != null && zone.Type == ZoneType.ExitZone;
        }
    }

    public Wolf DetectedWolf => hunterSensor != null ? hunterSensor.DetectedWolf : null;

    public event Action OnCaptured;
    public event Action<int, int> OnGathersChanged;
    public event Action<string, Boid> OnActionExecuted;

    public void NotifyAction(string actionName, Boid target) => OnActionExecuted?.Invoke(actionName, target);

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeComponents();
        InitializeVisualsAndUI();
        InitializeFSM();
    }

    private void InitializeComponents()
    {
        Agent = GetComponent<SteeringAgent>();
        NavAgent = GetComponent<NavigationAgent>();
        _rigidbody = GetComponent<Rigidbody>();

        if (NavAgent != null)
        {
            NavAgent.ExcludeSafeZones = false;
        }

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

        hunterSensor = GetComponent<HunterSensor>() ?? gameObject.AddComponent<HunterSensor>();
        hunterSensor.Configure(sensor, visionRadius, eyeHeight, obstacleMask);
        initialZoneStrategy = new GraphZoneNodeSpawnStrategy(ZoneType.InitialZone);

        if (poiSpawner == null)
        {
            poiSpawner = GetComponent<POISpawner>();
        }

        if (!CompareTag("Hunter"))
        {
            try { gameObject.tag = "Hunter"; } catch (Exception) { }
        }
    }

    private void InitializeVisualsAndUI()
    {
        hunterVisuals = GetComponent<HunterVisuals>() ?? gameObject.AddComponent<HunterVisuals>();

        if (!TryGetComponent<HunterStateLabel>(out _)) gameObject.AddComponent<HunterStateLabel>();
        if (SimulationHUD.Instance == null && !TryGetComponent<SimulationHUD>(out _))
        {
            gameObject.AddComponent<SimulationHUD>();
        }
        if (!TryGetComponent<PIA1.UI.PathLineView>(out _))
        {
            gameObject.AddComponent<PIA1.UI.PathLineView>();
        }
    }

    private void InitializeFSM()
    {
        FSM = new StateMachine<HunterStateId>();
        FSM.Register(HunterStateId.Search, new SearchState(FSM, this, searchData));
        FSM.Register(HunterStateId.Attack, new AttackState(FSM, this, attackData));
        FSM.Register(HunterStateId.Gather, new GatherState(FSM, this, gatherData));
        FSM.Register(HunterStateId.Flee, new FleeState(FSM, this));
        FSM.Register(HunterStateId.Exit, new ExitState(FSM, this));
        FSM.Register(HunterStateId.Dead, new DeadState(FSM, this));
        FSM.OnStateChanged += HandleStateChanged;
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (FSM != null)
        {
            FSM.OnStateChanged -= HandleStateChanged;
        }
    }

    protected virtual void Start()
    {
        CurrentGathers = 0;
        OnGathersChanged?.Invoke(CurrentGathers, RequiredGathers);

        if (spawnInInitialZoneOnStart)
        {
            ZoneArea currentZone = ZoneArea.GetZoneAt(transform.position);
            if (currentZone == null || currentZone.Type != ZoneType.InitialZone)
            {
                TeleportToInitialZone();
            }
        }

        lastAttackTime = -TBA;
        FSM.ChangeState(HunterStateId.Search);
    }

    protected virtual void Update()
    {
        FSM.Tick();
    }

    public void OnCapturedByWolf()
    {
        if (IsDead) return;

        _hasPendingCapture = true;
        OnCaptured?.Invoke();
    }

    public bool TryConsumePendingCapture()
    {
        if (_hasPendingCapture)
        {
            _hasPendingCapture = false;
            return true;
        }
        return false;
    }

    public Wolf DetectVisibleWolf() => hunterSensor != null ? hunterSensor.DetectVisibleWolf() : null;
    public bool ShouldFleeFromWolf() => hunterSensor != null && hunterSensor.ShouldFleeFromWolf();
    public Boid SelectAttackTarget() => hunterSensor != null ? hunterSensor.SelectAttackTarget(LastDamagedTarget) : null;
    public Boid FindCollectableBoid() => hunterSensor != null ? hunterSensor.FindCollectableBoid() : null;
    public bool IsInVision(Boid boid) => hunterSensor != null && hunterSensor.IsInVision(boid);

    public void IncrementGathers()
    {
        CurrentGathers++;
        OnGathersChanged?.Invoke(CurrentGathers, RequiredGathers);
    }

    public void ResetGathers()
    {
        CurrentGathers = 0;
        OnGathersChanged?.Invoke(CurrentGathers, RequiredGathers);
    }

    public void SetHiddenState(bool hidden)
    {
        _isHiddenOrDead = hidden;

        if (hidden)
        {
            CurrentTarget = null;
            if (hunterSensor != null) hunterSensor.Clear();
            if (NavAgent != null) NavAgent.StopNavigation();
            if (Agent != null) Agent.ResetMotion();
            if (sensor != null) sensor.Clear();
        }

        if (hunterVisuals != null)
        {
            hunterVisuals.SetRenderersEnabled(!hidden);
        }
    }

    public void SetVisualMode(HunterVisualMode mode)
    {
        if (hunterVisuals != null)
        {
            hunterVisuals.SetVisualMode(mode);
        }
    }

    private void HandleStateChanged(HunterStateId previousState, HunterStateId newState)
    {
        HunterVisualMode mode = newState switch
        {
            HunterStateId.Search => HunterVisualMode.Search,
            HunterStateId.Attack => HunterVisualMode.Pursuit,
            HunterStateId.Gather => HunterVisualMode.Gather,
            HunterStateId.Flee => HunterVisualMode.Flee,
            HunterStateId.Exit => HunterVisualMode.Exit,
            HunterStateId.Dead => HunterVisualMode.Dead,
            _ => HunterVisualMode.Search
        };
        SetVisualMode(mode);
    }

    public void TeleportToInitialZone()
    {
        CurrentTarget = null;
        LastDamagedTarget = null;
        ChosenSafeZone = null;
        if (hunterSensor != null) hunterSensor.Clear();
        if (NavAgent != null) NavAgent.StopNavigation();
        if (Agent != null) Agent.ResetMotion();

        if (initialZoneStrategy != null && initialZoneStrategy.TryGetSpawnPosition(out Vector3 spawnPos))
        {
            spawnPos.y = transform.position.y;
            transform.position = spawnPos;
            if (_rigidbody != null)
            {
                _rigidbody.position = spawnPos;
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                }
            }
            Physics.SyncTransforms();
        }

        if (sensor != null)
        {
            sensor.Clear();
        }
    }

    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showOnlyWhenSelected = false;
    [SerializeField] private Color visionGizmoColor = new Color(0f, 0.8f, 1f, 0.45f);
    [SerializeField] private Color rangeGizmoColor = new Color(1f, 0.92f, 0.016f, 0.65f);
    [SerializeField] private Color meleeGizmoColor = new Color(1f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Color wolfAlertGizmoColor = new Color(1f, 0.45f, 0f, 0.95f);

    public bool ShowGizmos { get => showGizmos; set => showGizmos = value; }
    public bool ShowOnlyWhenSelected { get => showOnlyWhenSelected; set => showOnlyWhenSelected = value; }

    private void OnDrawGizmos()
    {
        if (showGizmos && !showOnlyWhenSelected)
        {
            DrawHunterGizmosInternal();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos && showOnlyWhenSelected)
        {
            DrawHunterGizmosInternal();
        }
    }

    private void DrawHunterGizmosInternal()
    {
        Gizmos.color = visionGizmoColor;
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        Gizmos.color = rangeGizmoColor;
        Gizmos.DrawWireSphere(transform.position, RangeAttackRadius);

        Gizmos.color = meleeGizmoColor;
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRadius);

        if (Application.isPlaying && DetectedWolf != null && !IsDead)
        {
            Gizmos.color = wolfAlertGizmoColor;
            Vector3 from = transform.position + Vector3.up * eyeHeight;
            Vector3 to = DetectedWolf.transform.position + Vector3.up * eyeHeight;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(to, 0.35f);
        }
    }
}
