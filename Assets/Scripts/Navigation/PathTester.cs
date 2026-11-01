using System.Collections.Generic;
using UnityEngine;

namespace PIA1.Navigation
{
    [ExecuteInEditMode]
    public class PathTester : MonoBehaviour
    {
        [Header("Test Nodes")]
        [SerializeField] private Node startNode;
        [SerializeField] private Node targetNode;

        [Header("Pathfinding Configuration")]
        [Tooltip("Si es true, simula la navegación de los lobos excluyendo nodos de Zonas Seguras.")]
        [SerializeField] private bool excludeSafeZones = false;
        [SerializeField] private HeuristicType heuristic = HeuristicType.Euclidean;

        [Header("Runtime Options")]
        [Tooltip("Recalcular automáticamente en Update() para pruebas dinámicas.")]
        [SerializeField] private bool continuousUpdate = false;

        [Header("Gizmos & Visualization")]
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private float pathNodeRadius = 0.25f;

        [Header("Debug Results (Read-Only)")]
        [SerializeField] private bool pathFound = false;
        [SerializeField] private int nodeCount = 0;
        [SerializeField] private float totalDistance = 0f;

        private readonly List<Node> _calculatedPath = new List<Node>();

        public IReadOnlyList<Node> CurrentPath => _calculatedPath;
        public bool PathFound => pathFound;
        public float TotalDistance => totalDistance;

        private void Update()
        {
            if (continuousUpdate && startNode != null && targetNode != null)
            {
                CalculatePath();
            }
        }

        [ContextMenu("Calculate Path")]
        public void CalculatePath()
        {
            if (startNode == null || targetNode == null)
            {
                Debug.LogWarning("[PathTester] Debes asignar startNode y targetNode para calcular la ruta.");
                pathFound = false;
                nodeCount = 0;
                totalDistance = 0f;
                _calculatedPath.Clear();
                return;
            }

            pathFound = Pathfinding.FindPathNonAlloc(startNode, targetNode, _calculatedPath, excludeSafeZones, heuristic);

            if (pathFound)
            {
                nodeCount = _calculatedPath.Count;
                totalDistance = Pathfinding.CalculatePathDistance(_calculatedPath);
                if (!continuousUpdate)
                {
                    Debug.Log($"[PathTester] Camino encontrado: {nodeCount} nodos, Distancia: {totalDistance:F2} m. (excludeSafeZones={excludeSafeZones})");
                }
            }
            else
            {
                nodeCount = 0;
                totalDistance = 0f;
                if (!continuousUpdate)
                {
                    Debug.LogWarning($"[PathTester] No existe camino entre {startNode.name} y {targetNode.name} con excludeSafeZones={excludeSafeZones}.");
                }
            }
        }

        [ContextMenu("Clear Path")]
        public void ClearPath()
        {
            _calculatedPath.Clear();
            pathFound = false;
            nodeCount = 0;
            totalDistance = 0f;
        }

        [ContextMenu("Pick Random Start and Target")]
        public void PickRandomStartAndTarget()
        {
            if (Graph.Instance == null || Graph.Instance.Nodes.Count < 2)
            {
                Debug.LogWarning("[PathTester] No hay suficientes nodos en Graph.Instance.");
                return;
            }

            var allNodes = Graph.Instance.Nodes;
            int idxA = Random.Range(0, allNodes.Count);
            int idxB = Random.Range(0, allNodes.Count);
            while (idxB == idxA)
            {
                idxB = Random.Range(0, allNodes.Count);
            }

            startNode = allNodes[idxA];
            targetNode = allNodes[idxB];
            CalculatePath();
        }

        private void OnDrawGizmos()
        {
            if (_calculatedPath == null || _calculatedPath.Count == 0) return;

            Gizmos.color = pathColor;
            for (int i = 0; i < _calculatedPath.Count - 1; i++)
            {
                Node a = _calculatedPath[i];
                Node b = _calculatedPath[i + 1];

                if (a != null && b != null)
                {
                    Gizmos.DrawLine(a.Position + Vector3.up * 0.1f, b.Position + Vector3.up * 0.1f);
                }
            }

            for (int i = 0; i < _calculatedPath.Count; i++)
            {
                Node n = _calculatedPath[i];
                if (n != null)
                {
                    if (i == 0)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(n.Position + Vector3.up * 0.1f, pathNodeRadius * 1.8f);
                    }
                    else if (i == _calculatedPath.Count - 1)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(n.Position + Vector3.up * 0.1f, pathNodeRadius * 1.8f);
                    }
                    else
                    {
                        Gizmos.color = pathColor;
                        Gizmos.DrawSphere(n.Position + Vector3.up * 0.1f, pathNodeRadius);
                    }
                }
            }
        }
    }
}
