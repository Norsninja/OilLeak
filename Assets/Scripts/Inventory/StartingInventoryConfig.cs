using System;
using System.Collections.Generic;
using UnityEngine;

namespace OilLeak.Inventory
{
    /// <summary>
    /// Configuration for what items the player starts with at the beginning of a run.
    /// Replaces the old allPossibleItems/defaultItem system with explicit starting inventory.
    /// </summary>
    [CreateAssetMenu(menuName = "OilLeak/StartingInventoryConfig", fileName = "StartingInventoryConfig")]
    public class StartingInventoryConfig : ScriptableObject
    {
        /// <summary>
        /// Defines a starting item with quantity and auto-equip flag
        /// </summary>
        [Serializable]
        public class StartingItem
        {
            [Tooltip("The item to add to starting inventory")]
            public Item item;

            [Tooltip("How many of this item to start with")]
            [Min(1)]
            public int quantity = 10;

            [Tooltip("Should this item be auto-equipped on game start?")]
            public bool autoEquip = false;
        }

        [Header("Starting Items")]
        [Tooltip("Items the player starts with. First item with autoEquip=true will be equipped, or first item if none flagged.")]
        [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

        /// <summary>
        /// Get the list of starting items
        /// </summary>
        public IReadOnlyList<StartingItem> StartingItems => startingItems;

        /// <summary>
        /// Find the item that should be auto-equipped at start
        /// </summary>
        public Item GetAutoEquipItem()
        {
            // First, look for explicitly flagged item
            foreach (var startingItem in startingItems)
            {
                if (startingItem.item != null && startingItem.autoEquip)
                    return startingItem.item;
            }

            // Fallback to first item in list
            if (startingItems.Count > 0 && startingItems[0].item != null)
                return startingItems[0].item;

            return null;
        }

        void OnValidate()
        {
            // Validate that we have at least one item
            if (startingItems.Count == 0)
            {
                Debug.LogWarning("[StartingInventoryConfig] No starting items configured!");
            }

            // Check for duplicate items
            HashSet<string> seen = new HashSet<string>();
            foreach (var startingItem in startingItems)
            {
                if (startingItem.item == null) continue;

                if (seen.Contains(startingItem.item.itemName))
                {
                    Debug.LogWarning($"[StartingInventoryConfig] Duplicate item in starting inventory: {startingItem.item.itemName}");
                }
                seen.Add(startingItem.item.itemName);
            }

            // Warn if multiple auto-equip items
            int autoEquipCount = 0;
            foreach (var startingItem in startingItems)
            {
                if (startingItem.autoEquip) autoEquipCount++;
            }
            if (autoEquipCount > 1)
            {
                Debug.LogWarning($"[StartingInventoryConfig] Multiple items marked for auto-equip. Only the first will be equipped.");
            }
        }
    }
}