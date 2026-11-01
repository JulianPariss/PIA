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

    [Header("Colisión con Muros (Restricción Cinemática)")]
    [Tooltip("Máscara de capas consideradas obstáculos/paredes.")]
    [SerializeField] protected LayerMask obstacleMask = 0;
    [Tooltip("Radio del cuerpo del agente para colisión física con muros.")]
    [SerializeField] protected float bodyRadius = 0.5f;
    [Tooltip("Margen de seguridad para evitar solapamiento numérico.")]
    [SerializeField] protected float skinWidth = 0.05f;
    [Tooltip("Habilita o deshabilita la colisión con muros.")]
    [SerializeField] protected bool enableWallCollision = true;

    protected Vector3 lastSteeringForce = Vector3.zero;
    protected Vector3 acceleration = Vector3.zero;
    protected float initialY = 0f;

    public Vector3 Velocity => velocity;
    public float MaxSpeed { get => maxSpeed; set => maxSpeed = value; }
    public float MaxSteering => maxSteering;
    public float Mass => mass;
    public float RotationSpeed => rotationSpeed;
    public bool LockRotationToY => lockRotationToY;
    public bool LockMovementToXZ => lockMovementToXZ;
    public LayerMask ObstacleMask { get => obstacleMask; set => obstacleMask = value; }
    public float BodyRadius { get => bodyRadius; set => bodyRadius = value; }
    public bool EnableWallCollision { get => enableWallCollision; set => enableWallCollision = value; }

    protected virtual void Awake()
    {
        initialY = transform.position.y;
        if (obstacleMask == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0)
            {
                obstacleMask = 1 << obstacleLayer;
            }
        }
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

    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
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

        Vector3 nextPos = transform.position;
        ResolveWallMovement(ref nextPos, ref velocity, deltaTime);
        transform.position = nextPos;

        acceleration = Vector3.zero;
    }

    protected virtual void ResolveWallMovement(ref Vector3 pos, ref Vector3 vel, float deltaTime)
    {
        if (!enableWallCollision || obstacleMask == 0)
        {
            pos += vel * deltaTime;
            return;
        }

        Vector3 moveDelta = vel * deltaTime;
        if (lockMovementToXZ)
        {
            moveDelta.y = 0f;
        }

        float moveDist = moveDelta.magnitude;
        if (moveDist < 0.0001f) return;

        Vector3 moveDir = moveDelta / moveDist;

        if (Physics.SphereCast(pos, bodyRadius, moveDir, out RaycastHit hit, moveDist + skinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - skinWidth);
            pos += moveDir * safeDistance;

            Vector3 hitNormal = hit.normal;
            if (lockMovementToXZ)
            {
                hitNormal.y = 0f;
                if (hitNormal.sqrMagnitude > 0.001f) hitNormal.Normalize();
            }

            vel = Vector3.ProjectOnPlane(vel, hitNormal);
            if (lockMovementToXZ) vel.y = 0f;

            float remainingRatio = 1f - (safeDistance / (moveDist + skinWidth));
            float remainingTime = remainingRatio * deltaTime;
            if (remainingTime > 0.0001f && vel.sqrMagnitude > 0.001f)
            {
                Vector3 slideDelta = vel * remainingTime;
                float slideDist = slideDelta.magnitude;
                Vector3 slideDir = slideDelta / slideDist;

                if (Physics.SphereCast(pos, bodyRadius, slideDir, out RaycastHit cornerHit, slideDist + skinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    float cornerSafe = Mathf.Max(0f, cornerHit.distance - skinWidth);
                    pos += slideDir * cornerSafe;
                    vel = Vector3.ProjectOnPlane(vel, cornerHit.normal);
                    if (lockMovementToXZ) vel.y = 0f;
                }
                else
                {
                    pos += slideDelta;
                }
            }
        }
        else
        {
            pos += moveDelta;
        }
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

    protected virtual void OnDrawGizmosSelected()
    {
        if (enableWallCollision)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, bodyRadius);
        }
    }
}
