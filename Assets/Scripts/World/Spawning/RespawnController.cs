using System;
using System.Collections;
using UnityEngine;

public class RespawnController : MonoBehaviour
{
    [SerializeField] protected float respawnDelay = 5f;

    protected Renderer[] renderers;
    protected Collider[] colliders;
    protected ISpawnPositionStrategy spawnStrategy;

    public float RespawnDelay { get => respawnDelay; set => respawnDelay = value; }
    public bool IsRespawning { get; protected set; }

    public event Action OnRespawnStarted;
    public event Action OnRespawnCompleted;

    protected virtual void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void SetStrategy(ISpawnPositionStrategy strategy)
    {
        spawnStrategy = strategy;
    }

    public virtual void BeginRespawn(Action onComplete)
    {
        if (IsRespawning) return;

        IsRespawning = true;
        SetComponentsActive(false);
        OnRespawnStarted?.Invoke();

        StartCoroutine(RespawnRoutine(onComplete));
    }

    protected virtual IEnumerator RespawnRoutine(Action onComplete)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (spawnStrategy != null && spawnStrategy.TryGetSpawnPosition(out Vector3 spawnPos))
        {
            spawnPos.y = transform.position.y;
            transform.position = spawnPos;
            Physics.SyncTransforms();
        }

        SetComponentsActive(true);
        IsRespawning = false;
        OnRespawnCompleted?.Invoke();
        onComplete?.Invoke();
    }

    public virtual void SetComponentsActive(bool active)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && !(renderers[i] is LineRenderer))
                {
                    renderers[i].enabled = active;
                }
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = active;
                }
            }
        }
    }
}
