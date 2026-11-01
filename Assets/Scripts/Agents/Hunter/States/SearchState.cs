using UnityEngine;
using PIA1.Navigation;
using PIA1.World;

public class SearchState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly PatrolData data;
    private Node _targetCotoNode;

    public SearchState(StateMachine<HunterStateId> fsm, HunterNPC hunter, PatrolData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        hunter.CurrentTarget = null;
        hunter.ChosenSafeZone = null;
        hunter.IsWaitingInSafeZone = false;
        _targetCotoNode = null;
        hunter.SetVisualMode(HunterVisualMode.Search);
    }

    public override void Tick()
    {
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

        if (hunter.HasCompletedRequiredGathers)
        {
            fsm.ChangeState(HunterStateId.Exit);
            return;
        }

        Boid deadBoid = hunter.FindCollectableBoid();
        if (deadBoid != null)
        {
            hunter.CurrentTarget = deadBoid;
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        if (hunter.IsTBAReady)
        {
            Boid attackTarget = hunter.SelectAttackTarget();
            if (attackTarget != null)
            {
                hunter.CurrentTarget = attackTarget;
                fsm.ChangeState(HunterStateId.Attack);
                return;
            }
        }

        if (hunter.IsInCoto)
        {
            UpdatePOISpawnInCoto();
        }

        if (data.pursuitWhileCooldown && hunter.IsInCoto)
        {
            Boid visibleLiveBoid = hunter.SelectAttackTarget();
            if (visibleLiveBoid != null && hunter.NavAgent != null &&
                hunter.NavAgent.HasDirectClearanceTo(visibleLiveBoid.transform.position))
            {
                ZoneArea boidZone = ZoneArea.GetZoneAt(visibleLiveBoid.transform.position);
                if (boidZone != null && boidZone.Type == ZoneType.Coto)
                {
                    hunter.NavAgent.StopNavigation();
                    hunter.SetVisualMode(HunterVisualMode.Pursuit);
                    Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, visibleLiveBoid);
                    hunter.Agent.ApplyForce(pursuitForce * data.pursuitDamping);
                    return;
                }
            }
        }

        hunter.SetVisualMode(HunterVisualMode.Search);

        if (_targetCotoNode == null || !_targetCotoNode.IsWalkable)
        {
            _targetCotoNode = SelectNextCotoNode(_targetCotoNode);
        }

        if (_targetCotoNode != null && hunter.NavAgent != null)
        {
            bool reached = hunter.NavAgent.NavigateToNode(_targetCotoNode, useArriveAtEnd: false);
            if (reached)
            {
                _targetCotoNode = SelectNextCotoNode(_targetCotoNode);
            }
        }
        else
        {
            ZoneArea cotoZone = ZoneArea.GetZone(ZoneType.Coto);
            if (cotoZone != null && hunter.NavAgent != null)
            {
                hunter.NavAgent.NavigateToPosition(cotoZone.Bounds.center, useArriveAtEnd: false);
            }
        }
    }

    private void UpdatePOISpawnInCoto()
    {
        if (hunter.POISpawner == null) return;

        Vector3 candidatePos = hunter.transform.position - (hunter.transform.forward * data.poiDropOffset);
        ZoneArea candidateZone = ZoneArea.GetZoneAt(candidatePos);
        bool insideCoto = candidateZone != null && candidateZone.Type == ZoneType.Coto;
        bool blockedByWall = Physics.CheckSphere(candidatePos + Vector3.up * 0.5f, 0.4f, hunter.ObstacleMask, QueryTriggerInteraction.Ignore);

        Vector3 spawnPos = (insideCoto && !blockedByWall) ? candidatePos : hunter.transform.position;
        hunter.POISpawner.Tick(Time.deltaTime, spawnPos);
    }

    private static Node SelectNextCotoNode(Node previousNode)
    {
        if (Graph.Instance == null) return null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Node candidate = Graph.Instance.GetRandomCotoNode();
            if (candidate != null && candidate != previousNode)
            {
                return candidate;
            }
        }

        return Graph.Instance.GetRandomCotoNode();
    }

    public override void Exit()
    {
        hunter.NavAgent?.StopNavigation();
    }
}
