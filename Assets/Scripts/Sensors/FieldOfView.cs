using System;
using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Sensors
{
    public class FieldOfView : MonoBehaviour
    {
        [Header("FOV Configuration")]
        [Tooltip("Radio máximo de visión en metros.")]
        [SerializeField] private float visionRadius = 12f;

        [Tooltip("Ángulo total del cono de visión en grados (ej. 90° o 110° para Lobos, 360° para visión omnidireccional).")]
        [Range(1f, 360f)]
        [SerializeField] private float visionAngle = 90f;

        [Tooltip("Altura de los ojos sobre el pivote del agente para evitar colisiones con el suelo.")]
        [SerializeField] private float eyeHeight = 0.8f;

        [Tooltip("Altura estimada de los ojos/centro del objetivo.")]
        [SerializeField] private float targetEyeHeight = 0.8f;

        [Header("Layers & Filtering")]
        [Tooltip("Máscara de capas que bloquean la visión física (paredes/muros).")]
        [SerializeField] private LayerMask obstacleMask = 0;

        [Tooltip("Máscara de capas de los objetivos a detectar.")]
        [SerializeField] private LayerMask targetMask = ~0;

        [Tooltip("Si está activo, los objetivos dentro de una Zona Segura no podrán ser detectados bajo ninguna circunstancia.")]
        [SerializeField] private bool respectSafeZoneImmunity = true;

        [Header("Update & Scanning")]
        [Tooltip("Intervalo en segundos para refrescar la lista de objetivos visibles.")]
        [SerializeField] private float scanInterval = 0.12f;

        [Header("Gizmos & Visualization")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showOnlyWhenSelected = false;
        [SerializeField] private Color radiusWireColor = new Color(1f, 0.92f, 0.016f, 0.2f);
        [SerializeField] private Color fovConeColor = new Color(0.2f, 0.6f, 1f, 0.85f);
        [SerializeField] private Color detectedTargetColor = Color.red;

        public bool ShowGizmos { get => showGizmos; set => showGizmos = value; }
        public bool ShowOnlyWhenSelected { get => showOnlyWhenSelected; set => showOnlyWhenSelected = value; }

        private readonly List<Collider> _candidates = new List<Collider>(16);
        private readonly List<Transform> _visibleTargets = new List<Transform>(16);
        private SphereCollider _triggerCollider;
        private float _lastScanTime = -999f;
        private Transform _closestVisibleTarget;

        public float VisionRadius
        {
            get => visionRadius;
            set
            {
                visionRadius = value;
                if (_triggerCollider != null) _triggerCollider.radius = visionRadius;
            }
        }

        public float VisionAngle { get => visionAngle; set => visionAngle = value; }
        public float EyeHeight => eyeHeight;
        public float TargetEyeHeight => targetEyeHeight;
        public LayerMask ObstacleMask { get => obstacleMask; set => obstacleMask = value; }
        public LayerMask TargetMask { get => targetMask; set => targetMask = value; }
        public bool RespectSafeZoneImmunity { get => respectSafeZoneImmunity; set => respectSafeZoneImmunity = value; }
        public IReadOnlyList<Transform> VisibleTargets => _visibleTargets;
        public Transform ClosestVisibleTarget => _closestVisibleTarget;
        public bool HasTargetInSight => _closestVisibleTarget != null;

        public event Action<Transform> OnTargetSpotted;
        public event Action<Transform> OnTargetLost;

        private void Awake()
        {
            if (obstacleMask == 0)
            {
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (obstacleLayer >= 0) obstacleMask = 1 << obstacleLayer;
            }

            SetupSensorTrigger();
        }

        private void SetupSensorTrigger()
        {
            Transform sensorChild = transform.Find("[FOV_Sensor]");
            if (sensorChild == null)
            {
                GameObject go = new GameObject("[FOV_Sensor]");
                sensorChild = go.transform;
                sensorChild.SetParent(transform, false);
                sensorChild.localPosition = Vector3.zero;
                sensorChild.localRotation = Quaternion.identity;
                sensorChild.localScale = Vector3.one;
            }

            int sensorLayer = LayerMask.NameToLayer("Sensor");
            if (sensorLayer >= 0)
            {
                sensorChild.gameObject.layer = sensorLayer;
            }

            _triggerCollider = sensorChild.GetComponent<SphereCollider>();
            if (_triggerCollider == null)
            {
                _triggerCollider = sensorChild.gameObject.AddComponent<SphereCollider>();
            }
            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = visionRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other.isTrigger) return;
            if (other.transform == transform || other.transform.IsChildOf(transform)) return;
            if (((1 << other.gameObject.layer) & targetMask) == 0) return;

            if (!_candidates.Contains(other))
            {
                _candidates.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;

            if (_candidates.Remove(other))
            {
                Transform t = other.transform;
                if (_visibleTargets.Remove(t))
                {
                    OnTargetLost?.Invoke(t);
                    if (_closestVisibleTarget == t)
                    {
                        _closestVisibleTarget = _visibleTargets.Count > 0 ? _visibleTargets[0] : null;
                    }
                }
            }
        }

        private void Update()
        {
            if (Time.time - _lastScanTime < scanInterval) return;
            _lastScanTime = Time.time;

            UpdateVisibleTargets();
        }

        public void UpdateVisibleTargets()
        {
            _closestVisibleTarget = null;
            float closestSqrDist = float.MaxValue;

            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                Collider col = _candidates[i];
                if (col == null || !col.gameObject.activeInHierarchy)
                {
                    _candidates.RemoveAt(i);
                    continue;
                }

                Transform targetT = col.transform;
                bool canSee = CanSeeTarget(targetT);

                bool wasVisible = _visibleTargets.Contains(targetT);

                if (canSee)
                {
                    if (!wasVisible)
                    {
                        _visibleTargets.Add(targetT);
                        OnTargetSpotted?.Invoke(targetT);
                    }

                    float sqrDist = (targetT.position - transform.position).sqrMagnitude;
                    if (sqrDist < closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        _closestVisibleTarget = targetT;
                    }
                }
                else
                {
                    if (wasVisible)
                    {
                        _visibleTargets.Remove(targetT);
                        OnTargetLost?.Invoke(targetT);
                    }
                }
            }
        }

        public bool CanSeeTarget(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            if (target.TryGetComponent<HunterNPC>(out HunterNPC hunter) && hunter.IsDead) return false;
            return CanSeePosition(target.position);
        }

        public bool CanSeePosition(Vector3 targetPosition)
        {
            if (respectSafeZoneImmunity && ZoneArea.IsPositionInSafeZone(targetPosition))
            {
                return false;
            }

            Vector3 diff = targetPosition - transform.position;
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;
            if (sqrDist > visionRadius * visionRadius)
            {
                return false;
            }

            if (visionAngle < 359f && sqrDist > 0.0001f)
            {
                Vector3 dirToTarget = diff.normalized;
                float angle = Vector3.Angle(transform.forward, dirToTarget);
                if (angle > visionAngle * 0.5f)
                {
                    return false;
                }
            }

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetEyePos = targetPosition + Vector3.up * targetEyeHeight;
            return LineOfSight.HasLineOfSight(eyePos, targetEyePos, obstacleMask);
        }

        public void Clear()
        {
            _candidates.Clear();
            _visibleTargets.Clear();
            _closestVisibleTarget = null;
        }

        private void OnDrawGizmos()
        {
            if (showGizmos && !showOnlyWhenSelected)
            {
                DrawFOVGizmosInternal();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (showGizmos && showOnlyWhenSelected)
            {
                DrawFOVGizmosInternal();
            }
        }

        private void DrawFOVGizmosInternal()
        {
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

            Gizmos.color = radiusWireColor;
            Gizmos.DrawWireSphere(transform.position, visionRadius);

            if (visionAngle < 359f)
            {
                Gizmos.color = fovConeColor;
                Vector3 leftDir = Quaternion.AngleAxis(-visionAngle * 0.5f, Vector3.up) * transform.forward;
                Vector3 rightDir = Quaternion.AngleAxis(visionAngle * 0.5f, Vector3.up) * transform.forward;

                Vector3 leftPoint = eyePos + leftDir * visionRadius;
                Vector3 rightPoint = eyePos + rightDir * visionRadius;

                Gizmos.DrawLine(eyePos, leftPoint);
                Gizmos.DrawLine(eyePos, rightPoint);

                int segments = 16;
                Vector3 prevPoint = leftPoint;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float currentAngle = Mathf.Lerp(-visionAngle * 0.5f, visionAngle * 0.5f, t);
                    Vector3 arcDir = Quaternion.AngleAxis(currentAngle, Vector3.up) * transform.forward;
                    Vector3 currentPoint = eyePos + arcDir * visionRadius;
                    Gizmos.DrawLine(prevPoint, currentPoint);
                    prevPoint = currentPoint;
                }
            }

            if (Application.isPlaying && _visibleTargets.Count > 0)
            {
                Gizmos.color = detectedTargetColor;
                for (int i = 0; i < _visibleTargets.Count; i++)
                {
                    Transform t = _visibleTargets[i];
                    if (t != null)
                    {
                        Vector3 targetEyePos = t.position + Vector3.up * targetEyeHeight;
                        Gizmos.DrawLine(eyePos, targetEyePos);
                        Gizmos.DrawSphere(targetEyePos, 0.25f);
                    }
                }
            }
        }
    }
}
