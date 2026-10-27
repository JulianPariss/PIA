using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel HUD en tiempo de ejecución para verificar todos los requerimientos de feedback de la consigna (1.4):
/// - Estado actual de la FSM y modo visual del Cazador.
/// - Objetivo activo y progreso de recolección.
/// - Agentes Boids detectados en rango (vivos y caídos).
/// - Cooldown TBA restante y POIs activos (x/5).
/// - Registro histórico de cambios de estado y acciones ejecutadas.
/// </summary>
public class SimulationHUD : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private HunterNPC hunter;
    [SerializeField] private int maxLogEntries = 6;

    private readonly List<string> eventLog = new List<string>();
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle textStyle;
    private static Texture2D panelBg;

    private void Awake()
    {
        if (hunter == null)
        {
            hunter = GetComponent<HunterNPC>() ?? GetComponentInParent<HunterNPC>();
        }

        if (panelBg == null)
        {
            panelBg = new Texture2D(1, 1);
            panelBg.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.11f, 0.82f));
            panelBg.Apply();
        }
    }

    private void Start()
    {
        if (hunter != null)
        {
            if (hunter.FSM != null)
            {
                hunter.FSM.OnStateChanged += HandleStateChanged;
            }
            hunter.OnActionExecuted += HandleActionExecuted;
            AddLog($"Inicio FSM → {hunter.FSM?.CurrentKey}");
        }
    }

    private void OnDestroy()
    {
        if (hunter != null)
        {
            if (hunter.FSM != null)
            {
                hunter.FSM.OnStateChanged -= HandleStateChanged;
            }
            hunter.OnActionExecuted -= HandleActionExecuted;
        }
    }

    private void HandleStateChanged(HunterStateId previous, HunterStateId current)
    {
        string targetInfo = hunter.CurrentTarget != null ? $" ({hunter.CurrentTarget.name})" : string.Empty;
        AddLog($"FSM: {previous} → {current}{targetInfo}");
    }

    private void HandleActionExecuted(string actionName, Boid target)
    {
        string targetName = target != null ? target.name : "-";
        AddLog($"Acción: {actionName} → {targetName}");
    }

    private void AddLog(string message)
    {
        string entry = $"[{Time.time:0.0}s] {message}";
        eventLog.Insert(0, entry);
        if (eventLog.Count > maxLogEntries)
        {
            eventLog.RemoveAt(eventLog.Count - 1);
        }
    }

    private void EnsureStyles()
    {
        if (panelStyle == null)
        {
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };
            panelStyle.normal.background = panelBg;
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            headerStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true
            };
            textStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
        if (hunter == null) return;

        EnsureStyles();

        int livingDetected = 0;
        int deadDetected = 0;
        if (hunter.Sensor != null)
        {
            var boids = hunter.Sensor.Boids;
            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || b.isRespawning) continue;
                if (b.IsAlive) livingDetected++;
                else deadDetected++;
            }
        }

        string fsmState = hunter.FSM != null ? hunter.FSM.CurrentKey.ToString() : "None";
        string visualMode = hunter.CurrentVisualMode.ToString();
        string tbaText = hunter.IsTBAReady ? "<color=#4CF56B>LISTO</color>" : $"{hunter.AttackCooldownRemaining:0.0}s";
        string targetText = (hunter.CurrentTarget != null && !hunter.CurrentTarget.isRespawning)
            ? $"{hunter.CurrentTarget.name} ({Mathf.CeilToInt(hunter.CurrentTarget.Health.Current)} HP)"
            : "Ninguno";
        int activePois = hunter.POISpawner != null ? hunter.POISpawner.ActiveCount : 0;

        Rect panelRect = new Rect(12f, 12f, 330f, 235f);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUILayout.BeginArea(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 20f, panelRect.height - 16f));
        GUILayout.Label("TELEMETRÍA CAZADOR (FSM & SENSORES)", headerStyle);
        GUILayout.Label($"<b>Estado FSM:</b> {fsmState}   |   <b>Acción:</b> {visualMode}", textStyle);
        GUILayout.Label($"<b>Objetivo Activo:</b> {targetText}", textStyle);
        GUILayout.Label($"<b>Boids en Sensor:</b> {livingDetected} vivos / {deadDetected} caídos", textStyle);
        GUILayout.Label($"<b>Cooldown TBA:</b> {tbaText}   |   <b>Cebos (POI):</b> {activePois}/5", textStyle);

        if (hunter.FSM != null && hunter.FSM.CurrentKey == HunterStateId.Gather)
        {
            GUILayout.Label($"<b>Progreso Recolección:</b> {Mathf.RoundToInt(hunter.GatherProgress * 100f)}%", textStyle);
        }

        GUILayout.Space(4f);
        GUILayout.Label("<b>Últimas Transiciones y Acciones:</b>", textStyle);
        for (int i = 0; i < eventLog.Count; i++)
        {
            GUILayout.Label(eventLog[i], textStyle);
        }
        GUILayout.EndArea();
    }
}
