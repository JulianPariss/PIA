using System.Collections.Generic;
using UnityEngine;

namespace PIA1.Navigation
{
    public enum PatrolMode
    {
        Loop
    }

    public class PatrolRoute : MonoBehaviour
    {
        [Header("Patrol Configuration")]
        [SerializeField] private PatrolMode patrolMode = PatrolMode.Loop;
        [SerializeField] private List<Node> waypoints = new List<Node>();

        [Header("Gizmos")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color routeColor = new Color(1f, 0.5f, 0.1f, 0.9f);

        private int _currentIndex = 0;

        public PatrolMode Mode => patrolMode;
        public IReadOnlyList<Node> Waypoints => waypoints;
        public int Count => waypoints != null ? waypoints.Count : 0;
        public int WaypointCount => Count;
        public int CurrentIndex => _currentIndex;
        public Node CurrentWaypoint => GetWaypoint(_currentIndex);

        public void SetCurrentIndex(int index)
        {
            _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
        }

        public void Advance()
        {
            _currentIndex = GetNextIndex(_currentIndex);
        }

        public Node GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                return null;
            }
            index = Mathf.Clamp(index, 0, waypoints.Count - 1);
            return waypoints[index];
        }

        public int GetNextIndex(int currentIndex)
        {
            if (waypoints == null || waypoints.Count <= 1)
            {
                return 0;
            }

            return (currentIndex + 1) % waypoints.Count;
        }

        public int GetNextIndex(int currentIndex, ref int direction)
        {
            direction = 1;
            return GetNextIndex(currentIndex);
        }

        public int GetClosestWaypointIndex(Vector3 worldPosition)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                return 0;
            }

            int bestIndex = 0;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Node node = waypoints[i];
                if (node == null)
                {
                    continue;
                }

                float sqrDist = (node.Position - worldPosition).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || waypoints == null || waypoints.Count < 2)
            {
                return;
            }

            Gizmos.color = routeColor;
            Vector3 offset = Vector3.up * 0.25f;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Node a = waypoints[i];
                Node b = waypoints[i + 1];
                if (a != null && b != null)
                {
                    Gizmos.DrawLine(a.Position + offset, b.Position + offset);
                    Gizmos.DrawWireCube(a.Position + offset, Vector3.one * 0.3f);
                }
            }

            Node last = waypoints[waypoints.Count - 1];
            if (last != null)
            {
                Gizmos.DrawWireCube(last.Position + offset, Vector3.one * 0.3f);
                if (waypoints[0] != null)
                {
                    Gizmos.DrawLine(last.Position + offset, waypoints[0].Position + offset);
                }
            }
        }
    }
}
