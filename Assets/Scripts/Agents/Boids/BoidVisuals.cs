using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(Boid))]
public class BoidVisuals : MonoBehaviour
{
    [Header("Colores")]
    [SerializeField] private Color healthyColor = new Color(0.35f, 0.65f, 0.95f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.25f, 0.25f);
    [SerializeField] private Color evadingColor = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Color deadColor = Color.gray;
    [SerializeField] private Color damageFlashColor = Color.white;

    private Health health;
    private Boid boid;
    private Renderer[] meshRenderers;
    private MaterialPropertyBlock propBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float flashTimer = 0f;
    private const float FlashDuration = 0.12f;

    private void Awake()
    {
        health = GetComponent<Health>();
        boid = GetComponent<Boid>();
        meshRenderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();

        if (meshRenderers.Length > 0 && meshRenderers[0].sharedMaterial != null)
        {
            if (meshRenderers[0].sharedMaterial.HasProperty(BaseColorId))
            {
                healthyColor = meshRenderers[0].sharedMaterial.GetColor(BaseColorId);
            }
            else if (meshRenderers[0].sharedMaterial.HasProperty(ColorId))
            {
                healthyColor = meshRenderers[0].sharedMaterial.GetColor(ColorId);
            }
        }

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void Update()
    {
        UpdateVisuals();
    }

    private void HandleDamaged(float current, float max)
    {
        flashTimer = FlashDuration;
    }

    private void HandleDied()
    {
        ApplyColor(deadColor);
    }

    private void UpdateVisuals()
    {
        if (boid == null || meshRenderers == null || meshRenderers.Length == 0) return;

        if (!boid.IsAlive)
        {
            ApplyColor(deadColor);
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            ApplyColor(damageFlashColor);
            return;
        }

        if (boid.IsEvading)
        {
            ApplyColor(evadingColor);
            return;
        }

        float normalizedHp = health != null ? health.Normalized : 1f;
        Color currentColor = Color.Lerp(criticalColor, healthyColor, normalizedHp);
        ApplyColor(currentColor);
    }

    public void ApplyColor(Color color)
    {
        if (meshRenderers == null) return;

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            Renderer rend = meshRenderers[i];
            if (rend == null) continue;

            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(ColorId, color);
            rend.SetPropertyBlock(propBlock);
        }
    }
}
