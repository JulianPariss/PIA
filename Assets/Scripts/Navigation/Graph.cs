using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Navigation
{
    /// <summary>
    /// Componente central que gestiona la red de nodos del escenario para el algoritmo A*.
    /// Cumple con la consigna R-07 (grafo propio con nodos y listas de adyacencia, sin matriz ni NavMesh).
    /// </summary>
    public class Graph : MonoBehaviour
    {
        public static Graph Instance { get; private set; }

        [Header("Nodes in Scene")]
        [SerializeField] private List<Node> nodes = new List<Node>();

        [Header("Auto Connect Configuration")]
        [Tooltip("Distancia máxima para evaluar conexiones entre nodos.")]
        [SerializeField] private float maxConnectionDistance = 8.0f;

        [Tooltip("Radio del haz para comprobar despeje lateral contra muros (SphereCast).")]
        [SerializeField] private float clearanceRadius = 0.25f;

        [Tooltip("Capa de muros y obstáculos físicos.")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("Si está activo, poda conexiones directas largas cuando ya existe un nodo intermedio en línea recta.")]
        [SerializeField] private bool pruneRedundantConnections = true;

        [Tooltip("Cantidad máxima de vecinos recomendada por nodo para evitar marañas densas en salas abiertas (0 = sin límite).")]
        [SerializeField] private int maxNeighborsPerNode = 5;

        public IReadOnlyList<Node> Nodes => nodes;

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

            if (nodes == null || nodes.Count == 0)
            {
                FindNodesInScene();
            }

            if (obstacleMask == 0)
            {
                obstacleMask = LayerMask.GetMask("Obstacle");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Obtiene el nodo más cercano a una posición determinada.
        /// </summary>
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

        /// <summary>
        /// Obtiene el nodo más cercano que tenga línea de visión directa despejada (sin chocar con Obstacle).
        /// Si no hay ninguno con visión directa, retorna el nodo más cercano euclidiano.
        /// </summary>
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

            // Fallback si ningún nodo tiene visión directa
            return closest != null ? closest : GetClosestNode(position, excludeSafeZones);
        }

        /// <summary>
        /// Retorna un nodo transitable aleatorio dentro del Coto de Caza (para el estado Search del Cazador).
        /// </summary>
        public Node GetRandomCotoNode()
        {
            var candidates = new List<Node>();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node != null && node.IsWalkable && !node.IsSafeZone && node.Zone == ZoneType.Coto)
                {
                    candidates.Add(node);
                }
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    Node node = nodes[i];
                    if (node != null && node.IsWalkable && !node.IsSafeZone)
                    {
                        candidates.Add(node);
                    }
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// Retorna un nodo transitable aleatorio dentro de una zona específica.
        /// </summary>
        public Node GetRandomNodeInZone(ZoneType zone, bool excludeSafeZones = false)
        {
            var candidates = new List<Node>();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;
                if (node.Zone == zone)
                {
                    candidates.Add(node);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// Obtiene todos los nodos asociados a una zona determinada.
        /// </summary>
        public List<Node> GetNodesInZone(ZoneType zone, bool excludeSafeZones = false)
        {
            var result = new List<Node>();
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node == null || !node.IsWalkable) continue;
                if (excludeSafeZones && node.IsSafeZone) continue;
                if (node.Zone == zone)
                {
                    result.Add(node);
                }
            }
            return result;
        }

        #region Editor Utilities

        [ContextMenu("Find All Nodes in Scene")]
        public void FindNodesInScene()
        {
            nodes.Clear();
            Node[] foundNodes = GetComponentsInChildren<Node>();
            if (foundNodes.Length == 0)
            {
                foundNodes = FindObjectsByType<Node>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < foundNodes.Length; i++)
            {
                if (foundNodes[i] != null && !nodes.Contains(foundNodes[i]))
                {
                    nodes.Add(foundNodes[i]);
                }
            }

            Debug.Log($"[Graph] Se registraron {nodes.Count} nodos en el grafo.");
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

            // Limpiamos conexiones previas para generar un grafo limpio y consistente
            ClearAllConnections();

            int connectionsCreated = 0;
            float maxSqrDist = maxConnectionDistance * maxConnectionDistance;

            // Recopilamos pares candidatos válidos
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

                    // Altura del rayo: en el centro vertical de los agentes / muros
                    Vector3 origin = a.Position + Vector3.up * 0.5f;
                    Vector3 target = b.Position + Vector3.up * 0.5f;

                    if (LineOfSight.HasLineOfSight(origin, target, obstacleMask, clearanceRadius))
                    {
                        candidates.Add((a, b, dist));
                    }
                }
            }

            // Ordenamos candidatos de menor a mayor distancia
            // Prioriza tramos cortos y directos frente a saltos lejanos
            candidates.Sort((x, y) => x.dist.CompareTo(y.dist));

            for (int k = 0; k < candidates.Count; k++)
            {
                var (a, b, distAB) = candidates[k];

                // Comprobar límite de vecinos por nodo si está configurado
                if (maxNeighborsPerNode > 0)
                {
                    if (a.Neighbors.Count >= maxNeighborsPerNode || b.Neighbors.Count >= maxNeighborsPerNode)
                    {
                        continue;
                    }
                }

                // Poda de conexiones redundantes: si existe un nodo 'c' que está casi en la recta entre 'a' y 'b',
                // evitamos el salto largo redundante (a-b) ya que el camino natural transita por 'c'.
                if (pruneRedundantConnections)
                {
                    bool hasIntermediate = false;
                    for (int m = 0; m < nodes.Count; m++)
                    {
                        Node c = nodes[m];
                        if (c == null || c == a || c == b || !c.IsWalkable) continue;

                        float distAC = Vector3.Distance(a.Position, c.Position);
                        float distCB = Vector3.Distance(c.Position, b.Position);

                        // Si c se encuentra entre a y b en línea casi recta (tolerancia 8%)
                        if (distAC < distAB && distCB < distAB && (distAC + distCB) <= distAB * 1.08f)
                        {
                            hasIntermediate = true;
                            break;
                        }
                    }

                    if (hasIntermediate)
                    {
                        continue;
                    }
                }

                a.AddNeighbor(b, bidirectional: true);
                connectionsCreated++;
            }

            Debug.Log($"[Graph] AutoConnect finalizado. Conexiones bidireccionales creadas: {connectionsCreated}");
        }

        [ContextMenu("Auto Detect Zones From ZoneAreas")]
        public void AutoDetectZonesFromZoneAreas()
        {
            if (nodes == null || nodes.Count == 0)
            {
                FindNodesInScene();
            }

            int updated = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    nodes[i].AutoDetectZone();
                    updated++;
                }
            }

            Debug.Log($"[Graph] Zonas autodetectadas para {updated} nodos.");
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

            Debug.Log("[Graph] Todas las conexiones entre nodos han sido eliminadas.");
        }

        #endregion
    }
}
