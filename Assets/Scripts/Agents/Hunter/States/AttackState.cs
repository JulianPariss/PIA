using UnityEngine;

/// <summary>
/// Estado de combate del Cazador. Evalúa distancias en el plano XZ respecto al objetivo activo (Blackboard)
/// para ejecutar ataque cuerpo a cuerpo, a distancia o persecución (Pursuit).
/// Mantiene una ventana de tiempo perceptible (0.4s) en color Rojo para evidenciar el golpe,
/// preserva el TBA si la presa sale del rango de visión (R7) y registra LastDamagedTarget para asegurar bajas (R2).
/// </summary>
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
        // Si el golpe ya se asestó, sostiene la postura y el color rojo durante attackDuration para evidencia visual
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

        // Condición R7: Si el objetivo escapó de la visión, regresa a Patrol sin reiniciar el TBA
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

        // Mientras se aproxima hacia la presa para entrar en rango: Verde Agua (Persecución)
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
