/// <summary>
/// Algoritmos estáticos para calcular fuerzas de los Steering Behaviors clásicos (Seek, Flee, Arrive, Pursuit, Evade).
/// </summary>
using UnityEngine;

public static class SteeringBehaviors
{
    public static Vector3 Seek(SteeringAgent agent, Vector3 targetPosition)
    {
        if (agent == null) return Vector3.zero;
        Vector3 desiredVelocity = (targetPosition - agent.transform.position).normalized * agent.maxSpeed;
        Vector3 steeringForce = desiredVelocity - agent.velocity;
        return Vector3.ClampMagnitude(steeringForce, agent.maxForce);
    }

    public static Vector3 Pursuit(SteeringAgent agent, SteeringAgent target)
    {
        if (agent == null || target == null) return Vector3.zero;
        return Pursuit(agent, target.transform.position, target.velocity);
    }

    public static Vector3 Pursuit(SteeringAgent agent, Vector3 targetPosition, Vector3 targetVelocity)
    {
        if (agent == null) return Vector3.zero;
        Vector3 toTarget = targetPosition - agent.transform.position;
        float distance = toTarget.magnitude;
        float combinedSpeed = agent.maxSpeed + targetVelocity.magnitude;
        float lookAheadTime = combinedSpeed > 0.001f ? distance / combinedSpeed : 0f;
        Vector3 futurePosition = targetPosition + (targetVelocity * lookAheadTime);
        return Seek(agent, futurePosition);
    }

    public static Vector3 Flee(SteeringAgent agent, Vector3 targetPosition)
    {
        if (agent == null) return Vector3.zero;
        Vector3 desiredVelocity = (agent.transform.position - targetPosition).normalized * agent.maxSpeed;
        Vector3 steeringForce = desiredVelocity - agent.velocity;
        return Vector3.ClampMagnitude(steeringForce, agent.maxForce);
    }

    public static Vector3 Arrive(SteeringAgent agent, Vector3 targetPosition, float slowingRadius = 3f)
    {
        if (agent == null) return Vector3.zero;
        Vector3 toTarget = targetPosition - agent.transform.position;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
        {
            return Vector3.ClampMagnitude(-agent.velocity, agent.maxForce);
        }

        float targetSpeed = (distance < slowingRadius)
            ? agent.maxSpeed * (distance / slowingRadius)
            : agent.maxSpeed;

        Vector3 desiredVelocity = (toTarget / distance) * targetSpeed;
        Vector3 steeringForce = desiredVelocity - agent.velocity;
        return Vector3.ClampMagnitude(steeringForce, agent.maxForce);
    }

    public static Vector3 Arrive(SteeringAgent agent, Transform target, float slowingRadius = 3f)
    {
        if (target == null) return Vector3.zero;
        return Arrive(agent, target.position, slowingRadius);
    }

    public static Vector3 Evade(SteeringAgent agent, SteeringAgent target)
    {
        if (agent == null || target == null) return Vector3.zero;
        return Evade(agent, target.transform.position, target.velocity);
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
        float combinedSpeed = agent.maxSpeed + targetVelocity.magnitude;
        float lookAheadTime = combinedSpeed > 0.001f ? distance / combinedSpeed : 0f;
        Vector3 futurePosition = targetPosition + (targetVelocity * lookAheadTime);
        return Flee(agent, futurePosition);
    }
}
