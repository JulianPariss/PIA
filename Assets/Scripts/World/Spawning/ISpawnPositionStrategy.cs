using UnityEngine;

public interface ISpawnPositionStrategy
{
    bool TryGetSpawnPosition(out Vector3 position);
}
