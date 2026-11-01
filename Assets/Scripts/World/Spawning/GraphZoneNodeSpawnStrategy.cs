using System.Collections.Generic;
using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

public class GraphZoneNodeSpawnStrategy : ISpawnPositionStrategy
{
    private readonly ZoneType targetZone;

    public GraphZoneNodeSpawnStrategy(ZoneType zone)
    {
        targetZone = zone;
    }

    public bool TryGetSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;
        ZoneArea zone = ZoneArea.GetZone(targetZone);
        Vector3 refCenter = zone != null ? zone.Bounds.center : Vector3.zero;

        if (Graph.Instance != null)
        {
            Node bestNode = null;
            float bestSqr = float.MaxValue;
            IReadOnlyList<Node> nodes = Graph.Instance.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                Node n = nodes[i];
                if (n == null || !n.IsWalkable || n.Zone != targetZone) continue;

                float sqr = (n.Position - refCenter).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestNode = n;
                }
            }

            if (bestNode != null)
            {
                position = bestNode.Position;
                return true;
            }
        }

        if (zone != null)
        {
            position = zone.Bounds.center;
            return true;
        }

        return false;
    }
}
