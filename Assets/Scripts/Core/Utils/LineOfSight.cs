using UnityEngine;

/// <summary>
/// Utilidad estática para comprobación de Línea de Visión (LOS) física contra obstáculos.
/// Proporciona métodos optimizados con 0 asignaciones de memoria (GC Free)
/// utilizando Linecast o SphereCast según se requiera evaluar volumen o solo rayo.
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// Comprueba si existe línea de visión despejada entre dos puntos en el espacio.
    /// </summary>
    /// <param name="from">Punto de origen (e.g. posición de ojos).</param>
    /// <param name="to">Punto de destino (e.g. centro del objetivo).</param>
    /// <param name="obstacleMask">Máscara de capas considerada como obstáculos (e.g. Obstacle).</param>
    /// <param name="radius">Radio del haz. Si es <= 0 usa Linecast (rayo fino). Si es > 0 usa SphereCast.</param>
    /// <returns>True si no hay colisión con ningún obstáculo; False si la visión está bloqueada.</returns>
    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask obstacleMask, float radius = 0f)
    {
        return HasLineOfSight(from, to, obstacleMask, out _, radius);
    }

    /// <summary>
    /// Comprueba si existe línea de visión despejada entre dos puntos y devuelve el impacto si se obstruye.
    /// </summary>
    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask obstacleMask, out RaycastHit hit, float radius = 0f)
    {
        Vector3 diff = to - from;
        float dist = diff.magnitude;

        if (dist <= 0.0001f)
        {
            hit = default;
            return true;
        }

        // 1. Rayo lineal directo: SIEMPRE se evalúa primero para asegurar que no atraviesa obstáculos sólidos
        if (Physics.Linecast(from, to, out hit, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // 2. Si se solicitó radio de despeje, verificar volumen cilíndrico con SphereCast
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
