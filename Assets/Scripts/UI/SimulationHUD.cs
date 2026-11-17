using UnityEngine;
using PIA1.Agents.Wolves;
using PIA1.World;

public class SimulationHUD : MonoBehaviour
{
    [SerializeField] private HunterNPC hunter;

    public static SimulationHUD Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (hunter == null)
        {
            hunter = GetComponent<HunterNPC>() ?? GetComponentInParent<HunterNPC>() ?? HunterNPC.Instance;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnGUI()
    {
        if (hunter == null)
        {
            hunter = HunterNPC.Instance;
            if (hunter == null)
            {
                return;
            }
        }

        if (hunter.IsWaypointPatrolMode)
        {
            DrawRecuperatorioHUD();
        }
        else
        {
            DrawParcial2HUD();
        }
    }

    private void DrawRecuperatorioHUD()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 320f, 165f), GUI.skin.box);
        GUILayout.Label("Simulación - Telemetría Cazador", GUI.skin.box);

        string hunterState = hunter.FSM != null ? hunter.FSM.CurrentKey.ToString() : "None";
        GUILayout.Label($"Estado FSM: [{hunterState}] | Modo: {hunter.CurrentVisualMode}");
        GUILayout.Label($"Energía: {hunter.Energy:0}/{hunter.MaxEnergy:0} ({Mathf.RoundToInt(hunter.EnergyNormalized * 100f)}%)");

        string tba = hunter.IsTBAReady ? "Listo" : $"{hunter.AttackCooldownRemaining:0.0}s";
        string targetName = hunter.CurrentTarget != null ? $"{hunter.CurrentTarget.name} ({hunter.CurrentTarget.Health.Current:0} HP)" : "-";
        GUILayout.Label($"TBA: {tba} | Presa: {targetName}");

        CountSensorBoids(out int aliveBoids, out int deadBoids);
        int activePois = hunter.POISpawner != null ? hunter.POISpawner.ActiveCount : 0;
        GUILayout.Label($"Boids Vivos: {aliveBoids} | Caídos: {deadBoids} | POIs: {activePois}/5");

        if (hunter.FSM != null && hunter.FSM.CurrentKey == HunterStateId.Gather)
        {
            GUILayout.Label($"Progreso Recolección: {Mathf.RoundToInt(hunter.GatherProgress * 100f)}%");
        }

        GUILayout.EndArea();
    }

    private void DrawParcial2HUD()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 320f, 200f), GUI.skin.box);
        GUILayout.Label("Simulación - Telemetría", GUI.skin.box);

        string hunterState = hunter.FSM != null ? hunter.FSM.CurrentKey.ToString() : "None";
        GUILayout.Label($"Cazador: [{hunterState}] | Recolección: {hunter.CurrentGathers}/{hunter.RequiredGathers}");

        string navMode = hunter.NavAgent != null ? hunter.NavAgent.CurrentMode.ToString() : "Idle";
        ZoneArea currentZone = ZoneArea.GetZoneAt(hunter.transform.position);
        string zoneName = currentZone != null ? currentZone.ZoneName : "Pasillo";
        GUILayout.Label($"Navegación: {navMode} | Zona: {zoneName}");

        string wolfThreat = hunter.DetectedWolf != null
            ? $"¡{hunter.DetectedWolf.WolfId} a la vista!"
            : (WolfPack.Instance != null && WolfPack.Instance.HasActiveLKP ? "Alerta LKP" : "Despejado");
        GUILayout.Label($"Amenaza Lobos: {wolfThreat}");

        string safeZone = hunter.ChosenSafeZone != null
            ? hunter.ChosenSafeZone.ZoneName
            : (hunter.IsInSafeZone ? "En Refugio" : "-");
        GUILayout.Label($"Zona Segura: {safeZone}");

        string tba = hunter.IsTBAReady ? "Listo" : $"{hunter.AttackCooldownRemaining:0.0}s";
        string targetName = hunter.CurrentTarget != null ? hunter.CurrentTarget.name : "-";
        GUILayout.Label($"TBA: {tba} | Target: {targetName}");

        CountSensorBoids(out int aliveBoids, out int deadBoids);
        int activePois = hunter.POISpawner != null ? hunter.POISpawner.ActiveCount : 0;
        GUILayout.Label($"Boids Vivos: {aliveBoids} | Caídos: {deadBoids} | POIs: {activePois}");

        int totalWolves = WolfPack.Instance != null ? WolfPack.Instance.Wolves.Count : 0;
        GUILayout.Label($"Lobos Activos: {totalWolves}");

        GUILayout.EndArea();
    }

    private void CountSensorBoids(out int aliveBoids, out int deadBoids)
    {
        aliveBoids = 0;
        deadBoids = 0;

        if (hunter.Sensor == null) return;

        var boids = hunter.Sensor.Boids;
        for (int i = 0; i < boids.Count; i++)
        {
            Boid b = boids[i];
            if (b == null || b.isRespawning) continue;

            if (b.IsAlive)
            {
                aliveBoids++;
            }
            else
            {
                deadBoids++;
            }
        }
    }
}
