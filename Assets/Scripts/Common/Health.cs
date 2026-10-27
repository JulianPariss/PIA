using System;
using UnityEngine;

/// <summary>
/// Componente reutilizable de salud para agentes y entidades de la simulación.
/// Emite eventos desacoplados al recibir daño o morir.
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Configuración de Salud")]
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;

    public float Current => currentHealth;
    public float Max => maxHealth;
    public float Normalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsAlive => currentHealth > 0f;

    public event Action<float, float> OnDamaged;
    public event Action OnDied;

    private void Awake()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    public void SetMaxHealth(float newMax, bool resetCurrent = true)
    {
        maxHealth = Mathf.Max(1f, newMax);
        if (resetCurrent)
        {
            ResetHealth();
        }
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnDamaged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            OnDied?.Invoke();
        }
    }

    [ContextMenu("Test Take Damage 25")]
    private void TestDamage()
    {
        TakeDamage(25f);
    }
}
