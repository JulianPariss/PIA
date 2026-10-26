using UnityEngine;

/// <summary>
/// Algoritmos estáticos para calcular fuerzas de los Steering Behaviors clásicos (Seek, Flee, Arrive, Pursuit, Evade).
/// </summary>
public static class SteeringBehaviors
{
    /// <summary>
    /// Calcula la velocidad deseada en una dirección normalizada multiplicada por MaxSpeed del agente.
    /// </summary>
    public static Vector3 DesiredVelocity(SteeringAgent agent, Vector3 direction)
    {
        if (agent == null || direction.sqrMagnitude < 0.0001f) return Vector3.zero;
        return direction.normalized * agent.MaxSpeed;
    }

    /// <summary>
    /// Calcula la fuerza de steering requerida hacia la velocidad deseada, limitada por MaxSteering.
    /// </summary>
    public static Vector3 Steer(SteeringAgent agent, Vector3 desiredVelocity)
    {
        if (agent == null) return Vector3.zero;
        Vector3 steeringForce = desiredVelocity - agent.Velocity;
        return Vector3.ClampMagnitude(steeringForce, agent.MaxSteering);
    }

    public static Vector3 Seek(SteeringAgent agent, Vector3 targetPosition)
    {
        if (agent == null) return Vector3.zero;
        Vector3 direction = targetPosition - agent.transform.position;
        Vector3 desiredVelocity = DesiredVelocity(agent, direction);
        return Steer(agent, desiredVelocity);
    }

    public static Vector3 Pursuit(SteeringAgent agent, SteeringAgent target)
    {
        if (agent == null || target == null) return Vector3.zero;
        return Pursuit(agent, target.transform.position, target.Velocity);
    }

    public static Vector3 Pursuit(SteeringAgent agent, Vector3 targetPosition, Vector3 targetVelocity)
    {
        if (agent == null) return Vector3.zero;
        Vector3 toTarget = targetPosition - agent.transform.position;
        float distance = toTarget.magnitude;
        float combinedSpeed = agent.MaxSpeed + targetVelocity.magnitude;
        float lookAheadTime = combinedSpeed > 0.001f ? distance / combinedSpeed : 0f;
        Vector3 futurePosition = targetPosition + (targetVelocity * lookAheadTime);
        return Seek(agent, futurePosition);
    }

    public static Vector3 Flee(SteeringAgent agent, Vector3 targetPosition)
    {
        if (agent == null) return Vector3.zero;
        Vector3 direction = agent.transform.position - targetPosition;
        Vector3 desiredVelocity = DesiredVelocity(agent, direction);
        return Steer(agent, desiredVelocity);
    }

    public static Vector3 Arrive(SteeringAgent agent, Vector3 targetPosition, float slowingRadius = 3f)
    {
        if (agent == null) return Vector3.zero;
        Vector3 toTarget = targetPosition - agent.transform.position;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
        {
            return Steer(agent, Vector3.zero);
        }

        float targetSpeed = (distance < slowingRadius)
            ? agent.MaxSpeed * (distance / slowingRadius)
            : agent.MaxSpeed;

        Vector3 desiredVelocity = (toTarget / distance) * targetSpeed;
        return Steer(agent, desiredVelocity);
    }

    public static Vector3 Arrive(SteeringAgent agent, Transform target, float slowingRadius = 3f)
    {
        if (target == null) return Vector3.zero;
        return Arrive(agent, target.position, slowingRadius);
    }

    public static Vector3 Evade(SteeringAgent agent, SteeringAgent target)
    {
        if (agent == null || target == null) return Vector3.zero;
        return Evade(agent, target.transform.position, target.Velocity);
    }

    public static Vector3 Evade(SteeringAgent agent, Transform target, Vector3 targetVelocity)
    {
        if (target == null) return Vector3.zero;
        return Evade(agent, target.position, targetVelocity);
    }

    public static Vector3 Evade(SteeringAgent agent, Vector3 targetPosition, Vector3 targetVelocity)
    {
        if (agent == null) return Vector3.zero;
        Vector3 toTarget = targetPosition - agent.transform.position;
        float distance = toTarget.magnitude;
        float combinedSpeed = agent.MaxSpeed + targetVelocity.magnitude;
        float lookAheadTime = combinedSpeed > 0.001f ? distance / combinedSpeed : 0f;
        Vector3 futurePosition = targetPosition + (targetVelocity * lookAheadTime);
        return Flee(agent, futurePosition);
    }
}
