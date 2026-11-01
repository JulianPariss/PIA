using System;
using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Navigation
{
    public class Graph : MonoBehaviour
    {
        public static Graph Instance { get; private set; }

        [Header("Nodes in Scene")]
        [SerializeField] private List<Node> nodes = new List<Node>();

        [Header("Auto Connect Configuration")]
        [Tooltip("Distancia máxima para evaluar conexiones entre nodos.")]
        [SerializeField] private float maxConnectionDistance = 10.0f;

        [Tooltip("Radio del haz para comprobar despeje lateral contra muros (SphereCast).")]
        [SerializeField] private float clearanceRadius = 0.25f;

        [Tooltip("Capa de muros y obstáculos físicos.")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("Si está activo, poda conexiones directas largas cuando ya existe un nodo intermedio conectado en línea recta.")]
        [SerializeField] private bool pruneRedundantConnections = true;

        [Tooltip("Cantidad máxima de vecinos recomendada por nodo para evitar marañas densas en salas abiertas (0 = sin límite).")]
        [SerializeField] private int maxNeighborsPerNode = 5;

        private readonly List<Node> _queryBuffer = new List<Node>(128);

        public IReadOnlyList<Node> Nodes => nodes;

        public void RegisterNode(Node node)
        {
            if (node != null && !nodes.Contains(node))
            {
                nodes.Add(node);
            }
        }

        public void UnregisterNode(Node node)
        {
            if (node != null)
            {
                nodes.Remove(node);
            }
        }

        private void Awake()
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

            if (obstacleMask == 0)
            {
                obstacleMask = LayerMask.GetMask("Obstacle");
            }

            if (nodes == null || nodes.Count == 0)
            {
                FindNodesInScene();
            }

            bool anyConnected = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].Neighbors.Count > 0)
                {
                    anyConnected = true;
                    break;
                }
            }

            if (!anyConnected && nodes.Count > 1)
            {
                AutoDetectZonesFromZoneAreas();
                AutoConnectVisibleNeighbors();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public Node GetClosestNode(Vector3 position, bool excludeSafeZones = false)
        {
            Node closest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;

                float sqrDist = (node.Position - position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = node;
                }
            }

            return closest;
        }

        public Node GetClosestVisibleNode(Vector3 position, float eyeHeight = 0.8f, bool excludeSafeZones = false)
        {
            if (obstacleMask == 0)
            {
                obstacleMask = LayerMask.GetMask("Obstacle");
            }

            Node closest = null;
            float minSqrDist = float.MaxValue;
            Vector3 origin = position + Vector3.up * eyeHeight;

            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;

                Vector3 target = node.Position + Vector3.up * eyeHeight;
                if (!LineOfSight.HasLineOfSight(origin, target, obstacleMask, clearanceRadius))
                {
                    continue;
                }

                float sqrDist = (node.Position - position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = node;
                }
            }

            return closest != null ? closest : GetClosestNode(position, excludeSafeZones);
        }

        public Node GetRandomCotoNode()
        {
            _queryBuffer.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node != null && node.IsWalkable && !node.IsSafeZone && node.Zone == ZoneType.Coto)
                {
                    _queryBuffer.Add(node);
                }
            }

            if (_queryBuffer.Count == 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    Node node = nodes[i];
                    if (node != null && node.IsWalkable && !node.IsSafeZone)
                    {
                        _queryBuffer.Add(node);
                    }
                }
            }

            if (_queryBuffer.Count == 0) return null;
            return _queryBuffer[UnityEngine.Random.Range(0, _queryBuffer.Count)];
        }

        public Node GetRandomNodeInZone(ZoneType zone, bool excludeSafeZones = false)
        {
            _queryBuffer.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;
                if (node.Zone == zone)
                {
                    _queryBuffer.Add(node);
                }
            }

            if (_queryBuffer.Count == 0) return null;
            return _queryBuffer[UnityEngine.Random.Range(0, _queryBuffer.Count)];
        }

        public void GetNodesInZoneNonAlloc(ZoneType zone, List<Node> results, bool excludeSafeZones = false)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;
                if (node.Zone == zone)
                {
                    results.Add(node);
                }
            }
        }

        public Node GetClosestNode(Vector3 position, Func<Node, bool> filter)
        {
            Node closest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (filter != null && !filter(node)) continue;

                float sqrDist = (node.Position - position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = node;
                }
            }

            return closest;
        }

        public Node GetRandomNode(Func<Node, bool> filter)
        {
            _queryBuffer.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (filter != null && !filter(node)) continue;
                _queryBuffer.Add(node);
            }

            if (_queryBuffer.Count == 0) return null;
            return _queryBuffer[UnityEngine.Random.Range(0, _queryBuffer.Count)];
        }

        public void GetNodesNonAlloc(Func<Node, bool> filter, List<Node> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (filter != null && !filter(node)) continue;
                results.Add(node);
            }
        }

        #region Editor Utilities

        [ContextMenu("Find All Nodes in Scene")]
        public void FindNodesInScene()
        {
            nodes.Clear();
            Node[] foundNodes = GetComponentsInChildren<Node>(true);

            for (int i = 0; i < foundNodes.Length; i++)
            {
                if (foundNodes[i] != null && !nodes.Contains(foundNodes[i]))
                {
                    nodes.Add(foundNodes[i]);
                }
            }

            MarkGraphDirty();
        }

        [ContextMenu("Auto Connect Visible Neighbors")]
        public void AutoConnectVisibleNeighbors()
        {
            if (nodes == null || nodes.Count == 0)
            {
                FindNodesInScene();
            }

            if (obstacleMask == 0)
            {
                obstacleMask = LayerMask.GetMask("Obstacle");
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Physics.SyncTransforms();
            }
#endif

            AutoDetectZonesFromZoneAreas();

            ClearAllConnections();

            float maxSqrDist = maxConnectionDistance * maxConnectionDistance;

            var candidates = new List<(Node a, Node b, float dist)>();

            for (int i = 0; i < nodes.Count; i++)
            {
                Node a = nodes[i];
                if (a == null || !a.IsWalkable) continue;

                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Node b = nodes[j];
                    if (b == null || !b.IsWalkable) continue;

                    Vector3 diff = b.Position - a.Position;
                    float sqrDist = diff.sqrMagnitude;
                    if (sqrDist > maxSqrDist) continue;

                    float dist = Mathf.Sqrt(sqrDist);

                    Vector3 origin = a.Position + Vector3.up * 0.5f;
                    Vector3 target = b.Position + Vector3.up * 0.5f;

                    if (LineOfSight.HasLineOfSight(origin, target, obstacleMask, clearanceRadius))
                    {
                        candidates.Add((a, b, dist));
                    }
                }
            }

            candidates.Sort((x, y) => x.dist.CompareTo(y.dist));

            for (int k = 0; k < candidates.Count; k++)
            {
                var (a, b, distAB) = candidates[k];

                if (maxNeighborsPerNode > 0)
                {
                    if (a.Neighbors.Count >= maxNeighborsPerNode || b.Neighbors.Count >= maxNeighborsPerNode)
                    {
                        continue;
                    }
                }

                if (pruneRedundantConnections)
                {
                    bool hasIntermediate = false;
                    for (int m = 0; m < nodes.Count; m++)
                    {
                        Node c = nodes[m];
                        if (c == null || c == a || c == b || !c.IsWalkable) continue;

                        if (a.HasNeighbor(c) && b.HasNeighbor(c))
                        {
                            float distAC = Vector3.Distance(a.Position, c.Position);
                            float distCB = Vector3.Distance(c.Position, b.Position);

                            if ((distAC + distCB) <= distAB * 1.10f)
                            {
                                hasIntermediate = true;
                                break;
                            }
                        }
                    }

                    if (hasIntermediate)
                    {
                        continue;
                    }
                }

                a.AddNeighbor(b, bidirectional: true);
            }

            MarkGraphDirty();
        }

        [ContextMenu("Auto Detect Zones From ZoneAreas")]
        public void AutoDetectZonesFromZoneAreas()
        {
            if (nodes == null || nodes.Count == 0)
            {
                FindNodesInScene();
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    nodes[i].AutoDetectZone();
                }
            }

            MarkGraphDirty();
        }

        [ContextMenu("Clear All Connections")]
        public void ClearAllConnections()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    nodes[i].ClearNeighbors();
                }
            }

            MarkGraphDirty();
        }

        private void MarkGraphDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        #endregion
    }
}
