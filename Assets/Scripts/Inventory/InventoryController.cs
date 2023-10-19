using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public List<Item> allPossibleItems;
    public Item defaultItem;
    public Transform boatTransform; // Reference to the boat's Transform
    public float tossForce = 5.0f; // Upward force to toss the item
    public Vector3 itemDropOffset = new Vector3(0, 1, 0); // Offset for dropping the item
    public int defaultItemCount = 50;  // Default count for the default item
    public int testItemCount = 10;     // Count for all other items (for testing)
    public int itemsUsedThisRound = 0;
    // Reference to GameState for updating inventory-related variables
    public GameState gameState;
    public InventoryState inventoryState;

    void Start()
    {
        Debug.Log("InventoryController: Start");
        inventoryState.Reset();  // Reset the inventory state
        LoadInventory();         // Then, load the inventory
    }

    public void LoadInventory()
    {
        Debug.Log("Loading Inventory");

        // Populate the inventory with all possible items
        foreach (Item item in allPossibleItems)
        {
            if (item == defaultItem) // If it's the default item
            {
                if(!inventoryState.inventory.ContainsKey(item.itemName)) // Check if it's not already added
                {
                    Debug.Log($"Adding {defaultItem.itemName} to inventory with count of {defaultItemCount}");
                    AddItem(item, defaultItemCount);
                    EquipItem(item.itemName); // Equip default item
                }
                else
                {
                    Debug.Log($"Inventory already has {inventoryState.inventory[defaultItem.itemName].count} of {defaultItem.itemName}");
                }
            }
            else // For all other items
            {
                AddItem(item, testItemCount); 
            }
        }
    }

    public void DropItem()
    {
        // First, check if an item is equipped
        if (string.IsNullOrEmpty(inventoryState.equippedItem))
        {
            Debug.LogWarning("No item equipped!");
            return;
        }

        // Check if the equipped item exists in the inventory
        if (inventoryState.inventory.ContainsKey(inventoryState.equippedItem))
        {
            InventoryState.InventoryItem invItem = inventoryState.inventory[inventoryState.equippedItem];

            // Make sure the player has at least one count of the item
            if (invItem.count <= 0)
            {
                Debug.LogWarning("No more of this item left to drop!");
                return;
            }

            // Find the Item object based on the equippedItem name
            Item itemToDrop = allPossibleItems.Find(item => item.itemName == inventoryState.equippedItem);

            
        if (itemToDrop != null)
        {
            Debug.Log("Dropping item: " + itemToDrop.itemName);
            Vector3 dropPosition = boatTransform.position + itemDropOffset;
            GameObject droppedItem = Instantiate(itemToDrop.itemPrefab, dropPosition, Quaternion.identity);

            // Check if the dropped item is a ragdoll
            if (itemToDrop.isRagdoll)  // Assume isRagdoll is a boolean flag in your Item class
            {
                RagdollController ragdollController = droppedItem.GetComponent<RagdollController>();
                ragdollController.item = itemToDrop;
                
                float boatRotationY = boatTransform.rotation.eulerAngles.y;
                Vector3 tossDirection = boatRotationY == 0 ? new Vector3(1, 1, 0) : new Vector3(-1, 1, 0);
                ragdollController.Throw(tossDirection, tossForce);  // Use the Throw method from RagdollController
            }
            else
            {
                ItemController itemController = droppedItem.GetComponent<ItemController>();
                itemController.item = itemToDrop;

                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float boatRotationY = boatTransform.rotation.eulerAngles.y;
                    Vector3 tossDirection = boatRotationY == 0 ? new Vector3(1, 1, 0) : new Vector3(-1, 1, 0);
                    rb.AddForce(tossDirection.normalized * tossForce, ForceMode.Impulse);
                }
                else
                {
                    Debug.LogError("Rigidbody not found on the dropped item.");
                }
            }

            // Reduce item count in inventory by 1
            itemsUsedThisRound++;
            RemoveItem(itemToDrop, 1);
        }
        else
        {
            Debug.LogError("Item to drop not found in inventoryItems list!");
        }
        }
        else
        {
            Debug.LogWarning("Equipped item not found in inventory!");
        }
    }
    public void ResetItemsUsedCount()
    {
        itemsUsedThisRound = 0;
    }
    public void AddItem(Item item, int count)
    {
        if (CanAddItem(item, count))
        {
            if (inventoryState.inventory.ContainsKey(item.itemName))
            {
                inventoryState.inventory[item.itemName].count += count;
            }
            else
            {
                InventoryState.InventoryItem newItem = new InventoryState.InventoryItem
                {
                    count = count,
                    weight = item.weight  // Use the new weight variable here
                };
                inventoryState.inventory.Add(item.itemName, newItem);
            }

            CheckWeight();
        }
    }


    public void RemoveItem(Item item, int count)
    {
        if (inventoryState.inventory.ContainsKey(item.itemName))
        {
            inventoryState.inventory[item.itemName].count -= count;
            if (inventoryState.inventory[item.itemName].count <= 0)
            {
                inventoryState.inventory.Remove(item.itemName);
            }

            CheckWeight();
        }
    }

    public void EquipItem(string itemName)
    {
        if (inventoryState.inventory.ContainsKey(itemName))
        {
            inventoryState.equippedItem = itemName;
        }
    }

    public void UnequipItem()
    {
        inventoryState.equippedItem = null;
    }

    public bool CanAddItem(Item item, int count)
    {
        float addedWeight = item.weight * count;
        return inventoryState.currentWeight + addedWeight <= inventoryState.maxWeight;
    }

    public void CheckWeight()
    {
        float totalWeight = 0.0f;
        foreach (var entry in inventoryState.inventory)
        {
            totalWeight += entry.Value.weight * entry.Value.count;
        }
        inventoryState.currentWeight = totalWeight;
    }
    public int GetItemCount(string itemName)
    {
        if (inventoryState.inventory.ContainsKey(itemName))
        {
            return inventoryState.inventory[itemName].count;
        }
        else
        {
            return 0; // Item not found in inventory
        }
    }
    public int TotalAvailableItems()
    {
        int totalItems = 0;
        foreach (var entry in inventoryState.inventory)
        {
            totalItems += entry.Value.count;
        }
        return totalItems;
    }
    public int TotalItemsUsedThisRound()
    {
        return itemsUsedThisRound;
    }

}

