using System;
using System.Collections.Generic;
using UnityEngine;
using PIA1.Navigation;
using PIA1.Sensors;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    [RequireComponent(typeof(SteeringAgent))]
    [RequireComponent(typeof(NavigationAgent))]
    [RequireComponent(typeof(FieldOfView))]
    public class Wolf : MonoBehaviour
    {
        [Header("Wolf Identity & Hierarchy")]
        [SerializeField] private string wolfId = "Lobo";

        [Header("Movement Speeds (Must be < Hunter Speed)")]
        [Tooltip("Velocidad máxima en patrullaje (estrictamente menor a la del Cazador).")]
        [SerializeField] private float patrolSpeed = 3.2f;
        [Tooltip("Velocidad máxima en persecución/investigación (estrictamente menor a la del Cazador).")]
        [SerializeField] private float chaseSpeed = 3.8f;

        [Header("Capture Settings")]
        [Tooltip("Distancia horizontal mínima (con línea de visión) para capturar al Cazador inmediatamente.")]
        [SerializeField] private float captureRadius = 1.25f;

        [Header("Perception, Howl & Memory")]
        [Tooltip("Tiempo en segundos que el lobo persiste hacia la Última Posición Conocida tras perder visión en Chase.")]
        [SerializeField] private float memoryTime = 3.0f;
        [Tooltip("Tiempo en segundos que el lobo inspecciona el área en Search antes de retornar a Patrol.")]
        [SerializeField] private float searchDuration = 4.0f;
        [Tooltip("Tiempo mínimo entre emisiones de aullido de un mismo lobo para evitar spam por frame.")]
        [SerializeField] private float howlCooldown = 2.0f;

        [Header("References")]
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private HunterNPC targetHunter;
        [SerializeField] private Renderer wolfRenderer;

        [Header("Gizmos & Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color captureRadiusColor = new Color(1f, 0.2f, 0.2f, 0.45f);
        [SerializeField] private Color lkpMarkerColor = new Color(1f, 0.85f, 0f, 0.9f);

        private SteeringAgent _steering;
        private NavigationAgent _navAgent;
        private FieldOfView _fov;
        private StateMachine<WolfStateId> _fsm;

        private Vector3 _lastKnownPosition;
        private bool _hasLastKnownPosition = false;

        private Vector3 _pendingAlertPosition;
        private bool _hasPendingAlert = false;
        private float _lastAlertReceivedTime = -999f;
        private float _lastHowlTime = -999f;

        private Vector3 _lastValidOutsideSafePos;

        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public string WolfId
        {
            get
            {
                string id = string.IsNullOrEmpty(wolfId) ? gameObject.name : wolfId;
                if (id.StartsWith("Wolf", StringComparison.OrdinalIgnoreCase))
                {
                    id = "Lobo" + id.Substring(4);
                }
                return id;
            }
        }
        public SteeringAgent Steering => _steering;
        public NavigationAgent NavAgent => _navAgent;
        public FieldOfView FOV => _fov;
        public PatrolRoute Route => patrolRoute;
        public StateMachine<WolfStateId> FSM => _fsm;
        public HunterNPC TargetHunter { get => targetHunter; set => targetHunter = value; }
        public float CaptureRadius => captureRadius;
        public float MemoryTime => memoryTime;
        public float SearchDuration => searchDuration;
        public float PatrolSpeed => patrolSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float LastHowlTime => _lastHowlTime;
        public float LastAlertReceivedTime => _lastAlertReceivedTime;

        public Vector3 LastKnownPosition
        {
            get => _lastKnownPosition;
            set
            {
                _lastKnownPosition = value;
                _hasLastKnownPosition = true;
            }
        }

        public bool HasLastKnownPosition
        {
            get => _hasLastKnownPosition;
            set => _hasLastKnownPosition = value;
        }

        public bool HasPendingAlert => _hasPendingAlert;

        public event Action<Wolf, Vector3> OnHowled;
        public event Action<Wolf, Vector3> OnAlertReceived;
        public event Action<Wolf, HunterNPC> OnHunterCaptured;

        private void Awake()
        {
            InitializeComponents();
            ConfigurePerceptionAndNavigation();
            InitializeFSM();
            InitializeVisualsAndUI();
        }

        private void InitializeComponents()
        {
            _steering = GetComponent<SteeringAgent>();
            _navAgent = GetComponent<NavigationAgent>();
            _fov = GetComponent<FieldOfView>();

            if (patrolRoute == null)
            {
                patrolRoute = GetComponent<PatrolRoute>() ?? GetComponentInChildren<PatrolRoute>();
            }

            if (wolfRenderer == null)
            {
                wolfRenderer = GetComponentInChildren<Renderer>();
            }

            _propBlock = new MaterialPropertyBlock();
            _lastValidOutsideSafePos = transform.position;
        }

        private void ConfigurePerceptionAndNavigation()
        {
            if (_navAgent != null)
            {
                _navAgent.ExcludeSafeZones = true;
            }

            if (_fov != null)
            {
                _fov.RespectSafeZoneImmunity = true;
                int hunterLayer = LayerMask.NameToLayer("Hunter");
                if (hunterLayer >= 0)
                {
                    _fov.TargetMask = 1 << hunterLayer;
                }
            }

            ApplySpeedForState(WolfStateId.Patrol);
        }

        private void InitializeFSM()
        {
            _fsm = new StateMachine<WolfStateId>();
            _fsm.Register(WolfStateId.Patrol, new WolfPatrolState(_fsm, this));
            _fsm.Register(WolfStateId.Chase, new WolfChaseState(_fsm, this));
            _fsm.Register(WolfStateId.Investigate, new WolfInvestigateState(_fsm, this));
            _fsm.Register(WolfStateId.Search, new WolfSearchState(_fsm, this));

            _fsm.OnStateChanged += HandleStateChanged;
        }

        private void InitializeVisualsAndUI()
        {
            if (!TryGetComponent<PIA1.UI.WolfStateLabel>(out _))
            {
                gameObject.AddComponent<PIA1.UI.WolfStateLabel>();
            }

            if (!TryGetComponent<PIA1.UI.PathLineView>(out _))
            {
                gameObject.AddComponent<PIA1.UI.PathLineView>();
            }

            if (WolfPack.Instance == null)
            {
                new GameObject("[WolfPack]").AddComponent<WolfPack>();
            }
        }

        private void OnEnable()
        {
            if (WolfPack.Instance != null)
            {
                WolfPack.Instance.RegisterWolf(this);
                WolfPack.Instance.OnHowlAlert += HandlePackHowlAlert;
                WolfPack.Instance.OnLKPDiscarded += HandlePackLKPDiscarded;
            }
        }

        private void OnDisable()
        {
            if (WolfPack.Instance != null)
            {
                WolfPack.Instance.UnregisterWolf(this);
                WolfPack.Instance.OnHowlAlert -= HandlePackHowlAlert;
                WolfPack.Instance.OnLKPDiscarded -= HandlePackLKPDiscarded;
            }
        }

        private void Start()
        {
            if (targetHunter == null)
            {
                targetHunter = HunterNPC.Instance;
            }

            if (WolfPack.Instance != null)
            {
                WolfPack.Instance.RegisterWolf(this);
                WolfPack.Instance.OnHowlAlert -= HandlePackHowlAlert;
                WolfPack.Instance.OnHowlAlert += HandlePackHowlAlert;
                WolfPack.Instance.OnLKPDiscarded -= HandlePackLKPDiscarded;
                WolfPack.Instance.OnLKPDiscarded += HandlePackLKPDiscarded;
            }

            EnforceSlowerThanHunter();

            if (!ZoneArea.IsPositionInSafeZone(transform.position))
            {
                _lastValidOutsideSafePos = transform.position;
            }

            _fsm.ChangeState(WolfStateId.Patrol);
            UpdateStateVisuals(WolfStateId.Patrol);
        }

        private void Update()
        {
            EnforceSlowerThanHunter();
            _fsm.Tick();
            CheckProximityCapture();
        }

        private void LateUpdate()
        {
            if (ZoneArea.IsPositionInSafeZone(transform.position))
            {
                transform.position = _lastValidOutsideSafePos;
                if (_steering != null)
                {
                    _steering.Stop();
                }
            }
            else
            {
                _lastValidOutsideSafePos = transform.position;
            }
        }

        private void OnDestroy()
        {
            if (_fsm != null)
            {
                _fsm.OnStateChanged -= HandleStateChanged;
            }
        }

        private void EnforceSlowerThanHunter()
        {
            if (targetHunter == null || targetHunter.Agent == null) return;

            float maxAllowed = Mathf.Max(0.5f, targetHunter.Agent.MaxSpeed - 0.3f);
            if (chaseSpeed >= targetHunter.Agent.MaxSpeed)
            {
                chaseSpeed = maxAllowed;
            }
            if (patrolSpeed >= chaseSpeed)
            {
                patrolSpeed = Mathf.Max(0.4f, chaseSpeed - 0.4f);
            }
        }

        private void ApplySpeedForState(WolfStateId state)
        {
            if (_steering == null) return;
            EnforceSlowerThanHunter();
            _steering.MaxSpeed = (state == WolfStateId.Chase || state == WolfStateId.Investigate)
                ? chaseSpeed
                : patrolSpeed;
        }

        public void CheckProximityCapture()
        {
            if (targetHunter == null || !targetHunter.gameObject.activeInHierarchy || targetHunter.IsDead)
            {
                return;
            }

            if (ZoneArea.IsPositionInSafeZone(targetHunter.transform.position))
            {
                return;
            }

            Vector3 diff = targetHunter.transform.position - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude > captureRadius * captureRadius)
            {
                return;
            }

            LayerMask obsMask = _steering != null ? _steering.ObstacleMask : LayerMask.GetMask("Obstacle");
            float eyeH = _fov != null ? _fov.EyeHeight : 0.8f;
            Vector3 fromEye = transform.position + Vector3.up * eyeH;
            Vector3 toEye = targetHunter.transform.position + Vector3.up * eyeH;

            if (!LineOfSight.HasLineOfSight(fromEye, toEye, obsMask))
            {
                return;
            }

            TriggerCapture(targetHunter);
        }

        public void TriggerCapture(HunterNPC hunter)
        {
            if (hunter == null || hunter.IsDead) return;

            _hasLastKnownPosition = false;
            _hasPendingAlert = false;

            OnHunterCaptured?.Invoke(this, hunter);
            hunter.OnCapturedByWolf();
        }

        public void Howl(bool ignoreCooldown = false)
        {
            if (!ignoreCooldown && Time.time - _lastHowlTime < howlCooldown)
            {
                return;
            }

            if (targetHunter != null && !ZoneArea.IsPositionInSafeZone(targetHunter.transform.position))
            {
                LastKnownPosition = targetHunter.transform.position;
            }

            if (!_hasLastKnownPosition) return;

            _lastHowlTime = Time.time;
            OnHowled?.Invoke(this, _lastKnownPosition);
            if (WolfPack.Instance != null)
            {
                WolfPack.Instance.NotifyWolfHowl(this, _lastKnownPosition);
            }
        }

        public void ReceiveAlert(Vector3 lkp)
        {
            if (ZoneArea.IsPositionInSafeZone(lkp))
            {
                return;
            }

            if (_fsm != null && _fsm.CurrentKey == WolfStateId.Chase &&
                targetHunter != null && _fov != null && _fov.CanSeeTarget(targetHunter.transform))
            {
                return;
            }

            _pendingAlertPosition = lkp;
            _hasPendingAlert = true;
            _lastAlertReceivedTime = Time.time;
            OnAlertReceived?.Invoke(this, lkp);
        }

        public void AlertToPosition(Vector3 lkp) => ReceiveAlert(lkp);

        public bool TryConsumePendingAlert(out Vector3 alertPosition)
        {
            if (_hasPendingAlert)
            {
                _hasPendingAlert = false;
                alertPosition = _pendingAlertPosition;
                return !ZoneArea.IsPositionInSafeZone(alertPosition);
            }

            alertPosition = Vector3.zero;
            return false;
        }

        public void ClearMemoryAndAlerts()
        {
            _hasLastKnownPosition = false;
            _hasPendingAlert = false;
        }

        private void HandlePackHowlAlert(Wolf howler, Vector3 lkp)
        {
            if (howler == this) return;
            if (WolfPack.Instance != null && !WolfPack.Instance.GlobalHowlRange)
            {
                float sqrDist = (transform.position - howler.transform.position).sqrMagnitude;
                float maxAudition = WolfPack.Instance.HowlAuditionRadius;
                if (sqrDist > maxAudition * maxAudition) return;
            }
            ReceiveAlert(lkp);
        }

        private void HandlePackLKPDiscarded()
        {
            ClearMemoryAndAlerts();
        }

        private void HandleStateChanged(WolfStateId previousState, WolfStateId newState)
        {
            UpdateStateVisuals(newState);
            ApplySpeedForState(newState);
        }

        private void UpdateStateVisuals(WolfStateId state)
        {
            Color stateColor = state switch
            {
                WolfStateId.Patrol => new Color(0.85f, 0.45f, 0.15f),
                WolfStateId.Chase => new Color(0.95f, 0.1f, 0.1f),
                WolfStateId.Investigate => new Color(1f, 0.85f, 0.1f),
                WolfStateId.Search => new Color(0.65f, 0.2f, 0.9f),
                _ => Color.white
            };

            SetVisualColor(stateColor);
        }

        public void SetVisualColor(Color color)
        {
            if (wolfRenderer == null) return;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            wolfRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, color);
            _propBlock.SetColor(ColorId, color);
            wolfRenderer.SetPropertyBlock(_propBlock);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Gizmos.color = captureRadiusColor;
            Gizmos.DrawWireSphere(transform.position, captureRadius);

            if (_hasLastKnownPosition && (_fsm != null && _fsm.CurrentKey != WolfStateId.Patrol))
            {
                Gizmos.color = lkpMarkerColor;
                Vector3 markerPos = _lastKnownPosition + Vector3.up * 0.3f;
                Gizmos.DrawWireSphere(markerPos, 0.5f);
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, markerPos);
            }
        }
    }
}
