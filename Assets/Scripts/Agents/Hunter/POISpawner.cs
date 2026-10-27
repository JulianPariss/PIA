using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente encargado de gestionar la generación periódica de Puntos de Interés (cebos)
/// y mantener un límite estricto de instancias activas simultáneas en la simulación.
/// </summary>
public class POISpawner : MonoBehaviour
{
    [Header("Configuración de Spawn")]
    [SerializeField] private GameObject poiPrefab = null;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxActivePOIs = 5;

    private float timer = 0f;
    private readonly List<PointOfInterest> activePOIs = new List<PointOfInterest>();

    public int ActiveCount
    {
        get
        {
            PurgeNulls();
            return activePOIs.Count;
        }
    }

    public bool CanSpawn => ActiveCount < maxActivePOIs && poiPrefab != null;
    public float SpawnInterval => spawnInterval;
    public float Timer => timer;

    public void Tick(float deltaTime, Vector3 spawnPosition)
    {
        timer += deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            if (CanSpawn)
            {
                TrySpawn(spawnPosition);
            }
        }
    }

    public bool TrySpawn(Vector3 position)
    {
        PurgeNulls();
        if (!CanSpawn) return false;

        GameObject instance = Instantiate(poiPrefab, position, Quaternion.identity);
        if (instance == null) return false;

        if (instance.TryGetComponent<PointOfInterest>(out var poi))
        {
            activePOIs.Add(poi);
            if (poi.Health != null)
            {
                poi.Health.OnDied += () => activePOIs.Remove(poi);
            }
        }

        return true;
    }

    private void PurgeNulls()
    {
        for (int i = activePOIs.Count - 1; i >= 0; i--)
        {
            if (activePOIs[i] == null)
            {
                activePOIs.RemoveAt(i);
            }
        }
    }
}
