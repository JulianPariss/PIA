using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    public class WolfPatrolState : State<WolfStateId>
    {
        private readonly Wolf _wolf;

        public WolfPatrolState(StateMachine<WolfStateId> fsm, Wolf wolf) : base(fsm)
        {
            _wolf = wolf;
        }

        public override void Enter()
        {
            _wolf.HasLastKnownPosition = false;

            if (_wolf.Route != null && _wolf.Route.WaypointCount > 0)
            {
                int closestIdx = _wolf.Route.GetClosestWaypointIndex(_wolf.transform.position);
                _wolf.Route.SetCurrentIndex(closestIdx);
            }
        }

        public override void Tick()
        {
            HunterNPC hunter = _wolf.TargetHunter;

            if (hunter != null && hunter.gameObject.activeInHierarchy && !hunter.IsDead)
            {
                if (!ZoneArea.IsPositionInSafeZone(hunter.transform.position))
                {
                    if (_wolf.FOV != null && _wolf.FOV.CanSeeTarget(hunter.transform))
                    {
                        _wolf.ClearMemoryAndAlerts();
                        _wolf.LastKnownPosition = hunter.transform.position;
                        _wolf.Howl(ignoreCooldown: true);
                        fsm.ChangeState(WolfStateId.Chase);
                        return;
                    }
                }
            }

            if (hunter != null && !hunter.IsDead && !ZoneArea.IsPositionInSafeZone(hunter.transform.position))
            {
                if (_wolf.TryConsumePendingAlert(out Vector3 alertPos))
                {
                    _wolf.LastKnownPosition = alertPos;
                    fsm.ChangeState(WolfStateId.Investigate);
                    return;
                }
            }
            else if (_wolf.HasPendingAlert)
            {
                _wolf.ClearMemoryAndAlerts();
            }

            PatrolRoute route = _wolf.Route;
            if (route == null || route.WaypointCount == 0)
            {
                _wolf.NavAgent.StopNavigation();
                return;
            }

            Node currentWaypoint = route.CurrentWaypoint;
            if (currentWaypoint == null)
            {
                route.Advance();
                return;
            }

            bool reached = _wolf.NavAgent.NavigateToNode(currentWaypoint, useArriveAtEnd: false);
            if (reached)
            {
                route.Advance();
            }
        }

        public override void Exit()
        {
            _wolf.NavAgent.StopNavigation();
        }
    }
}
