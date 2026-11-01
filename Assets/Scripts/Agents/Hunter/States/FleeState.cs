using System.Collections.Generic;
using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

public class FleeState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private Node _safeRefugeNode;
    private float _waitTimer = 0f;
    private bool _isWaitingInside = false;

    public FleeState(StateMachine<HunterStateId> fsm, HunterNPC hunter) : base(fsm)
    {
        this.hunter = hunter;
    }

    public override void Enter()
    {
        hunter.CurrentTarget = null;
        _waitTimer = 0f;
        _isWaitingInside = false;
        hunter.IsWaitingInSafeZone = false;
        hunter.SafeZoneWaitRemaining = hunter.SafeZoneWaitTime;
        hunter.SetVisualMode(HunterVisualMode.Flee);

        SelectClosestSafeZoneDestination();
        hunter.NotifyAction("FleeToSafeZone", null);
    }

    public override void Tick()
    {
        if (hunter.TryConsumePendingCapture())
        {
            fsm.ChangeState(HunterStateId.Dead);
            return;
        }

        hunter.SetVisualMode(HunterVisualMode.Flee);

        if (!_isWaitingInside)
        {
            if (_safeRefugeNode == null && hunter.ChosenSafeZone == null)
            {
                SelectClosestSafeZoneDestination();
            }

            bool reachedNode = false;
            if (_safeRefugeNode != null && hunter.NavAgent != null)
            {
                reachedNode = hunter.NavAgent.NavigateToNode(_safeRefugeNode, useArriveAtEnd: true);
            }
            else if (hunter.ChosenSafeZone != null && hunter.NavAgent != null)
            {
                reachedNode = hunter.NavAgent.NavigateToPosition(hunter.ChosenSafeZone.Bounds.center, useArriveAtEnd: true);
            }

            if (reachedNode && !hunter.IsInSafeZone && hunter.ChosenSafeZone != null && hunter.NavAgent != null)
            {
                hunter.NavAgent.NavigateToPosition(hunter.ChosenSafeZone.Bounds.center, useArriveAtEnd: true);
                return;
            }

            bool nearTarget = reachedNode;
            if (!nearTarget && _safeRefugeNode != null)
            {
                Vector3 diff = hunter.transform.position - _safeRefugeNode.Position;
                diff.y = 0f;
                nearTarget = diff.sqrMagnitude <= 1.44f; 
            }

            if (hunter.IsInSafeZone && nearTarget)
            {
                _isWaitingInside = true;
                _waitTimer = 0f;
                hunter.IsWaitingInSafeZone = true;
                hunter.NavAgent?.StopNavigation();
                hunter.Agent.ResetMotion();
            }
            return;
        }

        hunter.NavAgent?.StopNavigation();
        hunter.Agent.Stop();

        _waitTimer += Time.deltaTime;
        hunter.SafeZoneWaitRemaining = Mathf.Max(0f, hunter.SafeZoneWaitTime - _waitTimer);

        if (_waitTimer >= hunter.SafeZoneWaitTime)
        {
            hunter.SafeZoneWaitRemaining = 0f;
            hunter.IsWaitingInSafeZone = false;

            if (hunter.HasCompletedRequiredGathers)
            {
                fsm.ChangeState(HunterStateId.Exit);
            }
            else
            {
                fsm.ChangeState(HunterStateId.Search);
            }
        }
    }

    private void SelectClosestSafeZoneDestination()
    {
        _safeRefugeNode = null;
        hunter.ChosenSafeZone = null;

        if (hunter.NavAgent == null) return;

        Node closestSafeEntryNode = hunter.NavAgent.FindClosestSafeZoneNodeByPath(out ZoneArea chosenZone, out _);
        if (chosenZone == null && closestSafeEntryNode == null)
        {
            chosenZone = ZoneArea.GetNearestSafeZone(hunter.transform.position);
        }

        hunter.ChosenSafeZone = chosenZone;
        _safeRefugeNode = closestSafeEntryNode;

        if (chosenZone != null && Graph.Instance != null)
        {
            Vector3 zoneCenter = chosenZone.Bounds.center;
            Node bestInteriorNode = null;
            float bestSqr = float.MaxValue;
            IReadOnlyList<Node> allNodes = Graph.Instance.Nodes;

            for (int i = 0; i < allNodes.Count; i++)
            {
                Node n = allNodes[i];
                if (n == null || !n.IsWalkable || !n.IsSafeZone) continue;
                if (!chosenZone.Contains(n.Position)) continue;

                float sqr = (n.Position - zoneCenter).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestInteriorNode = n;
                }
            }

            if (bestInteriorNode != null)
            {
                _safeRefugeNode = bestInteriorNode;
            }
        }
    }

    public override void Exit()
    {
        _waitTimer = 0f;
        _isWaitingInside = false;
        hunter.IsWaitingInSafeZone = false;
        hunter.SafeZoneWaitRemaining = 0f;
        hunter.ChosenSafeZone = null;
        hunter.NavAgent?.StopNavigation();
    }
}
