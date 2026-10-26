/// <summary>
/// Representa un objeto de interés en el escenario. Posee salud propia, se registra en un registro global y es consumido por los Boids.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

public class PointOfInterest : MonoBehaviour
{
    public const int MaxPOIs = 5;
    public static readonly List<PointOfInterest> ActivePOIs = new List<PointOfInterest>();
    public static int ActivePOICount => ActivePOIs.Count;
    public static bool CanSpawnPOI => ActivePOICount < MaxPOIs;

    public float maxHealth = 100f;
    public float currentHealth;

    private bool isRegistered = false;

    private void Awake()
    {
        if (ActivePOICount >= MaxPOIs)
        {
            Destroy(gameObject);
            return;
        }
        currentHealth = maxHealth;
        ActivePOIs.Add(this);
        isRegistered = true;
    }

    private void OnDestroy()
    {
        if (isRegistered)
        {
            ActivePOIs.Remove(this);
            isRegistered = false;
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
