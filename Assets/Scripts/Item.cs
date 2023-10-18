using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Item", order = 2)]
public class Item : ScriptableObject
{
    public string itemName;
    public float buoyancy;
    public float weight;  // Add this new weight variable
    public int cost;
    public GameObject itemPrefab;  // Reference to the item's prefab
    public bool isRagdoll;         // Flag to identify if the item is a ragdoll
}

