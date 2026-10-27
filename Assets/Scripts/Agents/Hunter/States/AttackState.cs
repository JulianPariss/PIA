using UnityEngine;

/// <summary>
/// Estado de combate del Cazador. Evalúa distancias en el plano XZ respecto al objetivo activo (Blackboard)
/// para ejecutar ataque cuerpo a cuerpo, a distancia o persecución (Pursuit).
/// </summary>
public class AttackState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private readonly AttackData data;

    public AttackState(StateMachine<HunterStateId> fsm, HunterNPC hunter, AttackData data) : base(fsm)
    {
        this.hunter = hunter;
        this.data = data;
    }

    public override void Enter()
    {
        Boid target = hunter.CurrentTarget;
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

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        // Si el objetivo escapó de la visión: regresa a Patrol sin reiniciar TBA (regla canónica)
        if (distance > hunter.VisionRadius)
        {
            fsm.ChangeState(HunterStateId.Patrol);
            return;
        }

        if (distance <= hunter.MeleeAttackRadius)
        {
            ExecuteAttack(target, data.meleeDamage);
            return;
        }

        if (distance <= hunter.RangeAttackRadius)
        {
            ExecuteAttack(target, data.rangedDamage);
            return;
        }

        Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, target);
        pursuitForce.y = 0f;
        hunter.Agent.ApplyForce(pursuitForce);
    }

    private void ExecuteAttack(Boid target, float damage)
    {
        target.TakeDamage(damage);
        hunter.ResetAttackCooldown();

        if (target.isDead)
        {
            fsm.ChangeState(HunterStateId.Gather);
        }
        else
        {
            fsm.ChangeState(HunterStateId.Patrol);
        }
    }
}
