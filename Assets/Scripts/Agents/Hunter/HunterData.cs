using System;
using UnityEngine;

/// <summary>
/// Modo de navegación del circuito de waypoints para la patrulla.
/// </summary>
public enum PatrolMode
{
    Loop,
    PingPong
}

/// <summary>
/// Parámetros configurables desde el Inspector para el estado de patrulla del Cazador.
/// </summary>
[Serializable]
public class PatrolData
{
    [Tooltip("Distancia mínima al waypoint para considerarlo alcanzado.")]
    public float arrivalDistance = 1.5f;

    [Tooltip("Modo de recorrido de la ruta de patrullaje.")]
    public PatrolMode mode = PatrolMode.Loop;

    [Tooltip("Distancia de separación hacia atrás para colocar el POI sin obstruir el movimiento.")]
    public float poiDropOffset = 1.5f;
}

/// <summary>
/// Parámetros configurables desde el Inspector para el estado de ataque del Cazador.
/// </summary>
[Serializable]
public class AttackData
{
    [Tooltip("Daño infligido por ataque cuerpo a cuerpo.")]
    public float meleeDamage = 40f;

    [Tooltip("Daño infligido por ataque a distancia.")]
    public float rangedDamage = 20f;
}

/// <summary>
/// Parámetros configurables desde el Inspector para el estado de recolección del Cazador.
/// </summary>
[Serializable]
public class GatherData
{
    [Tooltip("Tiempo en segundos necesario para recolectar un Boid caído.")]
    public float duration = 3f;

    [Tooltip("Distancia de frenado y permanencia respecto al Boid caído.")]
    public float arrivalDistance = 2f;
}
