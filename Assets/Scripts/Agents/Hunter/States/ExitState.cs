using System.Collections.Generic;
using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

public class ExitState : State<HunterStateId>
{
    private const float ExitDisappearDuration = 0.6f;

    private readonly HunterNPC hunter;
    private ZoneArea _exitZone;
    private Node _exitNode;
    private bool _hasReachedExit = false;
    private float _disappearTimer = 0f;

    public ExitState(StateMachine<HunterStateId> fsm, HunterNPC hunter) : base(fsm)
    {
        this.hunter = hunter;
    }

    public override void Enter()
    {
        hunter.CurrentTarget = null;
        _hasReachedExit = false;
        _disappearTimer = 0f;
        hunter.SetVisualMode(HunterVisualMode.Exit);
        SelectExitDestination();
    }

    public override void Tick()
    {
        if (_hasReachedExit)
        {
            _disappearTimer += Time.deltaTime;
            if (_disappearTimer >= ExitDisappearDuration)
            {
                _hasReachedExit = false;
                hunter.ResetGathers();
                hunter.TeleportToInitialZone();
                hunter.SetHiddenState(false);
                hunter.ResetAttackCooldown();
                hunter.NotifyAction("ExitCycleCompleted", null);
                fsm.ChangeState(HunterStateId.Search);
            }
            return;
        }

        if (hunter.TryConsumePendingCapture())
        {
            fsm.ChangeState(HunterStateId.Dead);
            return;
        }

        if (hunter.ShouldFleeFromWolf())
        {
            fsm.ChangeState(HunterStateId.Flee);
            return;
        }

        hunter.SetVisualMode(HunterVisualMode.Exit);

        if (_exitNode == null && _exitZone == null)
        {
            SelectExitDestination();
        }

        bool reached = false;
        if (_exitNode != null && hunter.NavAgent != null)
        {
            reached = hunter.NavAgent.NavigateToNode(_exitNode, useArriveAtEnd: true);
        }
        else if (_exitZone != null && hunter.NavAgent != null)
        {
            reached = hunter.NavAgent.NavigateToPosition(_exitZone.Bounds.center, useArriveAtEnd: true);
        }

        if (!reached && hunter.IsInExitZone && _exitNode != null)
        {
            Vector3 diff = hunter.transform.position - _exitNode.Position;
            diff.y = 0f;
            reached = diff.sqrMagnitude <= 1.44f;
        }

        if (reached)
        {
            _hasReachedExit = true;
            _disappearTimer = 0f;
            hunter.SetHiddenState(true);
        }
    }

    private void SelectExitDestination()
    {
        _exitZone = ZoneArea.GetZone(ZoneType.ExitZone);
        _exitNode = null;

        if (Graph.Instance == null) return;

        Vector3 refCenter = _exitZone != null ? _exitZone.Bounds.center : hunter.transform.position;
        float bestSqr = float.MaxValue;
        IReadOnlyList<Node> nodes = Graph.Instance.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n == null || !n.IsWalkable || n.Zone != ZoneType.ExitZone) continue;

            float sqr = (n.Position - refCenter).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                _exitNode = n;
            }
        }
    }

    public override void Exit()
    {
        if (_hasReachedExit)
        {
            _hasReachedExit = false;
            hunter.SetHiddenState(false);
        }
        hunter.NavAgent?.StopNavigation();
    }
}
