using UnityEngine;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    public class WolfChaseState : State<WolfStateId>
    {
        private readonly Wolf _wolf;
        private float _lostVisionTimer = 0f;
        private Vector3 _lastBroadcastPosition;

        public WolfChaseState(StateMachine<WolfStateId> fsm, Wolf wolf) : base(fsm)
        {
            _wolf = wolf;
        }

        public override void Enter()
        {
            _lostVisionTimer = 0f;
            _lastBroadcastPosition = _wolf.LastKnownPosition;
        }

        public override void Tick()
        {
            HunterNPC hunter = _wolf.TargetHunter;

            if (hunter == null || !hunter.gameObject.activeInHierarchy || hunter.IsDead)
            {
                _wolf.ClearMemoryAndAlerts();
                fsm.ChangeState(WolfStateId.Patrol);
                return;
            }

            if (ZoneArea.IsPositionInSafeZone(hunter.transform.position))
            {
                _wolf.ClearMemoryAndAlerts();
                fsm.ChangeState(WolfStateId.Patrol);
                return;
            }

            bool hasVision = _wolf.FOV != null && _wolf.FOV.CanSeeTarget(hunter.transform);

            if (hasVision)
            {
                bool justReacquired = _lostVisionTimer > 0.3f;
                _lostVisionTimer = 0f;
                _wolf.LastKnownPosition = hunter.transform.position;

                _wolf.TryConsumePendingAlert(out _);

                if (justReacquired || (hunter.transform.position - _lastBroadcastPosition).sqrMagnitude >= 49f)
                {
                    _lastBroadcastPosition = hunter.transform.position;
                    _wolf.Howl(ignoreCooldown: false);
                }

                _wolf.NavAgent.PursueAgent(hunter.Agent);
            }
            else
            {
                if (_wolf.TryConsumePendingAlert(out Vector3 freshAlertLkp))
                {
                    _wolf.LastKnownPosition = freshAlertLkp;
                    fsm.ChangeState(WolfStateId.Investigate);
                    return;
                }

                _lostVisionTimer += Time.deltaTime;

                if (_lostVisionTimer < _wolf.MemoryTime)
                {
                    bool reachedLKP = _wolf.NavAgent.NavigateToPosition(_wolf.LastKnownPosition, useArriveAtEnd: false);
                    if (reachedLKP)
                    {
                        fsm.ChangeState(WolfStateId.Search);
                        return;
                    }
                }
                else
                {
                    _wolf.ClearMemoryAndAlerts();
                    fsm.ChangeState(WolfStateId.Patrol);
                    return;
                }
            }
        }

        public override void Exit()
        {
            _wolf.NavAgent.StopNavigation();
        }
    }
}
