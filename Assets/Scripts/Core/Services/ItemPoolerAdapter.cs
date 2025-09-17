using UnityEngine;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Adapter wrapping ItemPooler to provide IItemService interface
    /// Maps the simpler ItemPooler API to the richer IItemService contract
    /// </summary>
    public class ItemPoolerAdapter : IItemService
    {
        private readonly ItemPooler wrapped;
        private ItemPoolConfig config;
        private Dictionary<string, GameObject> prefabLookup;

        public ItemPoolerAdapter(ItemPooler pooler)
        {
            wrapped = pooler ?? throw new System.ArgumentNullException(nameof(pooler));
            prefabLookup = new Dictionary<string, GameObject>();
        }

        // === IItemService Implementation ===

        /// <summary>
        /// Initialize pools with prefabs
        /// </summary>
        public void InitializePools(ItemPoolConfig poolConfig)
        {
            config = poolConfig;

            // Build lookup table for item types
            if (config != null)
            {
                prefabLookup.Clear();

                if (config.spongePrefab != null)
                    prefabLookup["sponge"] = config.spongePrefab;

                if (config.conchPrefab != null)
                    prefabLookup["conch"] = config.conchPrefab;

                if (config.ragdollPrefab != null)
                    prefabLookup["ragdoll"] = config.ragdollPrefab;

                if (config.cubePrefab != null)
                    prefabLookup["cube"] = config.cubePrefab;

                Debug.Log($"[ItemPoolerAdapter] Initialized with {prefabLookup.Count} item types");
            }
        }

        /// <summary>
        /// Get a pooled item of specific type
        /// </summary>
        public GameObject GetPooledItem(string itemType)
        {
            if (!prefabLookup.TryGetValue(itemType.ToLower(), out GameObject prefab))
            {
                Debug.LogError($"[ItemPoolerAdapter] Unknown item type: {itemType}");
                return null;
            }

            // Use ItemPooler's method with default position/rotation
            return wrapped.GetPooledItem(prefab, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Get a pooled item by component type
        /// </summary>
        public T GetPooled<T>() where T : Component
        {
            // Find prefab with matching component
            foreach (var kvp in prefabLookup)
            {
                if (kvp.Value.GetComponent<T>() != null)
                {
                    GameObject item = wrapped.GetPooledItem(kvp.Value, Vector3.zero, Quaternion.identity);
                    return item?.GetComponent<T>();
                }
            }

            Debug.LogError($"[ItemPoolerAdapter] No prefab found with component type: {typeof(T).Name}");
            return null;
        }

        /// <summary>
        /// Return item to pool
        /// </summary>
        public void ReturnToPool(GameObject item)
        {
            wrapped.ReturnToPool(item);
        }

        /// <summary>
        /// Force return all active items to pools
        /// </summary>
        public void ClearAll()
        {
            // ItemPooler's Reset() method handles this
            wrapped.Reset();
        }

        /// <summary>
        /// Get count of active items in scene
        /// </summary>
        public int ActiveItemCount => wrapped.ActiveItemCount;

        /// <summary>
        /// Get count of specific item type active
        /// </summary>
        public int GetActiveCount(string itemType)
        {
            // ItemPooler doesn't track per-type counts yet
            // For now, return -1 to indicate not implemented
            Debug.LogWarning($"[ItemPoolerAdapter] GetActiveCount not implemented for type: {itemType}");
            return -1;
        }

        // === IResettable Implementation (forward to wrapped) ===

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void Reset()
        {
            wrapped.Reset();
        }

        /// <summary>
        /// Verify the service is properly cleaned
        /// </summary>
        public bool IsClean => wrapped.IsClean;

        // === Helper Methods ===

        /// <summary>
        /// Get pooled item with specific position and rotation
        /// </summary>
        public GameObject GetPooledItemAt(string itemType, Vector3 position, Quaternion rotation)
        {
            if (!prefabLookup.TryGetValue(itemType.ToLower(), out GameObject prefab))
            {
                Debug.LogError($"[ItemPoolerAdapter] Unknown item type: {itemType}");
                return null;
            }

            return wrapped.GetPooledItem(prefab, position, rotation);
        }
    }
}