using UnityEngine;

/// <summary>
/// Objeto de interés en el escenario. Posee salud administrada por el componente Health,
/// es detectado localmente por los sensores de los Boids y se destruye al agotarse su vida.
/// </summary>
[RequireComponent(typeof(Health))]
public class PointOfInterest : MonoBehaviour
{
    [Header("Componente de Salud")]
    [SerializeField] private Health health;

    public Health Health => health;
    public float CurrentHealth => health != null ? health.Current : 0f;
    public float MaxHealth => health != null ? health.Max : 100f;
    public float currentHealth => CurrentHealth;
    public float maxHealth => MaxHealth;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.OnDied += HandleDied;
        }
        if (!TryGetComponent<WorldHealthBar>(out _))
        {
            gameObject.AddComponent<WorldHealthBar>();
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
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
