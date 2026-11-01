using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    public class WolfSearchState : State<WolfStateId>
    {
        private readonly Wolf _wolf;
        private float _searchTimer = 0f;
        private Vector3 _searchPoint;
        private float _subPointTimer = 0f;

        public WolfSearchState(StateMachine<WolfStateId> fsm, Wolf wolf) : base(fsm)
        {
            _wolf = wolf;
        }

        public override void Enter()
        {
            _searchTimer = 0f;
            _subPointTimer = 0f;
            PickNextSearchPoint();
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

            if (_wolf.TryConsumePendingAlert(out Vector3 alertPos))
            {
                _wolf.LastKnownPosition = alertPos;
                fsm.ChangeState(WolfStateId.Investigate);
                return;
            }

            _searchTimer += Time.deltaTime;
            if (_searchTimer >= _wolf.SearchDuration)
            {
                _wolf.ClearMemoryAndAlerts();
                fsm.ChangeState(WolfStateId.Patrol);
                return;
            }

            _subPointTimer += Time.deltaTime;
            bool reachedSubPoint = _wolf.NavAgent.NavigateToPosition(_searchPoint, useArriveAtEnd: true);
            if (reachedSubPoint || _subPointTimer >= 2.0f)
            {
                _subPointTimer = 0f;
                PickNextSearchPoint();
            }
        }

        private void PickNextSearchPoint()
        {
            if (Graph.Instance != null && _wolf.HasLastKnownPosition)
            {
                float eyeH = _wolf.FOV != null ? _wolf.FOV.EyeHeight : 0.8f;
                Node nearNode = Graph.Instance.GetClosestVisibleNode(_wolf.LastKnownPosition, eyeH, excludeSafeZones: true);
                if (nearNode != null && nearNode.Neighbors.Count > 0)
                {
                    int randIdx = Random.Range(0, nearNode.Neighbors.Count);
                    Node candidate = nearNode.Neighbors[randIdx];
                    if (candidate != null && !candidate.IsSafeZone)
                    {
                        _searchPoint = candidate.Position;
                        return;
                    }
                }
            }

            Vector2 circle = Random.insideUnitCircle * 3.0f;
            Vector3 candidatePos = _wolf.transform.position + new Vector3(circle.x, 0f, circle.y);
            if (!ZoneArea.IsPositionInSafeZone(candidatePos))
            {
                _searchPoint = candidatePos;
            }
            else
            {
                _searchPoint = _wolf.transform.position;
            }
        }

        public override void Exit()
        {
            _wolf.NavAgent.StopNavigation();
        }
    }
}
