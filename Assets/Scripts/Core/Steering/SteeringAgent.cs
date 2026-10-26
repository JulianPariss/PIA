/// <summary>
/// Agente base que integra físicas elementales de steering (velocidad, fuerzas y rotación en plano XZ).
/// </summary>
using UnityEngine;

public class SteeringAgent : MonoBehaviour
{
    [Header("Parámetros de Movimiento")]
    public float maxSpeed = 5f;
    public float maxForce = 10f;
    public float mass = 1f;

    [Header("Estado")]
    public Vector3 velocity = Vector3.zero;

    [Header("Restricciones")]
    public float rotationSpeed = 10f;
    public bool lockRotationToY = true;
    public bool lockMovementToXZ = true;

    protected Vector3 acceleration = Vector3.zero;
    protected float initialY = 0f;

    protected virtual void Awake()
    {
        initialY = transform.position.y;
    }

    public void ApplyForce(Vector3 force)
    {
        if (lockMovementToXZ)
        {
            force.y = 0f;
        }

        if (mass > 0f)
        {
            acceleration += force / mass;
        }
        else
        {
            acceleration += force;
        }
    }

    protected virtual void Update()
    {
        if (lockMovementToXZ)
        {
            acceleration.y = 0f;
            velocity.y = 0f;
        }

        Vector3 clampedAcceleration = Vector3.ClampMagnitude(acceleration, maxForce);
        velocity += clampedAcceleration * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        transform.position += velocity * Time.deltaTime;

        if (lockMovementToXZ)
        {
            Vector3 currentPos = transform.position;
            currentPos.y = initialY;
            transform.position = currentPos;
        }

        Vector3 lookDirection = velocity;
        if (lockRotationToY)
        {
            lookDirection.y = 0f;
        }

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);

            if (rotationSpeed > 0f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }

        acceleration = Vector3.zero;
    }
}
