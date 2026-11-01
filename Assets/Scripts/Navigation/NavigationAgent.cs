using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Navigation
{
    public enum NavigationMode
    {
        Idle,
        Direct,
        AStar
    }

    [RequireComponent(typeof(SteeringAgent))]
    public class NavigationAgent : MonoBehaviour
    {
        [Header("Agent Constraints")]
        [Tooltip("Activar para los Lobos: prohíbe usar nodos de Zonas Seguras o ingresar en ellas (Regla R-09).")]
        [SerializeField] private bool excludeSafeZones = false;
        [SerializeField] private HeuristicType heuristic = HeuristicType.Euclidean;

        [Header("Path Following Thresholds")]
        [Tooltip("Distancia en el plano XZ para dar por alcanzado un nodo intermedio de la ruta A* y pasar al siguiente.")]
        [SerializeField] private float nodeCheckDistance = 0.65f;
        [Tooltip("Distancia en el plano XZ para dar por alcanzado el destino final.")]
        [SerializeField] private float arrivalDistance = 0.65f;
        [Tooltip("Radio de frenado progresivo al usar Arrive sobre el destino final.")]
        [SerializeField] private float slowingRadius = 2.5f;

        [Header("Line of Sight & Repath")]
        [Tooltip("Altura de los ojos para comprobar Línea de Visión hacia nodos y objetivos.")]
        [SerializeField] private float eyeHeight = 0.8f;
        [Tooltip("Radio del cuerpo para comprobar si puede ir en línea recta sin chocar contra esquinas de paredes.")]
        [SerializeField] private float clearanceRadius = 0.3f;
        [Tooltip("Intervalo mínimo en segundos entre recálculos de A* cuando el destino se mueve.")]
        [SerializeField] private float repathInterval = 0.35f;
        [Tooltip("Distancia que debe desplazarse el destino para forzar un nuevo cálculo de A*.")]
        [SerializeField] private float repathTargetMoveThreshold = 1.2f;

        [Header("A* Smooth (Clase 27/10/2026)")]
        [Tooltip("Aplica el algoritmo de suavizado de caminos con SphereCast enseñado por el profesor para eliminar el movimiento rígido en escalera.")]
        [SerializeField] private bool usePathSmoothing = true;

        [Header("Debug Gizmos")]
        [SerializeField] private bool showPathGizmos = true;
        [SerializeField] private Color aStarPathColor = Color.cyan;
        [SerializeField] private Color directLineColor = Color.green;

        private SteeringAgent _steering;
        private readonly List<Node> _currentPath = new List<Node>(64);
        private readonly List<Node> _evalPathBuffer = new List<Node>(64);

        private int _currentPathIndex = 0;
        private NavigationMode _currentMode = NavigationMode.Idle;
        private Vector3 _currentDestination;
        private Node _lastTargetNode;
        private Vector3 _lastPathedTargetPos;
        private float _lastRepathTime = -999f;
        private bool _hasReachedDestination = false;

        public SteeringAgent Steering => _steering;
        public NavigationMode CurrentMode => _currentMode;
        public IReadOnlyList<Node> CurrentPath => _currentPath;
        public int CurrentPathIndex => _currentPathIndex;
        public Vector3 CurrentDestination => _currentDestination;
        public bool HasReachedDestination => _hasReachedDestination;
        public bool ExcludeSafeZones { get => excludeSafeZones; set => excludeSafeZones = value; }
        public bool UsePathSmoothing { get => usePathSmoothing; set => usePathSmoothing = value; }

        private void Awake()
        {
            _steering = GetComponent<SteeringAgent>();
        }

        public void StopNavigation()
        {
            _currentPath.Clear();
            _currentPathIndex = 0;
            _lastTargetNode = null;
            _currentMode = NavigationMode.Idle;
            _hasReachedDestination = false;
            if (_steering != null)
            {
                _steering.Stop();
            }
        }

        public bool NavigateToNode(Node targetNode, bool useArriveAtEnd = false)
        {
            if (targetNode == null)
            {
                StopNavigation();
                return false;
            }

            if (excludeSafeZones && targetNode.IsSafeZone)
            {
                StopNavigation();
                return false;
            }

            _currentDestination = targetNode.Position;

            float sqrDistToTarget = HorizontalSqrDistance(transform.position, targetNode.Position);
            if (sqrDistToTarget <= arrivalDistance * arrivalDistance)
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
                _lastTargetNode = null;
                _hasReachedDestination = true;
                if (useArriveAtEnd)
                {
                    _steering.Stop();
                }
                return true;
            }

            _hasReachedDestination = false;

            if (HasDirectClearanceTo(targetNode.Position))
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
                _lastTargetNode = null;
                _currentMode = NavigationMode.Direct;

                Vector3 force = useArriveAtEnd
                    ? SteeringBehaviors.Arrive(_steering, targetNode.Position, slowingRadius)
                    : SteeringBehaviors.Seek(_steering, targetNode.Position);
                _steering.ApplyForce(force);
                return false;
            }

            _currentMode = NavigationMode.AStar;

            bool needsRepath = _currentPath.Count == 0 ||
                               _lastTargetNode != targetNode ||
                               _currentPathIndex >= _currentPath.Count;

            if (needsRepath && Time.time - _lastRepathTime >= repathInterval)
            {
                ComputePathToNode(targetNode);
            }

            FollowCurrentPath(targetNode.Position, useArriveAtEnd);
            return false;
        }

        public bool NavigateToPosition(Vector3 targetPosition, bool useArriveAtEnd = true)
        {
            if (excludeSafeZones && ZoneArea.IsPositionInSafeZone(targetPosition))
            {
                StopNavigation();
                return false;
            }

            _currentDestination = targetPosition;

            float sqrDistToTarget = HorizontalSqrDistance(transform.position, targetPosition);
            if (sqrDistToTarget <= arrivalDistance * arrivalDistance)
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
                _lastTargetNode = null;
                _hasReachedDestination = true;
                if (useArriveAtEnd)
                {
                    _steering.Stop();
                }
                return true;
            }

            _hasReachedDestination = false;

            if (HasDirectClearanceTo(targetPosition))
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
                _lastTargetNode = null;
                _currentMode = NavigationMode.Direct;

                Vector3 force = useArriveAtEnd
                    ? SteeringBehaviors.Arrive(_steering, targetPosition, slowingRadius)
                    : SteeringBehaviors.Seek(_steering, targetPosition);
                _steering.ApplyForce(force);
                return false;
            }

            _currentMode = NavigationMode.AStar;

            bool targetMoved = HorizontalSqrDistance(_lastPathedTargetPos, targetPosition) >= repathTargetMoveThreshold * repathTargetMoveThreshold;
            bool needsRepath = _currentPath.Count == 0 || targetMoved || _currentPathIndex >= _currentPath.Count;

            if (needsRepath && Time.time - _lastRepathTime >= repathInterval)
            {
                if (Graph.Instance != null)
                {
                    Node goalNode = Graph.Instance.GetClosestVisibleNode(targetPosition, eyeHeight, excludeSafeZones);
                    if (goalNode != null)
                    {
                        ComputePathToNode(goalNode);
                        _lastPathedTargetPos = targetPosition;
                    }
                }
            }

            FollowCurrentPath(targetPosition, useArriveAtEnd);
            return false;
        }

        public void PursueAgent(SteeringAgent targetAgent)
        {
            if (targetAgent == null)
            {
                StopNavigation();
                return;
            }

            Vector3 targetPos = targetAgent.transform.position;
            if (excludeSafeZones && ZoneArea.IsPositionInSafeZone(targetPos))
            {
                StopNavigation();
                return;
            }

            _currentDestination = targetPos;
            _hasReachedDestination = false;

            if (HasDirectVisionTo(targetPos))
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
                _lastTargetNode = null;
                _currentMode = NavigationMode.Direct;

                Vector3 pursuitForce = SteeringBehaviors.Pursuit(_steering, targetAgent);
                _steering.ApplyForce(pursuitForce);
                return;
            }

            NavigateToPosition(targetPos, useArriveAtEnd: false);
        }

        public Node FindClosestSafeZoneNodeByPath(out ZoneArea chosenSafeZone, out float bestPathDistance)
        {
            chosenSafeZone = null;
            bestPathDistance = float.MaxValue;

            if (Graph.Instance == null) return null;

            Node startNode = Graph.Instance.GetClosestVisibleNode(transform.position, eyeHeight, excludeSafeZones: false);
            if (startNode == null) return null;

            Node bestSafeNode = null;
            IReadOnlyList<Node> allNodes = Graph.Instance.Nodes;

            for (int i = 0; i < allNodes.Count; i++)
            {
                Node candidate = allNodes[i];
                if (candidate == null || !candidate.IsWalkable || !candidate.IsSafeZone) continue;

                if (Pathfinding.FindPathNonAlloc(startNode, candidate, _evalPathBuffer, excludeSafeZones: false, heuristic))
                {
                    float dist = Vector3.Distance(transform.position, startNode.Position) +
                                 Pathfinding.CalculatePathDistance(_evalPathBuffer);

                    if (dist < bestPathDistance)
                    {
                        bestPathDistance = dist;
                        bestSafeNode = candidate;
                    }
                }
            }

            if (bestSafeNode != null)
            {
                chosenSafeZone = ZoneArea.GetZoneAt(bestSafeNode.Position);
            }

            return bestSafeNode;
        }

        public bool HasDirectVisionTo(Vector3 targetPosition)
        {
            LayerMask mask = _steering != null ? _steering.ObstacleMask : LayerMask.GetMask("Obstacle");
            Vector3 from = transform.position + Vector3.up * eyeHeight;
            Vector3 to = targetPosition + Vector3.up * eyeHeight;
            return LineOfSight.HasLineOfSight(from, to, mask, 0f);
        }

        public bool HasDirectClearanceTo(Vector3 targetPosition)
        {
            LayerMask mask = _steering != null ? _steering.ObstacleMask : LayerMask.GetMask("Obstacle");
            Vector3 from = transform.position + Vector3.up * eyeHeight;
            Vector3 to = targetPosition + Vector3.up * eyeHeight;
            return LineOfSight.HasLineOfSight(from, to, mask, clearanceRadius);
        }

        private void ComputePathToNode(Node targetNode)
        {
            _lastRepathTime = Time.time;
            _lastTargetNode = targetNode;

            if (Graph.Instance == null || targetNode == null) return;

            Node startNode = Graph.Instance.GetClosestVisibleNode(transform.position, eyeHeight, excludeSafeZones);
            if (startNode == null) return;

            if (Pathfinding.FindPathNonAlloc(startNode, targetNode, _currentPath, excludeSafeZones, heuristic))
            {
                if (usePathSmoothing && _currentPath.Count >= 3)
                {
                    LayerMask mask = _steering != null ? _steering.ObstacleMask : LayerMask.GetMask("Obstacle");
                    Pathfinding.SmoothPathNonAlloc(_currentPath, mask, clearanceRadius, excludeSafeZones);
                }

                _currentPathIndex = 0;

                if (_currentPath.Count > 1 && HasDirectClearanceTo(_currentPath[1].Position))
                {
                    float dist0to1 = HorizontalDistance(_currentPath[0].Position, _currentPath[1].Position);
                    float distMeTo1 = HorizontalDistance(transform.position, _currentPath[1].Position);
                    if (distMeTo1 <= dist0to1)
                    {
                        _currentPathIndex = 1;
                    }
                }
            }
            else
            {
                _currentPath.Clear();
                _currentPathIndex = 0;
            }
        }

        private void FollowCurrentPath(Vector3 finalDestination, bool useArriveAtEnd)
        {
            if (_currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
            {
                Vector3 fallbackForce = useArriveAtEnd
                    ? SteeringBehaviors.Arrive(_steering, finalDestination, slowingRadius)
                    : SteeringBehaviors.Seek(_steering, finalDestination);
                _steering.ApplyForce(fallbackForce);
                return;
            }

            Node currentWaypoint = _currentPath[_currentPathIndex];
            if (currentWaypoint == null)
            {
                _currentPathIndex++;
                return;
            }

            float sqrDist = HorizontalSqrDistance(transform.position, currentWaypoint.Position);
            if (sqrDist <= nodeCheckDistance * nodeCheckDistance)
            {
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count)
                {
                    return;
                }
                currentWaypoint = _currentPath[_currentPathIndex];
            }

            bool isLastNode = (_currentPathIndex == _currentPath.Count - 1);
            Vector3 steerForce = (isLastNode && useArriveAtEnd)
                ? SteeringBehaviors.Arrive(_steering, currentWaypoint.Position, slowingRadius)
                : SteeringBehaviors.Seek(_steering, currentWaypoint.Position);

            _steering.ApplyForce(steerForce);
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(HorizontalSqrDistance(a, b));
        }

        private void OnDrawGizmos()
        {
            if (!showPathGizmos || !Application.isPlaying) return;

            Vector3 offset = Vector3.up * 0.2f;

            if (_currentMode == NavigationMode.AStar && _currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                Gizmos.color = aStarPathColor;
                Vector3 prev = transform.position + offset;

                for (int i = _currentPathIndex; i < _currentPath.Count; i++)
                {
                    if (_currentPath[i] == null) continue;
                    Vector3 next = _currentPath[i].Position + offset;
                    Gizmos.DrawLine(prev, next);
                    Gizmos.DrawSphere(next, 0.2f);
                    prev = next;
                }
            }
            else if (_currentMode == NavigationMode.Direct)
            {
                Gizmos.color = directLineColor;
                Gizmos.DrawLine(transform.position + offset, _currentDestination + offset);
            }
        }
    }
}
