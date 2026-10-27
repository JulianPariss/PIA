using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sensor basado en triggers físicos para agentes autónomos.
/// Resuelve y almacena componentes una sola vez en eventos de colisión (OnTriggerEnter/Exit),
/// eliminando Physics.OverlapSphere y llamadas continuas a GetComponent en Update.
/// </summary>
public class AgentSensor : MonoBehaviour
{
    [Header("Configuración de Detección")]
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private SphereCollider sensorCollider;

    private readonly List<Boid> detectedBoids = new List<Boid>();
    private readonly List<PointOfInterest> detectedPOIs = new List<PointOfInterest>();
    private HunterNPC detectedHunter;

    public event Action<Boid> OnBoidEntered;
    public event Action<Boid> OnBoidExited;

    /// <summary>
    /// Lista de Boids actualmente dentro del volumen del sensor (excluye instancias destruidas o inactivas).
    /// </summary>
    public IReadOnlyList<Boid> Boids
    {
        get
        {
            PurgeDeadBoids();
            return detectedBoids;
        }
    }

    /// <summary>
    /// Lista de POIs actualmente dentro del volumen del sensor (excluye instancias destruidas, inactivas o sin vida).
    /// </summary>
    public IReadOnlyList<PointOfInterest> POIs
    {
        get
        {
            PurgeDeadPOIs();
            return detectedPOIs;
        }
    }

    /// <summary>
    /// Referencia al Cazador si está actualmente en el sensor y activo.
    /// </summary>
    public HunterNPC Hunter
    {
        get
        {
            if (detectedHunter == null || !detectedHunter.gameObject.activeInHierarchy)
            {
                detectedHunter = null;
            }
            return detectedHunter;
        }
    }

    /// <summary>
    /// Radio actual del SphereCollider del sensor.
    /// </summary>
    public float Radius
    {
        get => sensorCollider != null ? sensorCollider.radius : 0f;
        set => SetRadius(value);
    }

    private void Awake()
    {
        if (sensorCollider == null)
        {
            sensorCollider = GetComponent<SphereCollider>();
        }

        if (sensorCollider != null)
        {
            sensorCollider.isTrigger = true;
        }
    }

    /// <summary>
    /// Sincroniza dinámicamente el radio del SphereCollider del sensor con el rango del agente.
    /// </summary>
    public void SetRadius(float radius)
    {
        if (sensorCollider == null)
        {
            sensorCollider = GetComponent<SphereCollider>();
        }

        if (sensorCollider != null)
        {
            sensorCollider.radius = radius;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & detectionMask) == 0) return;

        // 1. Detección de Boid
        Boid boid = other.GetComponent<Boid>() ?? other.GetComponentInParent<Boid>();
        if (boid != null)
        {
            if (transform.IsChildOf(boid.transform)) return;

            if (!detectedBoids.Contains(boid))
            {
                detectedBoids.Add(boid);
                OnBoidEntered?.Invoke(boid);
            }
            return;
        }

        // 2. Detección de Cazador
        HunterNPC hunter = other.GetComponent<HunterNPC>() ?? other.GetComponentInParent<HunterNPC>();
        if (hunter != null)
        {
            if (transform.IsChildOf(hunter.transform)) return;

            detectedHunter = hunter;
            return;
        }

        // 3. Detección de POI
        PointOfInterest poi = other.GetComponent<PointOfInterest>() ?? other.GetComponentInParent<PointOfInterest>();
        if (poi != null)
        {
            if (!detectedPOIs.Contains(poi))
            {
                detectedPOIs.Add(poi);
            }
            return;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 1. Salida de Boid
        Boid boid = other.GetComponent<Boid>() ?? other.GetComponentInParent<Boid>();
        if (boid != null)
        {
            if (detectedBoids.Remove(boid))
            {
                OnBoidExited?.Invoke(boid);
            }
            return;
        }

        // 2. Salida de Cazador
        HunterNPC hunter = other.GetComponent<HunterNPC>() ?? other.GetComponentInParent<HunterNPC>();
        if (hunter != null)
        {
            if (detectedHunter == hunter)
            {
                detectedHunter = null;
            }
            return;
        }

        // 3. Salida de POI
        PointOfInterest poi = other.GetComponent<PointOfInterest>() ?? other.GetComponentInParent<PointOfInterest>();
        if (poi != null)
        {
            detectedPOIs.Remove(poi);
            return;
        }
    }

    /// <summary>
    /// Limpia todas las listas y referencias detectadas por el sensor.
    /// </summary>
    public void Clear()
    {
        detectedBoids.Clear();
        detectedPOIs.Clear();
        detectedHunter = null;
    }

    private void PurgeDeadBoids()
    {
        for (int i = detectedBoids.Count - 1; i >= 0; i--)
        {
            Boid b = detectedBoids[i];
            if (b == null || !b.gameObject.activeInHierarchy || b.isRespawning)
            {
                detectedBoids.RemoveAt(i);
            }
        }
    }

    private void PurgeDeadPOIs()
    {
        for (int i = detectedPOIs.Count - 1; i >= 0; i--)
        {
            PointOfInterest poi = detectedPOIs[i];
            if (poi == null || !poi.gameObject.activeInHierarchy || poi.currentHealth <= 0f)
            {
                detectedPOIs.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        float r = sensorCollider != null ? sensorCollider.radius : 1f;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
