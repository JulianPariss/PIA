using UnityEngine;
using UnityEngine.Serialization;

public class SteeringAgent : MonoBehaviour
{
    [Header("Parámetros de Movimiento")]
    [SerializeField] private float maxSpeed = 5f;
    [FormerlySerializedAs("maxForce")]
    [SerializeField] private float maxSteering = 10f;
    [SerializeField] private float mass = 1f;

    [Header("Estado")]
    [SerializeField] protected Vector3 velocity = Vector3.zero;

    [Header("Restricciones")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] protected bool lockRotationToY = true;
    [SerializeField] protected bool lockMovementToXZ = true;
    [SerializeField] protected bool showDebugRays = true;

    protected Vector3 lastSteeringForce = Vector3.zero;
    protected Vector3 acceleration = Vector3.zero;
    protected float initialY = 0f;

    public Vector3 Velocity => velocity;
    public float MaxSpeed => maxSpeed;
    public float MaxSteering => maxSteering;
    public float Mass => mass;
    public float RotationSpeed => rotationSpeed;
    public bool LockRotationToY => lockRotationToY;
    public bool LockMovementToXZ => lockMovementToXZ;

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

    public void Stop()
    {
        if (velocity.sqrMagnitude < 0.0001f)
        {
            velocity = Vector3.zero;
            return;
        }

        Vector3 desiredVelocity = Vector3.zero;
        Vector3 steeringForce = desiredVelocity - velocity;
        ApplyForce(Vector3.ClampMagnitude(steeringForce, maxSteering));
    }

    public void ResetMotion()
    {
        velocity = Vector3.zero;
        acceleration = Vector3.zero;
    }

    protected virtual void Update()
    {
        IntegrateForces(Time.deltaTime);
        ClampToPlane();
        FaceVelocity(Time.deltaTime);

        if (showDebugRays)
        {
            Debug.DrawRay(transform.position, velocity, Color.cyan);
            Debug.DrawRay(transform.position + Vector3.up * 0.1f, lastSteeringForce, Color.yellow);
        }
    }

    protected virtual void IntegrateForces(float deltaTime)
    {
        if (lockMovementToXZ)
        {
            acceleration.y = 0f;
            velocity.y = 0f;
        }

        Vector3 clampedAcceleration = Vector3.ClampMagnitude(acceleration, maxSteering);
        lastSteeringForce = clampedAcceleration;
        velocity += clampedAcceleration * deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        transform.position += velocity * deltaTime;

        acceleration = Vector3.zero;
    }

    protected virtual void ClampToPlane()
    {
        if (lockMovementToXZ)
        {
            Vector3 currentPos = transform.position;
            currentPos.y = initialY;
            transform.position = currentPos;
        }
    }

    protected virtual void FaceVelocity(float deltaTime)
    {
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }
    }
}
