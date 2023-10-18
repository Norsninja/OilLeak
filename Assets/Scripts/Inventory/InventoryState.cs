using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryState", menuName = "Game/InventoryState")]
public class InventoryState : ScriptableObject
{
    [System.Serializable]
    public class InventoryItem
    {
        public int count;
        public float weight;
    }

    public Dictionary<string, InventoryItem> inventory = new Dictionary<string, InventoryItem>();
    public float maxWeight = 1000.0f; // Arbitrary value, change as needed
    public float currentWeight = 0.0f;
    public string equippedItem;
    public void Reset()
    {
        inventory.Clear();
        currentWeight = 0.0f;
        equippedItem = null;
    }
}

