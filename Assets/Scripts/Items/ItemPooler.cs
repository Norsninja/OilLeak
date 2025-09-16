using System.Collections.Generic;
using UnityEngine;

public class ItemPooler : MonoBehaviour
{
    public int defaultPoolSize = 5;
    private Dictionary<string, Queue<GameObject>> itemPools;

    void Start()
    {
        itemPools = new Dictionary<string, Queue<GameObject>>();
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
            itemPools[key].Enqueue(item);
        }
        else
        {
            Debug.LogWarning("No pool exists for item: " + key);
        }
    }
}

