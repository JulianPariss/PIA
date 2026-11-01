using UnityEngine;

public static class LineOfSight
{
    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask obstacleMask, float radius = 0f)
    {
        return HasLineOfSight(from, to, obstacleMask, out _, radius);
    }

    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask obstacleMask, out RaycastHit hit, float radius = 0f)
    {
        Vector3 diff = to - from;
        float dist = diff.magnitude;

        if (dist <= 0.0001f)
        {
            hit = default;
            return true;
        }

        if (Physics.Linecast(from, to, out hit, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (radius > 0.001f)
        {
            Vector3 dir = diff / dist;
            float checkDist = Mathf.Max(0.01f, dist - radius * 0.5f);
            if (Physics.SphereCast(from, radius, dir, out hit, checkDist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        hit = default;
        return true;
    }
}
