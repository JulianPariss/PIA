using UnityEngine;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    public class WolfInvestigateState : State<WolfStateId>
    {
        private readonly Wolf _wolf;

        public WolfInvestigateState(StateMachine<WolfStateId> fsm, Wolf wolf) : base(fsm)
        {
            _wolf = wolf;
        }

        public override void Enter()
        {
            if (!_wolf.HasLastKnownPosition)
            {
                fsm.ChangeState(WolfStateId.Patrol);
            }
        }

        public override void Tick()
        {
            HunterNPC hunter = _wolf.TargetHunter;

            if (hunter == null || !hunter.gameObject.activeInHierarchy || hunter.IsDead ||
                ZoneArea.IsPositionInSafeZone(hunter.transform.position))
            {
                _wolf.ClearMemoryAndAlerts();
                fsm.ChangeState(WolfStateId.Patrol);
                return;
            }

            if (_wolf.FOV != null && _wolf.FOV.CanSeeTarget(hunter.transform))
            {
                _wolf.ClearMemoryAndAlerts();
                _wolf.LastKnownPosition = hunter.transform.position;
                _wolf.Howl(ignoreCooldown: true);
                fsm.ChangeState(WolfStateId.Chase);
                return;
            }

            if (_wolf.TryConsumePendingAlert(out Vector3 updatedAlertPos))
            {
                _wolf.LastKnownPosition = updatedAlertPos;
            }

            if (!_wolf.HasLastKnownPosition || ZoneArea.IsPositionInSafeZone(_wolf.LastKnownPosition))
            {
                _wolf.ClearMemoryAndAlerts();
                fsm.ChangeState(WolfStateId.Patrol);
                return;
            }

            bool reached = _wolf.NavAgent.NavigateToPosition(_wolf.LastKnownPosition, useArriveAtEnd: false);
            if (reached)
            {
                fsm.ChangeState(WolfStateId.Search);
            }
        }

        public override void Exit()
        {
            _wolf.NavAgent.StopNavigation();
        }
    }
}
