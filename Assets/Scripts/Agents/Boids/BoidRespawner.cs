using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la reaparición diferida del Boid.
/// Oculta renderers y colisionadores, espera el delay configurado y reposiciona dentro de SimulationBounds.
/// </summary>
public class BoidRespawner : MonoBehaviour
{
    [Header("Configuración de Respawn")]
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private SimulationBounds bounds;

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

        Vector3 spawnPos = bounds != null
            ? bounds.RandomPoint(transform.position.y)
            : transform.position;

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
