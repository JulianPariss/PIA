using UnityEngine;
using PIA1.World;

public enum BoundaryMode
{
    BounceClamp = 0,
    WrapAround = 1
}

public class SimulationBounds : MonoBehaviour
{
    public static SimulationBounds Instance { get; private set; }

    [Header("Dimensiones del Área")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(17.4f, 1f, 23.8f);

    [Header("Comportamiento de Contención")]
    [Tooltip("Modo de contención al alcanzar el borde del Coto.")]
    [SerializeField] private BoundaryMode boundaryMode = BoundaryMode.WrapAround;

    [Tooltip("Distancia interior al borde (en metros) donde comienza a actuar la fuerza de contención.")]
    [SerializeField] private float softMargin = 2.2f;

    [Tooltip("Si está activo, reposiciona inmediatamente al interior del Coto a cualquier Boid que sea detectado fuera.")]
    [SerializeField] private bool enableEmergencyRescue = true;

    [Header("Visualización")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color boundsColor = new Color(0.1f, 0.95f, 0.35f, 0.45f);

    public Vector3 Center => center;
    public Vector3 Size => size;
    public BoundaryMode Mode { get => boundaryMode; set => boundaryMode = value; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (ZoneArea.AllZones != null && ZoneArea.AllZones.Count > 0)
        {
            ZoneArea coto = ZoneArea.GetZone(ZoneType.Coto);
            if (coto != null && coto.Collider != null)
            {
                center = coto.Bounds.center;
                size = coto.Bounds.size;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Vector3 RandomPoint(float yLevel = 0f)
    {
        float safeHalfX = Mathf.Max(0.5f, (size.x * 0.5f) - 1.2f);
        float safeHalfZ = Mathf.Max(0.5f, (size.z * 0.5f) - 1.2f);
        float rx = Random.Range(-safeHalfX, safeHalfX);
        float rz = Random.Range(-safeHalfZ, safeHalfZ);
        return new Vector3(center.x + rx, yLevel, center.z + rz);
    }

    public bool Contains(Vector3 position)
    {
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        return position.x >= center.x - halfX && position.x <= center.x + halfX &&
               position.z >= center.z - halfZ && position.z <= center.z + halfZ;
    }

    public Vector3 ComputeContainmentForce(SteeringAgent agent, float boundaryWeight = 1.8f, bool isIsolated = false)
    {
        if (agent == null) return Vector3.zero;

        Vector3 pos = agent.transform.position;
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        float minX = center.x - halfX;
        float maxX = center.x + halfX;
        float minZ = center.z - halfZ;
        float maxZ = center.z + halfZ;

        if (pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ)
        {
            Vector3 directSeek = SteeringBehaviors.Seek(agent, center);
            directSeek.y = 0f;
            return directSeek * (boundaryWeight * 2.5f);
        }

        Vector3 steeringForce = Vector3.zero;

        float distToMinX = pos.x - minX;
        float distToMaxX = maxX - pos.x;

        if (distToMinX < softMargin)
        {
            float factor = 1f - (distToMinX / softMargin);
            steeringForce += Vector3.right * (factor * factor);
        }
        else if (distToMaxX < softMargin)
        {
            float factor = 1f - (distToMaxX / softMargin);
            steeringForce += Vector3.left * (factor * factor);
        }

        float distToMinZ = pos.z - minZ;
        float distToMaxZ = maxZ - pos.z;

        if (distToMinZ < softMargin)
        {
            float factor = 1f - (distToMinZ / softMargin);
            steeringForce += Vector3.forward * (factor * factor);
        }
        else if (distToMaxZ < softMargin)
        {
            float factor = 1f - (distToMaxZ / softMargin);
            steeringForce += Vector3.back * (factor * factor);
        }

        if (steeringForce.sqrMagnitude > 0.0001f)
        {
            Vector3 desiredVelocity = SteeringBehaviors.DesiredVelocity(agent, steeringForce);
            desiredVelocity.y = 0f;
            return SteeringBehaviors.Steer(agent, desiredVelocity) * boundaryWeight;
        }

        if (isIsolated)
        {
            Vector3 gentleSeek = SteeringBehaviors.Seek(agent, center);
            gentleSeek.y = 0f;
            return gentleSeek * (boundaryWeight * 0.35f);
        }

        return Vector3.zero;
    }

    public void EnforceContainment(Boid boid)
    {
        if (boid == null || !boid.IsAlive) return;

        Vector3 pos = boid.transform.position;

        if (enableEmergencyRescue && !Contains(pos))
        {
            ZoneArea currentZone = ZoneArea.GetZoneAt(pos);
            if (currentZone == null || currentZone.Type != ZoneType.Coto)
            {
                Vector3 safeSpawn = RandomPoint(pos.y);
                boid.transform.position = safeSpawn;
                boid.ResetMotion();
                Physics.SyncTransforms();
                return;
            }
        }

        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        float margin = 0.35f;
        float minX = center.x - halfX + margin;
        float maxX = center.x + halfX - margin;
        float minZ = center.z - halfZ + margin;
        float maxZ = center.z + halfZ - margin;

        Vector3 vel = boid.Velocity;
        bool modified = false;

        if (boundaryMode == BoundaryMode.WrapAround)
        {
            if (pos.x > maxX)
            {
                pos.x = minX + 0.5f;
                modified = true;
            }
            else if (pos.x < minX)
            {
                pos.x = maxX - 0.5f;
                modified = true;
            }

            if (pos.z > maxZ)
            {
                pos.z = minZ + 0.5f;
                modified = true;
            }
            else if (pos.z < minZ)
            {
                pos.z = maxZ - 0.5f;
                modified = true;
            }
        }
        else
        {
            if (pos.x > maxX)
            {
                pos.x = maxX;
                if (vel.x > 0f) vel.x = -vel.x * 0.5f;
                modified = true;
            }
            else if (pos.x < minX)
            {
                pos.x = minX;
                if (vel.x < 0f) vel.x = -vel.x * 0.5f;
                modified = true;
            }

            if (pos.z > maxZ)
            {
                pos.z = maxZ;
                if (vel.z > 0f) vel.z = -vel.z * 0.5f;
                modified = true;
            }
            else if (pos.z < minZ)
            {
                pos.z = minZ;
                if (vel.z < 0f) vel.z = -vel.z * 0.5f;
                modified = true;
            }
        }

        if (modified)
        {
            boid.transform.position = pos;
            Physics.SyncTransforms();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(center, new Vector3(size.x, 1f, size.z));

        Color innerColor = boundsColor;
        innerColor.a *= 0.5f;
        Gizmos.color = innerColor;
        float innerX = Mathf.Max(0.5f, size.x - softMargin * 2f);
        float innerZ = Mathf.Max(0.5f, size.z - softMargin * 2f);
        Gizmos.DrawWireCube(center, new Vector3(innerX, 1f, innerZ));
    }
}
