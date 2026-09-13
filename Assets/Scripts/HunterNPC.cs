/// <summary>
/// Controlador principal del NPC Cazador. Administra la FSM, variables de ataque, generación de POIs, gizmos de percepción/ataque y cambios de color según el estado activo.
/// </summary>
using System;
using UnityEngine;

[RequireComponent(typeof(HunterFSM))]
[RequireComponent(typeof(SteeringAgent))]
public class HunterNPC : MonoBehaviour
{
    [Header("Parámetros de Ataque")]
    public float TBA = 2.5f;
    [HideInInspector] public float lastAttackTime = 0f;
    public bool IsTBAReady => Time.time >= lastAttackTime + TBA;

    public float RangeAttackRadius = 10f;
    public float MeleeAttackRadius = 2.5f;

    [Header("Ruta de Patrullaje")]
    public Transform[] waypoints;

    [Header("Prefabs y Spawn de POI")]
    public GameObject poiPrefab;
    public GameObject[] poiPrefabs;
    public float poiSpawnInterval = 5f;
    [HideInInspector] public float poiTimer = 0f;

    [Header("Detección")]
    public string targetTag = "Boid";
    public float visionRadius = 15f;

    public HunterFSM FSM { get; private set; }
    public SteeringAgent Agent { get; private set; }

    private int currentWaypointIndex = 0;
    private Renderer[] meshRenderers;

    protected virtual void Awake()
    {
        FSM = GetComponent<HunterFSM>();
        Agent = GetComponent<SteeringAgent>();
        meshRenderers = GetComponentsInChildren<Renderer>();

        if (!CompareTag("Hunter"))
        {
            try { gameObject.tag = "Hunter"; } catch (Exception) { }
        }
    }

    protected virtual void Start()
    {
        lastAttackTime = Time.time;
        poiTimer = poiSpawnInterval * 0.6f;

        if (FSM != null)
        {
            FSM.OnStateChanged += HandleStateChanged;
            if (FSM.CurrentState == null)
            {
                FSM.ChangeState(new PatrolState(this), force: true);
            }
            else
            {
                UpdateStateColor(FSM.CurrentState);
            }
        }
    }

    private void OnDestroy()
    {
        if (FSM != null)
        {
            FSM.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(IState previousState, IState newState)
    {
        UpdateStateColor(newState);
    }

    private void UpdateStateColor(IState state)
    {
        if (state == null || meshRenderers == null) return;

        Color targetColor = Color.white;
        if (state is PatrolState)
        {
            targetColor = Color.green;
        }
        else if (state is AttackState)
        {
            targetColor = Color.red;
        }
        else if (state is GatherState)
        {
            targetColor = Color.yellow;
        }

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].material != null)
            {
                meshRenderers[i].material.color = targetColor;
            }
        }
    }

    public GameObject SpawnPOI(Vector3 position)
    {
        if (!PointOfInterest.CanSpawnPOI) return null;

        GameObject prefabToInstantiate = poiPrefab;
        if (prefabToInstantiate == null && poiPrefabs != null && poiPrefabs.Length > 0)
        {
            prefabToInstantiate = poiPrefabs[UnityEngine.Random.Range(0, poiPrefabs.Length)];
        }

        if (prefabToInstantiate == null) return null;

        return Instantiate(prefabToInstantiate, position, Quaternion.identity);
    }

    public Transform GetCurrentWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return null;

        int checkedCount = 0;
        while (checkedCount < waypoints.Length)
        {
            if (currentWaypointIndex >= waypoints.Length) currentWaypointIndex = 0;
            if (waypoints[currentWaypointIndex] != null)
            {
                return waypoints[currentWaypointIndex];
            }
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            checkedCount++;
        }

        return null;
    }

    public void AdvanceToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, RangeAttackRadius);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRadius);

        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                int nextIndex = (i + 1) % waypoints.Length;
                if (waypoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                }
            }
        }
    }
}
