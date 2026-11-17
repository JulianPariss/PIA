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

        if (obstacleMask == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0) obstacleMask = 1 << obstacleLayer;
        }

        EnsureSpawnStrategy();
    }

    private void Start()
    {
        EnsureSpawnStrategy();
    }

    private void EnsureSpawnStrategy()
    {
        if (bounds == null)
        {
            bounds = SimulationBounds.Instance;
        }

        spawnStrategy = new RandomBoundsSpawnStrategy(bounds, obstacleMask, checkRadius, maxSpawnAttempts);
    }
}
