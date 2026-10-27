using UnityEngine;

/// <summary>
/// Etiqueta de texto proyectada en el espacio del mundo sobre el Cazador.
/// Muestra de forma destacada el estado actual de la FSM, el modo de acción y el objetivo activo.
/// </summary>
public class HunterStateLabel : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private HunterNPC hunter;
    [SerializeField] private float heightOffset = 2.3f;

    private GUIStyle labelStyle;
    private GUIStyle badgeStyle;
    private static Texture2D badgeBackground;

    private void Awake()
    {
        if (hunter == null)
        {
            hunter = GetComponent<HunterNPC>() ?? GetComponentInParent<HunterNPC>();
        }

        InitStyles();
    }

    private static void InitStyles()
    {
        if (badgeBackground == null)
        {
            badgeBackground = new Texture2D(1, 1);
            badgeBackground.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.08f, 0.85f));
            badgeBackground.Apply();
        }
    }

    private void EnsureStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
        }

        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(GUI.skin.box);
            badgeStyle.normal.background = badgeBackground;
            badgeStyle.padding = new RectOffset(6, 6, 2, 2);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (hunter == null || !gameObject.activeInHierarchy) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 worldPos = transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0.5f) return; // Detrás de la cámara

        EnsureStyles();

        string stateText;
        Color stateColor;

        switch (hunter.CurrentVisualMode)
        {
            case HunterVisualMode.Pursuit:
                stateText = "PERSECUCIÓN";
                stateColor = new Color(0.1f, 0.85f, 0.75f); // Teal
                break;
            case HunterVisualMode.Attack:
                stateText = "ATAQUE";
                stateColor = new Color(1f, 0.25f, 0.25f); // Rojo
                break;
            case HunterVisualMode.Gather:
                int pct = Mathf.RoundToInt(hunter.GatherProgress * 100f);
                stateText = $"RECOLECCIÓN {pct}%";
                stateColor = new Color(1f, 0.88f, 0.1f); // Amarillo
                break;
            case HunterVisualMode.Patrol:
            default:
                stateText = "PATRULLA";
                stateColor = new Color(0.3f, 0.95f, 0.4f); // Verde
                break;
        }

        string displayText = $"[{stateText}]";
        if (hunter.CurrentTarget != null && !hunter.CurrentTarget.isRespawning)
        {
            displayText += $"  Objetivo: {hunter.CurrentTarget.name}";
        }

        Vector2 size = labelStyle.CalcSize(new GUIContent(displayText)) + new Vector2(16, 6);
        float x = screenPos.x - (size.x * 0.5f);
        float y = Screen.height - screenPos.y - size.y;

        Rect badgeRect = new Rect(x, y, size.x, size.y);
        GUI.Box(badgeRect, GUIContent.none, badgeStyle);

        labelStyle.normal.textColor = stateColor;
        GUI.Label(badgeRect, displayText, labelStyle);
    }
}
