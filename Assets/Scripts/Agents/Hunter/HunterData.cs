using System;
using UnityEngine;

public enum PatrolMode
{
    Loop
}

public enum HunterVisualMode
{
    Search,
    Pursuit,
    Attack,
    Gather,
    Flee,
    Exit,
    Dead
}

[Serializable]
public class PatrolData
{
    public float arrivalDistance = 1.5f;
    public PatrolMode mode = PatrolMode.Loop;
    public float poiDropOffset = 1.5f;
    public bool pursuitWhileCooldown = false;
    public float pursuitDamping = 0.85f;
}

[Serializable]
public class AttackData
{
    public float meleeDamage = 45f;
    public float rangedDamage = 30f;
    public float attackDuration = 0.35f;
}

[Serializable]
public class GatherData
{
    public float duration = 2.5f;
    public float arrivalDistance = 2f;
}
