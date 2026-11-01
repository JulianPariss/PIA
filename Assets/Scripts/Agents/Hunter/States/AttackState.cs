using UnityEngine;

public class AttackState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly AttackData data;

    private bool isAttacking = false;
    private float attackTimer = 0f;

    public AttackState(StateMachine<HunterStateId> fsm, HunterNPC hunter, AttackData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        isAttacking = false;
        attackTimer = 0f;
        hunter.NavAgent?.StopNavigation();

        Boid target = hunter.CurrentTarget;
        if (target == null || !target.IsAlive || target.isRespawning || !hunter.IsInVision(target))
        {
            target = hunter.SelectAttackTarget();
            hunter.CurrentTarget = target;
        }

        if (target == null || !target.IsAlive || target.isRespawning)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Search);
        }
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
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Flee);
            return;
        }

        if (isAttacking)
        {
            hunter.Agent.Stop();
            hunter.SetVisualMode(HunterVisualMode.Attack);
            attackTimer += Time.deltaTime;

            if (attackTimer >= data.attackDuration)
            {
                isAttacking = false;
                hunter.CurrentTarget = null;
                fsm.ChangeState(HunterStateId.Search);
            }
            return;
        }

        Boid target = hunter.CurrentTarget;
        if (target == null || target.isRespawning || target.isDead)
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Search);
            return;
        }

        if (!hunter.IsInVision(target))
        {
            hunter.CurrentTarget = null;
            fsm.ChangeState(HunterStateId.Search);
            return;
        }

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        if (distance <= hunter.MeleeAttackRadius)
        {
            StartAttackExecution(target, data.meleeDamage, "Melee");
            return;
        }

        if (distance <= hunter.RangeAttackRadius)
        {
            StartAttackExecution(target, data.rangedDamage, "Ranged");
            return;
        }

        hunter.NavAgent?.StopNavigation();
        hunter.SetVisualMode(HunterVisualMode.Pursuit);
        Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, target);
        hunter.Agent.ApplyForce(pursuitForce);
    }

    private void StartAttackExecution(Boid target, float damage, string actionName)
    {
        isAttacking = true;
        attackTimer = 0f;

        hunter.NavAgent?.StopNavigation();
        hunter.Agent.Stop();
        hunter.SetVisualMode(HunterVisualMode.Attack);

        target.TakeDamage(damage);
        hunter.ResetAttackCooldown();
        hunter.LastDamagedTarget = target;
        hunter.NotifyAction(actionName, target);
    }

    public override void Exit()
    {
        isAttacking = false;
        attackTimer = 0f;
        hunter.NavAgent?.StopNavigation();
    }
}
