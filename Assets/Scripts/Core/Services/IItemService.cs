using UnityEngine;

/// <summary>
/// Service interface for item pooling and management
/// Handles all thrown items and their lifecycle
/// </summary>
public interface IItemService : IResettable
{
    /// <summary>
    /// Get a pooled item of specific type
    /// </summary>
    GameObject GetPooledItem(string itemType);

    /// <summary>
    /// Get a pooled item by component type
    /// </summary>
    T GetPooled<T>() where T : Component;

    /// <summary>
    /// Return item to pool
    /// </summary>
    void ReturnToPool(GameObject item);

    /// <summary>
    /// Force return all active items to pools
    /// Used during cleanup
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Get count of active items in scene
    /// </summary>
    int ActiveItemCount { get; }

    /// <summary>
    /// Get count of specific item type active
    /// </summary>
    int GetActiveCount(string itemType);

    /// <summary>
    /// Initialize pools with prefabs
    /// Called once on startup
    /// </summary>
    void InitializePools(ItemPoolConfig config);
}

/// <summary>
/// Configuration for item pools
/// </summary>
[System.Serializable]
public class ItemPoolConfig
{
    public GameObject spongePrefab;
    public GameObject conchPrefab;
    public GameObject ragdollPrefab;
    public GameObject cubePrefab;

    public int initialPoolSize = 20;
    public int maxPoolSize = 100;
}