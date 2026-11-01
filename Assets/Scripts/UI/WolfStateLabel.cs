using UnityEngine;
using PIA1.Agents.Wolves;

namespace PIA1.UI
{
    [RequireComponent(typeof(Wolf))]
    public class WolfStateLabel : FloatingAgentLabel
    {
        private Wolf wolf;

        private void Awake()
        {
            wolf = GetComponent<Wolf>();
            heightOffset = 1.9f;
        }

        protected override bool IsTargetActive()
        {
            return wolf != null && wolf.gameObject.activeInHierarchy;
        }

        protected override void UpdateLabelContent(out string text, out Color color)
        {
            if (wolf == null)
            {
                text = string.Empty;
                color = Color.white;
                return;
            }

            bool isHowling = (Time.time - wolf.LastHowlTime) < 1.5f;
            bool alertReceivedRecently = (Time.time - wolf.LastAlertReceivedTime) < 2.2f;
            WolfStateId state = wolf.FSM != null ? wolf.FSM.CurrentKey : WolfStateId.Patrol;
            string currentId = wolf.WolfId;
            if (string.IsNullOrEmpty(currentId) || currentId.StartsWith("Wolf", System.StringComparison.OrdinalIgnoreCase))
            {
                currentId = "Lobo" + (currentId != null && currentId.Length > 4 ? currentId.Substring(4) : string.Empty);
            }

            string stateText;
            if (isHowling)
            {
                stateText = "¡AULLANDO!";
                color = new Color(1f, 0.9f, 0.15f);
            }
            else
            {
                switch (state)
                {
                    case WolfStateId.Chase:
                        stateText = "PERSECUCIÓN";
                        color = new Color(0.95f, 0.15f, 0.15f);
                        break;
                    case WolfStateId.Investigate:
                        stateText = "INVESTIGANDO (LKP)";
                        color = new Color(1f, 0.82f, 0.15f);
                        break;
                    case WolfStateId.Search:
                        stateText = "BÚSQUEDA";
                        color = new Color(0.75f, 0.3f, 0.95f);
                        break;
                    case WolfStateId.Patrol:
                    default:
                        stateText = "PATRULLA";
                        color = new Color(0.85f, 0.55f, 0.25f);
                        break;
                }
            }

            text = (alertReceivedRecently && !isHowling)
                ? $"{currentId}: [{stateText}] (¡Alerta!)"
                : $"{currentId}: [{stateText}]";
        }
    }
}
