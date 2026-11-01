using UnityEngine;

public class RandomBoundsSpawnStrategy : ISpawnPositionStrategy
{
    private readonly SimulationBounds bounds;
    private readonly LayerMask obstacleMask;
    private readonly float clearanceRadius;
    private readonly int maxAttempts;

    public RandomBoundsSpawnStrategy(SimulationBounds bounds, LayerMask obstacleMask, float clearanceRadius = 1.0f, int maxAttempts = 15)
    {
        this.bounds = bounds;
        this.obstacleMask = obstacleMask;
        this.clearanceRadius = clearanceRadius;
        this.maxAttempts = maxAttempts;
    }

    public bool TryGetSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (bounds == null) return false;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = bounds.RandomPoint(0f);
            if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.5f, clearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                position = candidate;
                return true;
            }
        }

        position = bounds.Center;
        return true;
    }
}
