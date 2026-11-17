using UnityEngine;

namespace PIA1.UI
{
    [RequireComponent(typeof(Boid))]
    public class BoidStateLabel : FloatingAgentLabel
    {
        [SerializeField] private Boid boid;

        private void Start()
        {
            if (boid == null)
            {
                boid = GetComponent<Boid>();
            }
            heightOffset = 1.75f;
        }

        protected override bool IsTargetActive()
        {
            return boid != null && !boid.IsRespawning;
        }

        protected override void UpdateLabelContent(out string text, out Color color)
        {
            if (boid == null || boid.FSM == null || !boid.FSM.HasState)
            {
                text = string.Empty;
                color = Color.white;
                return;
            }

            BoidStateId state = boid.FSM.CurrentKey;

            text = state switch
            {
                BoidStateId.SeekObject => "[Seek Object]",
                BoidStateId.Consume => "[Consume]",
                BoidStateId.Evade => "[Evade]",
                BoidStateId.Dead => "[Dead]",
                BoidStateId.Respawn => "[Respawn]",
                _ => "[Flocking]"
            };

            color = state switch
            {
                BoidStateId.Evade => new Color(1f, 0.55f, 0.1f),
                BoidStateId.SeekObject => new Color(0.35f, 0.85f, 1f),
                BoidStateId.Consume => new Color(1f, 0.9f, 0.25f),
                BoidStateId.Dead => new Color(0.65f, 0.65f, 0.65f),
                _ => new Color(0.45f, 0.9f, 1f)
            };
        }
    }
}
