using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemPooler : MonoBehaviour, IResettable
{
    public int defaultPoolSize = 5;
    private Dictionary<string, Queue<GameObject>> itemPools;
    private HashSet<GameObject> activeItems; // Track all items currently active in scene

    void Start()
    {
        itemPools = new Dictionary<string, Queue<GameObject>>();
        activeItems = new HashSet<GameObject>();
    }

    public GameObject GetPooledItem(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        if (!itemPools.ContainsKey(key))
        {
            CreateNewPool(prefab);
        }

        Queue<GameObject> pool = itemPools[key];

        GameObject item;
        if (pool.Count > 0)
        {
            // Get from pool
            item = pool.Dequeue();
            item.transform.position = position;
            item.transform.rotation = rotation;
        }
        else
        {
            // Create new at specified position (avoids origin spawn)
            item = Instantiate(prefab, position, rotation);
            item.name = key; // Reset the name as Instantiate appends "(Clone)" to it
        }

        // Activate AFTER positioning
        item.SetActive(true);
        activeItems.Add(item); // Track active item
        return item;
    }

    private void CreateNewPool(GameObject prefab)
    {
        Queue<GameObject> newPool = new Queue<GameObject>();
        for (int i = 0; i < defaultPoolSize; i++)
        {
            GameObject item = Instantiate(prefab);
            item.name = prefab.name; // Reset the name as Instantiate appends "(Clone)" to it
            item.SetActive(false);
            newPool.Enqueue(item);
        }
        itemPools.Add(prefab.name, newPool);
    }

    public void ReturnToPool(GameObject item)
    {
        string key = item.name;
        if (itemPools.ContainsKey(key))
        {
            item.SetActive(false);
            activeItems.Remove(item); // Remove from active tracking
            itemPools[key].Enqueue(item);
        }
        else
        {
            Debug.LogWarning("No pool exists for item: " + key);
        }
    }

    // === IResettable Implementation ===

    /// <summary>
    /// Reset pooler to initial state - return all active items to pools
    /// </summary>
    public void Reset()
    {
        // Return all active items to their pools
        var itemsToReturn = activeItems.ToList(); // Copy to avoid collection modification
        foreach (var item in itemsToReturn)
        {
            if (item != null)
            {
                ReturnToPool(item);
            }
        }

        // Clear the active items set
        activeItems.Clear();

        // Verify all pools are consistent
        foreach (var kvp in itemPools)
        {
            foreach (var pooledItem in kvp.Value)
            {
                if (pooledItem != null && pooledItem.activeSelf)
                {
                    // Force deactivate any stragglers
                    pooledItem.SetActive(false);
                    Debug.LogWarning($"[ItemPooler] Reset found active item in pool: {pooledItem.name}");
                }
            }
        }
    }

    /// <summary>
    /// Verify all items are properly pooled
    /// </summary>
    public bool IsClean
    {
        get
        {
            // Check no active items in scene
            bool noActiveItems = activeItems == null || activeItems.Count == 0;

            // Check all pooled items are inactive
            bool allPooledInactive = true;
            if (itemPools != null)
            {
                foreach (var kvp in itemPools)
                {
                    foreach (var item in kvp.Value)
                    {
                        if (item != null && item.activeSelf)
                        {
                            allPooledInactive = false;
                            Debug.LogError($"[ItemPooler] Active item found in pool during IsClean check: {item.name}");
                            break;
                        }
                    }
                    if (!allPooledInactive) break;
                }
            }

            return noActiveItems && allPooledInactive;
        }
    }

    /// <summary>
    /// Get count of active items (for debug/monitoring)
    /// </summary>
    public int ActiveItemCount => activeItems?.Count ?? 0;
}

