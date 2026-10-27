using UnityEngine;

public class SimulationBounds : MonoBehaviour
{
    public static SimulationBounds Instance { get; private set; }

    [Header("Dimensiones del Área")]
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 size = new Vector3(55f, 1f, 50f);

    public Vector3 Center => center;
    public Vector3 Size => size;

    private void Awake()
    {
        Instance = this;
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
        float rx = Random.Range(-size.x * 0.5f, size.x * 0.5f);
        float rz = Random.Range(-size.z * 0.5f, size.z * 0.5f);
        return new Vector3(center.x + rx, yLevel, center.z + rz);
    }

    public bool Contains(Vector3 position)
    {
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        return position.x >= center.x - halfX && position.x <= center.x + halfX &&
               position.z >= center.z - halfZ && position.z <= center.z + halfZ;
    }

    public Vector3 ComputeContainmentForce(SteeringAgent agent, float boundaryWeight = 1.5f, bool isIsolated = false)
    {
        if (agent == null) return Vector3.zero;

        Vector3 toCenter = center - agent.transform.position;
        toCenter.y = 0f;
        float maxAllowedDist = Mathf.Max(size.x, size.z) * 0.5f;

        if (toCenter.magnitude > maxAllowedDist)
        {
            Vector3 returnForce = SteeringBehaviors.Seek(agent, center);
            returnForce.y = 0f;
            return returnForce * boundaryWeight;
        }
        else if (isIsolated)
        {
            Vector3 gentleReturn = SteeringBehaviors.Seek(agent, center);
            gentleReturn.y = 0f;
            return gentleReturn * (boundaryWeight * 0.4f);
        }

        return Vector3.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.35f);
        Gizmos.DrawWireCube(center, new Vector3(size.x, 1f, size.z));
    }
}
