/// <summary>
/// Estado de combate del Cazador. Evalúa distancias en el plano XZ para atacar con rangos de histéresis y ventanas de tiempo mínimas.
/// </summary>
using UnityEngine;

public class AttackState : IState
{
    private readonly HunterNPC hunter;
    private readonly Boid target;

    public float meleeDamage = 40f;
    public float rangedDamage = 20f;

    public AttackState(HunterNPC hunter, Boid target)
    {
        this.hunter = hunter;
        this.target = target;
    }

    public void Enter() { }

    public void UpdateState()
    {
        if (target == null)
        {
            hunter.FSM.ChangeState(new PatrolState(hunter), force: true);
            return;
        }

        if (target.isDead)
        {
            hunter.FSM.ChangeState(new GatherState(hunter, target), force: true);
            return;
        }

        Vector3 hunterFlat = hunter.transform.position;
        Vector3 targetFlat = target.transform.position;
        hunterFlat.y = 0f;
        targetFlat.y = 0f;
        float distance = Vector3.Distance(hunterFlat, targetFlat);

        float escapeRadius = hunter.visionRadius * 1.25f;

        if (distance > escapeRadius && hunter.FSM.CanChangeState)
        {
            hunter.FSM.ChangeState(new PatrolState(hunter));
            return;
        }

        if (distance <= hunter.MeleeAttackRadius)
        {
            ExecuteMeleeAttack();
            return;
        }

        if (distance <= hunter.RangeAttackRadius)
        {
            ExecuteRangedAttack();
            return;
        }

        Vector3 pursuitForce = SteeringBehaviors.Pursuit(hunter.Agent, target);
        pursuitForce.y = 0f;
        hunter.Agent.ApplyForce(pursuitForce);
    }

    private void ExecuteMeleeAttack()
    {
        target.TakeDamage(meleeDamage);
        hunter.lastAttackTime = Time.time;
        if (target.isDead)
        {
            hunter.FSM.ChangeState(new GatherState(hunter, target), force: true);
        }
        else
        {
            hunter.FSM.ChangeState(new PatrolState(hunter), force: true);
        }
    }

    private void ExecuteRangedAttack()
    {
        target.TakeDamage(rangedDamage);
        hunter.lastAttackTime = Time.time;
        if (target.isDead)
        {
            hunter.FSM.ChangeState(new GatherState(hunter, target), force: true);
        }
        else
        {
            hunter.FSM.ChangeState(new PatrolState(hunter), force: true);
        }
    }

    public void Exit() { }
}
