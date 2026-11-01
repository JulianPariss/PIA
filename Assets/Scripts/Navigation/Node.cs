using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Navigation
{
    [SelectionBase]
    public class Node : MonoBehaviour
    {
        [Header("Graph Connections")]
        [SerializeField] private List<Node> neighbors = new List<Node>();

        [Header("Pathfinding Properties")]
        [SerializeField] private float cost = 1.0f;
        [SerializeField] private bool isWalkable = true;
        [SerializeField] private bool isSafeZone = false;
        [SerializeField] private ZoneType zone = ZoneType.Corridor;

        [Header("Gizmos")]
        [SerializeField] private float gizmoRadius = 0.35f;

        public Vector3 Position => transform.position;
        public IReadOnlyList<Node> Neighbors => neighbors;
        public float Cost => cost;
        public bool IsWalkable { get => isWalkable; set => isWalkable = value; }
        public bool IsSafeZone { get => isSafeZone; set => isSafeZone = value; }
        public ZoneType Zone { get => zone; set => zone = value; }

        private void OnEnable()
        {
            if (Graph.Instance != null)
            {
                Graph.Instance.RegisterNode(this);
            }
        }

        private void OnDisable()
        {
            if (Graph.Instance != null)
            {
                Graph.Instance.UnregisterNode(this);
            }
        }

        public void AddNeighbor(Node neighbor, bool bidirectional = true)
        {
            if (neighbor == null || neighbor == this) return;
            if (!neighbors.Contains(neighbor))
            {
                neighbors.Add(neighbor);
                MarkDirty();
            }
            if (bidirectional && !neighbor.neighbors.Contains(this))
            {
                neighbor.neighbors.Add(this);
                neighbor.MarkDirty();
            }
        }

        public void RemoveNeighbor(Node neighbor, bool bidirectional = true)
        {
            if (neighbor == null) return;
            if (neighbors.Remove(neighbor))
            {
                MarkDirty();
            }
            if (bidirectional && neighbor.neighbors.Remove(this))
            {
                neighbor.MarkDirty();
            }
        }

        public void ClearNeighbors()
        {
            if (neighbors.Count > 0)
            {
                neighbors.Clear();
                MarkDirty();
            }
        }

        public bool HasNeighbor(Node neighbor)
        {
            return neighbors.Contains(neighbor);
        }

        public void AutoDetectZone()
        {
            ZoneArea detected = ZoneArea.GetZoneAt(transform.position);
            if (detected != null)
            {
                zone = detected.Type;
                isSafeZone = (detected.Type == ZoneType.SafeZone);
            }
            else
            {
                zone = ZoneType.Corridor;
                isSafeZone = false;
            }
            MarkDirty();
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GetNodeColor();
            Gizmos.DrawSphere(transform.position, gizmoRadius);

            if (neighbors == null) return;

            Gizmos.color = isSafeZone ? new Color(1f, 0.85f, 0.1f, 0.7f) : new Color(0.7f, 0.3f, 0.9f, 0.6f);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Node neighbor = neighbors[i];
                if (neighbor != null)
                {
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius * 1.5f);

            if (neighbors == null) return;

            Gizmos.color = Color.green;
            for (int i = 0; i < neighbors.Count; i++)
            {
                Node neighbor = neighbors[i];
                if (neighbor != null)
                {
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                    Gizmos.DrawWireSphere(neighbor.transform.position, gizmoRadius * 1.2f);
                }
            }
        }

        private Color GetNodeColor()
        {
            if (!isWalkable) return Color.red;
            if (isSafeZone) return new Color(1f, 0.85f, 0.1f);

            switch (zone)
            {
                case ZoneType.InitialZone:
                    return new Color(0.2f, 0.6f, 1f);
                case ZoneType.Coto:
                    return new Color(0.2f, 0.9f, 0.35f);
                case ZoneType.SafeZone:
                    return new Color(1f, 0.85f, 0.1f);
                case ZoneType.ExitZone:
                    return new Color(0.9f, 0.2f, 0.85f);
                case ZoneType.Corridor:
                default:
                    return new Color(0.75f, 0.3f, 0.95f);
            }
        }
    }
}
