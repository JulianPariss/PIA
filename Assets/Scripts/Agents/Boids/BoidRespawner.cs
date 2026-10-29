using System;
using System.Collections;
using UnityEngine;

public class BoidRespawner : MonoBehaviour
{
    [Header("Configuración de Respawn")]
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private LayerMask obstacleMask = 0;
    [SerializeField] private float checkRadius = 1.0f;
    [SerializeField] private int maxSpawnAttempts = 15;

    private Renderer[] renderers;
    private Collider[] colliders;

    public float RespawnDelay => respawnDelay;
    public bool IsRespawning { get; private set; }

    public event Action OnRespawnStarted;
    public event Action OnRespawnCompleted;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        if (bounds == null)
        {
            bounds = SimulationBounds.Instance;
        }

        if (obstacleMask == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0) obstacleMask = 1 << obstacleLayer;
        }
    }

    public void BeginRespawn(Action onComplete)
    {
        if (IsRespawning) return;

        IsRespawning = true;
        SetComponentsActive(false);
        OnRespawnStarted?.Invoke();

        StartCoroutine(RespawnRoutine(onComplete));
    }

    private IEnumerator RespawnRoutine(Action onComplete)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (bounds == null)
        {
            bounds = SimulationBounds.Instance;
        }

        Vector3 spawnPos = transform.position;
        if (bounds != null)
        {
            float yLevel = transform.position.y;
            bool foundValid = false;

            // Intentar encontrar un punto libre de obstáculos dentro del área
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 candidate = bounds.RandomPoint(yLevel);
                if (obstacleMask == 0 || !Physics.CheckSphere(candidate, checkRadius, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = candidate;
                    foundValid = true;
                    break;
                }
            }

            if (!foundValid)
            {
                Vector3 centerFallback = bounds.Center;
                centerFallback.y = yLevel;
                spawnPos = centerFallback;
            }
        }

        transform.position = spawnPos;
        SetComponentsActive(true);

        IsRespawning = false;
        OnRespawnCompleted?.Invoke();
        onComplete?.Invoke();
    }

    private void SetComponentsActive(bool active)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = active;
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = active;
            }
        }
    }
}
