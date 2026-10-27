using System;
using UnityEngine;

public enum PatrolMode
{
    Loop,
    PingPong
}

public enum HunterVisualMode
{
    Patrol,
    Pursuit,
    Attack,
    Gather
}

[Serializable]
public class PatrolData
{
    [Tooltip("Distancia mínima al waypoint para considerarlo alcanzado.")]
    public float arrivalDistance = 1.5f;

    [Tooltip("Modo de recorrido de la ruta de patrullaje.")]
    public PatrolMode mode = PatrolMode.Loop;

    [Tooltip("Distancia de separación hacia atrás para colocar el POI sin obstruir el movimiento.")]
    public float poiDropOffset = 1.5f;

    [Tooltip("Si está activo, persigue suavemente al objetivo dañado visible mientras transcurre el cooldown de ataque.")]
    public bool pursuitWhileCooldown = true;

    [Tooltip("Factor de atenuación de fuerza durante la persecución de mantenimiento en patrulla.")]
    [Range(0.1f, 1f)]
    public float pursuitDamping = 0.85f;
}

[Serializable]
public class AttackData
{
    [Tooltip("Daño infligido por ataque cuerpo a cuerpo.")]
    public float meleeDamage = 45f;

    [Tooltip("Daño infligido por ataque a distancia.")]
    public float rangedDamage = 30f;

    [Tooltip("Tiempo en segundos que se sostiene visiblemente la ejecución del ataque.")]
    public float attackDuration = 0.4f;
}

[Serializable]
public class GatherData
{
    [Tooltip("Tiempo en segundos necesario para recolectar un Boid caído.")]
    public float duration = 2.5f;

    [Tooltip("Distancia de frenado y permanencia respecto al Boid caído.")]
    public float arrivalDistance = 2f;
}
