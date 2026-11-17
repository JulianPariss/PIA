using UnityEngine;

public class HuntState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly AttackData data;

    private Vector3 lastKnownPosition;
    private float attackVisualTimer;

    public HuntState(StateMachine<HunterStateId> fsm, HunterNPC hunter, AttackData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        attackVisualTimer = 0f;

        Boid target = hunter.CurrentTarget;
        if (target == null || !target.IsAlive || target.isRespawning)
        {
            target = hunter.SelectAttackTarget();
            hunter.CurrentTarget = target;
        }

        if (target == null || !target.IsAlive || target.isRespawning)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        target.ClearBoundsExitFlag();
        lastKnownPosition = target.transform.position;
        hunter.SetVisualMode(HunterVisualMode.Pursuit);
    }

    public override void Tick()
    {
        hunter.ConsumeEnergy(Time.deltaTime);

        if (hunter.Energy <= 0f)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Rest);
            return;
        }

        Boid deadBoid = hunter.FindCollectableBoid();
        if (deadBoid != null)
        {
            hunter.CurrentTarget = deadBoid;
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        Boid target = hunter.CurrentTarget;

        if (target == null || !target.IsAlive || target.isRespawning || !target.gameObject.activeInHierarchy)
        {
            Boid nextTarget = hunter.SelectAttackTarget();
            if (nextTarget != null)
            {
                nextTarget.ClearBoundsExitFlag();
                hunter.CurrentTarget = nextTarget;
                target = nextTarget;
                lastKnownPosition = target.transform.position;
            }
            else
            {
                hunter.CurrentTarget = null;
                MoveToLastKnownPosition();
                return;
            }
        }

        if (target.ConsumeBoundsExitFlag() ||
            (hunter.Bounds != null && !hunter.Bounds.Contains(target.transform.position)))
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        lastKnownPosition = target.transform.position;

        if (attackVisualTimer > 0f)
        {
            attackVisualTimer -= Time.deltaTime;
            hunter.SetVisualMode(HunterVisualMode.Attack);
        }
        else
        {
            hunter.SetVisualMode(HunterVisualMode.Pursuit);
        }

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        if (hunter.IsTBAReady)
        {
            if (distance <= hunter.MeleeAttackRadius)
            {
                ExecuteAttack(target, data.meleeHits, "Melee");
            }
            else if (distance <= hunter.RangeAttackRadius)
            {
                ExecuteAttack(target, data.rangedHits, "Ranged");
            }
        }

        Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, target);
        pursuitForce.y = 0f;
        hunter.Agent.ApplyForce(pursuitForce);
    }

    private void ExecuteAttack(Boid target, int hits, string actionName)
    {
        attackVisualTimer = data.attackDuration;
        hunter.SetVisualMode(HunterVisualMode.Attack);

        target.TakeHits(hits);
        hunter.ResetAttackCooldown();
        hunter.LastDamagedTarget = target;
        hunter.NotifyAction(actionName, target);
    }

    private void MoveToLastKnownPosition()
    {
        if (attackVisualTimer > 0f)
        {
            attackVisualTimer -= Time.deltaTime;
            hunter.SetVisualMode(HunterVisualMode.Attack);
        }
        else
        {
            hunter.SetVisualMode(HunterVisualMode.Pursuit);
        }

        Vector3 targetPos = lastKnownPosition;
        targetPos.y = hunter.transform.position.y;
        Vector3 seekForce = SteeringBehaviors.Seek(hunter.Agent, targetPos);
        seekForce.y = 0f;
        hunter.Agent.ApplyForce(seekForce);
    }

    public override void Exit()
    {
        attackVisualTimer = 0f;
    }
}
