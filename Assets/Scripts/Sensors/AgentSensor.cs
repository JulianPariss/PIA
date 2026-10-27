using System;
using System.Collections.Generic;
using UnityEngine;

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

    public IReadOnlyList<Boid> Boids
    {
        get
        {
            PurgeDeadBoids();
            return detectedBoids;
        }
    }

    public IReadOnlyList<PointOfInterest> POIs
    {
        get
        {
            PurgeDeadPOIs();
            return detectedPOIs;
        }
    }

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

        HunterNPC hunter = other.GetComponent<HunterNPC>() ?? other.GetComponentInParent<HunterNPC>();
        if (hunter != null)
        {
            if (transform.IsChildOf(hunter.transform)) return;

            detectedHunter = hunter;
            return;
        }

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
        Boid boid = other.GetComponent<Boid>() ?? other.GetComponentInParent<Boid>();
        if (boid != null)
        {
            if (detectedBoids.Remove(boid))
            {
                OnBoidExited?.Invoke(boid);
            }
            return;
        }

        HunterNPC hunter = other.GetComponent<HunterNPC>() ?? other.GetComponentInParent<HunterNPC>();
        if (hunter != null)
        {
            if (detectedHunter == hunter)
            {
                detectedHunter = null;
            }
            return;
        }

        PointOfInterest poi = other.GetComponent<PointOfInterest>() ?? other.GetComponentInParent<PointOfInterest>();
        if (poi != null)
        {
            detectedPOIs.Remove(poi);
            return;
        }
    }

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
