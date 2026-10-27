using UnityEngine;

public class AttackState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly AttackData data;

    private bool isAttacking = false;
    private float attackTimer = 0f;
    private Boid attackTargetSaved = null;

    public AttackState(StateMachine<HunterStateId> fsm, HunterNPC hunter, AttackData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        isAttacking = false;
        attackTimer = 0f;
        attackTargetSaved = null;

        Boid target = hunter.SelectAttackTarget();
        hunter.CurrentTarget = target;

        if (target == null || target.isRespawning)
        {
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        if (target.isDead)
        {
            fsm.ChangeState(HunterStateId.Gather);
        }
    }

    public override void Tick()
    {
        if (isAttacking)
        {
            hunter.Agent.Stop();
            hunter.SetVisualMode(HunterVisualMode.Attack);
            attackTimer += Time.deltaTime;

            if (attackTimer >= data.attackDuration)
            {
                isAttacking = false;
                if (attackTargetSaved != null && attackTargetSaved.isDead)
                {
                    fsm.ChangeState(HunterStateId.Gather);
                }
                else
                {
                    fsm.ChangeState(HunterStateId.Patrol);
                }
            }
            return;
        }

        Boid target = hunter.CurrentTarget;
        if (target == null || target.isRespawning)
        {
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        if (target.isDead)
        {
            fsm.ChangeState(HunterStateId.Gather);
            return;
        }

        if (!hunter.IsInVision(target))
        {
            fsm.ChangeState(HunterStateId.Patrol);
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

        hunter.SetVisualMode(HunterVisualMode.Pursuit);
        Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, target);
        pursuitForce.y = 0f;
        hunter.Agent.ApplyForce(pursuitForce);
    }

    private void StartAttackExecution(Boid target, float damage, string actionName)
    {
        isAttacking = true;
        attackTimer = 0f;
        attackTargetSaved = target;

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
        attackTargetSaved = null;
    }
}
