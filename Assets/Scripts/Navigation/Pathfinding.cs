using System.Collections.Generic;
using UnityEngine;

namespace PIA1.Navigation
{
    public enum HeuristicType
    {
        Euclidean,
        Manhattan
    }

    public static class Pathfinding
    {
        private static readonly PriorityQueue<Node> OpenSet = new PriorityQueue<Node>(128);
        private static readonly HashSet<Node> ClosedSet = new HashSet<Node>();
        private static readonly Dictionary<Node, Node> CameFrom = new Dictionary<Node, Node>(128);
        private static readonly Dictionary<Node, float> GScore = new Dictionary<Node, float>(128);
        private static readonly List<Node> SmoothBuffer = new List<Node>(128);

        public static List<Node> FindPath(Node start, Node target, bool excludeSafeZones = false, HeuristicType heuristic = HeuristicType.Euclidean)
        {
            var path = new List<Node>();
            if (FindPathNonAlloc(start, target, path, excludeSafeZones, heuristic))
            {
                return path;
            }
            return null;
        }

        public static bool FindPathNonAlloc(Node start, Node target, List<Node> resultPath, bool excludeSafeZones = false, HeuristicType heuristic = HeuristicType.Euclidean)
        {
            if (resultPath == null) return false;
            resultPath.Clear();

            if (start == null || target == null) return false;
            if (!start.IsWalkable || !target.IsWalkable) return false;

            if (excludeSafeZones && (start.IsSafeZone || target.IsSafeZone))
            {
                return false;
            }

            if (start == target)
            {
                resultPath.Add(start);
                return true;
            }

            OpenSet.Clear();
            ClosedSet.Clear();
            CameFrom.Clear();
            GScore.Clear();

            GScore[start] = 0f;
            float initialH = GetHeuristic(start, target, heuristic);
            OpenSet.Enqueue(start, initialH);

            while (!OpenSet.IsEmpty)
            {
                Node current = OpenSet.Dequeue();

                if (current == target)
                {
                    ReconstructPath(CameFrom, current, resultPath);
                    return true;
                }

                if (!ClosedSet.Add(current))
                {
                    continue;
                }

                float currentG = GScore[current];
                IReadOnlyList<Node> neighbors = current.Neighbors;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Node neighbor = neighbors[i];

                    if (neighbor == null || !neighbor.IsWalkable) continue;
                    if (excludeSafeZones && neighbor.IsSafeZone) continue;
                    if (ClosedSet.Contains(neighbor)) continue;

                    float stepDistance = Vector3.Distance(current.Position, neighbor.Position);
                    float tentativeG = currentG + stepDistance * neighbor.Cost;

                    if (!GScore.TryGetValue(neighbor, out float neighborG) || tentativeG < neighborG)
                    {
                        CameFrom[neighbor] = current;
                        GScore[neighbor] = tentativeG;

                        float h = GetHeuristic(neighbor, target, heuristic);
                        float f = tentativeG + h;

                        OpenSet.Enqueue(neighbor, f);
                    }
                }
            }

            return false;
        }

        private static void ReconstructPath(Dictionary<Node, Node> cameFrom, Node current, List<Node> resultPath)
        {
            resultPath.Add(current);
            while (cameFrom.TryGetValue(current, out Node prev))
            {
                current = prev;
                resultPath.Add(current);
            }
            resultPath.Reverse();
        }

        public static float CalculatePathDistance(IReadOnlyList<Node> path)
        {
            if (path == null || path.Count < 2) return 0f;

            float totalDist = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                totalDist += Vector3.Distance(path[i].Position, path[i + 1].Position);
            }
            return totalDist;
        }

        public static List<Node> SmoothPath(List<Node> path, LayerMask obstacleMask, float agentRadius)
        {
            if (path == null || path.Count < 3)
            {
                return path;
            }

            List<Node> smoothPath = new List<Node>(path.Count);
            smoothPath.Add(path[0]);
            int anchor = 0;

            while (anchor < path.Count - 1)
            {
                for (int i = path.Count - 1; i > anchor; i--)
                {
                    if (i == anchor + 1)
                    {
                        smoothPath.Add(path[i]);
                        anchor = i;
                        break;
                    }

                    if (HasSmoothLineOfSight(path[anchor].Position, path[i].Position, obstacleMask, agentRadius, false))
                    {
                        smoothPath.Add(path[i]);
                        anchor = i;
                        break;
                    }
                }
            }

            return smoothPath;
        }

        public static void SmoothPathNonAlloc(List<Node> path, LayerMask obstacleMask, float agentRadius, bool excludeSafeZones = false)
        {
            if (path == null || path.Count < 3) return;

            SmoothBuffer.Clear();
            SmoothBuffer.Add(path[0]);
            int anchor = 0;

            while (anchor < path.Count - 1)
            {
                for (int i = path.Count - 1; i > anchor; i--)
                {
                    if (i == anchor + 1)
                    {
                        SmoothBuffer.Add(path[i]);
                        anchor = i;
                        break;
                    }

                    if (HasSmoothLineOfSight(path[anchor].Position, path[i].Position, obstacleMask, agentRadius, excludeSafeZones))
                    {
                        SmoothBuffer.Add(path[i]);
                        anchor = i;
                        break;
                    }
                }
            }

            path.Clear();
            for (int k = 0; k < SmoothBuffer.Count; k++)
            {
                path.Add(SmoothBuffer[k]);
            }
        }

        public static bool HasSmoothLineOfSight(Vector3 from, Vector3 to, LayerMask obstacleMask, float agentRadius, bool excludeSafeZones = false)
        {
            Vector3 fromPos = from + Vector3.up * 0.5f;
            Vector3 toPos = to + Vector3.up * 0.5f;
            Vector3 dir = toPos - fromPos;
            float distance = dir.magnitude;
            if (distance <= 0.001f) return true;
            dir /= distance;

            if (excludeSafeZones)
            {
                if (PIA1.World.ZoneArea.IsPositionInSafeZone((from + to) * 0.5f) ||
                    PIA1.World.ZoneArea.IsPositionInSafeZone(to))
                {
                    return false;
                }
            }

            if (agentRadius <= 0.001f)
            {
                return !Physics.Linecast(fromPos, toPos, obstacleMask);
            }

            return !Physics.SphereCast(fromPos, agentRadius, dir, out _, distance, obstacleMask);
        }

        public static float GetHeuristic(Node a, Node b, HeuristicType type)
        {
            Vector3 diff = b.Position - a.Position;

            switch (type)
            {
                case HeuristicType.Manhattan:
                    return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.z);

                case HeuristicType.Euclidean:
                default:
                    return diff.magnitude;
            }
        }
    }
}
