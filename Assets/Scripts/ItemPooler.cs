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

    public GameObject GetPooledItem(GameObject prefab)
    {
        string key = prefab.name;

        if (!itemPools.ContainsKey(key))
        {
            CreateNewPool(prefab);
        }

        Queue<GameObject> pool = itemPools[key];

        if (pool.Count > 0)
        {
            GameObject item = pool.Dequeue();
            item.SetActive(true);
            return item;
        }
        else
        {
            GameObject item = Instantiate(prefab);
            item.name = key; // Reset the name as Instantiate appends "(Clone)" to it
            return item;
        }
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

