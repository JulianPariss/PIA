using UnityEngine;

public class BoidRespawner : RespawnController
{
    [SerializeField] private SimulationBounds bounds;
    [SerializeField] private LayerMask obstacleMask = 0;
    [SerializeField] private float checkRadius = 1.0f;
    [SerializeField] private int maxSpawnAttempts = 15;

    protected override void Awake()
    {
        base.Awake();

        if (bounds == null)
        {
            bounds = SimulationBounds.Instance;
        }

        if (obstacleMask == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0) obstacleMask = 1 << obstacleLayer;
        }

        spawnStrategy = new RandomBoundsSpawnStrategy(bounds ?? SimulationBounds.Instance, obstacleMask, checkRadius, maxSpawnAttempts);
    }
}
