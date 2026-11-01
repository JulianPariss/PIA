using UnityEngine;

public class DeadState : State<HunterStateId>
{
    private readonly HunterNPC hunter;
    private float _respawnTimer = 0f;

    public DeadState(StateMachine<HunterStateId> fsm, HunterNPC hunter) : base(fsm)
    {
        this.hunter = hunter;
    }

    public override void Enter()
    {
        _respawnTimer = 0f;
        hunter.RespawnRemaining = hunter.RespawnTime;
        hunter.CurrentTarget = null;
        hunter.LastDamagedTarget = null;
        hunter.ChosenSafeZone = null;
        hunter.IsWaitingInSafeZone = false;

        hunter.SetVisualMode(HunterVisualMode.Dead);
        hunter.SetHiddenState(true);
        hunter.NotifyAction("CapturedByWolf", null);
    }

    public override void Tick()
    {
        hunter.TryConsumePendingCapture();

        _respawnTimer += Time.deltaTime;
        hunter.RespawnRemaining = Mathf.Max(0f, hunter.RespawnTime - _respawnTimer);

        if (_respawnTimer >= hunter.RespawnTime)
        {
            hunter.RespawnRemaining = 0f;
            hunter.ResetGathers();
            hunter.TeleportToInitialZone();
            hunter.SetHiddenState(false);
            hunter.ResetAttackCooldown();
            hunter.NotifyAction("RespawnedInInitialZone", null);
            fsm.ChangeState(HunterStateId.Search);
        }
    }

    public override void Exit()
    {
        _respawnTimer = 0f;
        hunter.RespawnRemaining = 0f;
        hunter.SetHiddenState(false);
    }
}
