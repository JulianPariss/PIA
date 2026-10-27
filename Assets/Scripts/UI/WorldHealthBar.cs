using UnityEngine;

/// <summary>
/// Barra de salud proyectada en el espacio del mundo para Boids y Puntos de Interés (POIs).
/// Se posiciona sobre el agente, se orienta hacia la cámara y refleja en tiempo real el valor de Health.
/// Se oculta automáticamente cuando la salud llega a 0 o el objeto está inactivo.
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Health health;
    [SerializeField] private float heightOffset = 1.3f;
    [SerializeField] private float barWidth = 46f;
    [SerializeField] private float barHeight = 6f;
    [SerializeField] private bool showBorder = true;

    private static Texture2D bgTexture;
    private static Texture2D fillTexture;
    private static Texture2D borderTexture;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>() ?? GetComponentInParent<Health>();
        }

        InitTextures();
    }

    private static void InitTextures()
    {
        if (bgTexture == null)
        {
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 0.75f));
            bgTexture.Apply();
        }

        if (fillTexture == null)
        {
            fillTexture = new Texture2D(1, 1);
            fillTexture.SetPixel(0, 0, Color.white);
            fillTexture.Apply();
        }

        if (borderTexture == null)
        {
            borderTexture = new Texture2D(1, 1);
            borderTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.9f));
            borderTexture.Apply();
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (health == null || !health.IsAlive || !gameObject.activeInHierarchy) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 worldPos = transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0.5f) return; // Detrás de la cámara

        float x = screenPos.x - (barWidth * 0.5f);
        float y = Screen.height - screenPos.y - (barHeight * 0.5f);

        // Borde
        if (showBorder)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x - 1, y - 1, barWidth + 2, barHeight + 2), borderTexture);
        }

        // Fondo oscuro
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), bgTexture);

        // Relleno de salud con degradado Verde -> Amarillo -> Rojo
        float normalized = Mathf.Clamp01(health.Normalized);
        float fillW = barWidth * normalized;

        Color barColor = Color.Lerp(new Color(0.95f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 0.3f), normalized);
        if (normalized > 0.3f && normalized < 0.6f)
        {
            barColor = Color.Lerp(new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.1f), (normalized - 0.3f) / 0.3f);
        }

        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, fillW, barHeight), fillTexture);

        GUI.color = Color.white;
    }
}
