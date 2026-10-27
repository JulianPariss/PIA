using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Objeto de interés en el escenario. Posee salud administrada por el componente Health,
/// se registra en la lista de activos y es consumido por los Boids.
/// </summary>
[RequireComponent(typeof(Health))]
public class PointOfInterest : MonoBehaviour
{
    public const int MaxPOIs = 5;
    public static readonly List<PointOfInterest> ActivePOIs = new List<PointOfInterest>();
    public static int ActivePOICount => ActivePOIs.Count;
    public static bool CanSpawnPOI => ActivePOICount < MaxPOIs;

    [Header("Componente de Salud")]
    [SerializeField] private Health health;

    public Health Health => health;
    public float currentHealth => health != null ? health.Current : 0f;
    public float maxHealth => health != null ? health.Max : 100f;

    private bool isRegistered = false;

    private void Awake()
    {
        if (ActivePOICount >= MaxPOIs)
        {
            Destroy(gameObject);
            return;
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.OnDied += HandleDied;
        }

        ActivePOIs.Add(this);
        isRegistered = true;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }

        if (isRegistered)
        {
            ActivePOIs.Remove(this);
            isRegistered = false;
        }
    }

    public void TakeDamage(float amount)
    {
        if (health != null)
        {
            health.TakeDamage(amount);
        }
    }

    private void HandleDied()
    {
        Destroy(gameObject);
    }
}
