using UnityEngine;

namespace PIA1.UI
{
    [RequireComponent(typeof(HunterNPC))]
    public class HunterStateLabel : FloatingAgentLabel
    {
        [SerializeField] private HunterNPC hunter;

        private void Start()
        {
            if (hunter == null)
            {
                hunter = GetComponent<HunterNPC>();
            }
            heightOffset = 2.3f;
        }

        protected override bool IsTargetActive()
        {
            return hunter != null && !hunter.IsDead;
        }

        protected override void UpdateLabelContent(out string text, out Color color)
        {
            if (hunter == null)
            {
                text = string.Empty;
                color = Color.white;
                return;
            }

            HunterStateId state = (hunter.FSM != null && hunter.FSM.HasState)
                ? hunter.FSM.CurrentKey
                : HunterStateId.Search;

            int auxInt = state switch
            {
                HunterStateId.Gather => Mathf.RoundToInt(hunter.GatherProgress * 100f),
                HunterStateId.Flee when hunter.IsWaitingInSafeZone => Mathf.CeilToInt(hunter.SafeZoneWaitRemaining * 10f),
                _ => hunter.CurrentGathers
            };

            Boid activeTarget = (hunter.CurrentTarget != null && !hunter.CurrentTarget.isRespawning) ? hunter.CurrentTarget : null;
            string safeName = hunter.ChosenSafeZone != null ? hunter.ChosenSafeZone.ZoneName : string.Empty;

            string stateText = state switch
            {
                HunterStateId.Attack => "ATAQUE",
                HunterStateId.Gather => $"RECOLECCIÓN {auxInt}%",
                HunterStateId.Flee => hunter.IsWaitingInSafeZone
                    ? $"REFUGIO {(auxInt * 0.1f):0.0}s"
                    : (!string.IsNullOrEmpty(safeName) ? $"HUYENDO → {safeName}" : "HUYENDO"),
                HunterStateId.Exit => $"SALIDA ({hunter.CurrentGathers}/{hunter.RequiredGathers})",
                HunterStateId.Dead => "CAPTURADO",
                _ => $"BÚSQUEDA ({hunter.CurrentGathers}/{hunter.RequiredGathers})"
            };

            text = activeTarget != null ? $"[{stateText}]  Obj: {activeTarget.name}" : $"[{stateText}]";

            color = state switch
            {
                HunterStateId.Attack => new Color(1f, 0.25f, 0.25f),
                HunterStateId.Gather => new Color(1f, 0.88f, 0.1f),
                HunterStateId.Flee => new Color(1f, 0.55f, 0.1f),
                HunterStateId.Exit => new Color(0.8f, 0.4f, 1f),
                _ => new Color(0.3f, 0.95f, 0.4f)
            };
        }
    }
}
