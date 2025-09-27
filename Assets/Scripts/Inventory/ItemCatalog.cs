using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OilLeak.Inventory
{
    /// <summary>
    /// Central catalog of all available items in the game.
    /// Single source of truth for Item references to avoid Resources lookups.
    /// </summary>
    [CreateAssetMenu(menuName = "OilLeak/ItemCatalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        [Header("All Available Items")]
        [Tooltip("Complete list of all items that exist in the game")]
        [SerializeField] private List<Item> items = new List<Item>();

        // Fast lookup cache built on enable
        private Dictionary<string, Item> itemLookup;

        /// <summary>
        /// All items in the catalog
        /// </summary>
        public IReadOnlyList<Item> Items => items;

        void OnEnable()
        {
            BuildLookupCache();
        }

        void OnValidate()
        {
            // Rebuild cache when items are modified in editor
            BuildLookupCache();
        }

        private void BuildLookupCache()
        {
            itemLookup = new Dictionary<string, Item>();

            foreach (var item in items)
            {
                if (item == null) continue;

                if (string.IsNullOrEmpty(item.itemName))
                {
                    Debug.LogWarning($"[ItemCatalog] Item {item.name} has no itemName set!");
                    continue;
                }

                if (itemLookup.ContainsKey(item.itemName))
                {
                    Debug.LogWarning($"[ItemCatalog] Duplicate item name: {item.itemName}");
                    continue;
                }

                itemLookup[item.itemName] = item;
            }

            Debug.Log($"[ItemCatalog] Built lookup cache with {itemLookup.Count} items");
        }

        /// <summary>
        /// Get an item by its name. Returns null if not found.
        /// </summary>
        public Item GetByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return null;

            // Ensure cache is built
            if (itemLookup == null || itemLookup.Count == 0)
                BuildLookupCache();

            itemLookup.TryGetValue(itemName, out Item item);
            return item;
        }

        /// <summary>
        /// Check if an item exists in the catalog
        /// </summary>
        public bool Exists(string itemName)
        {
            return GetByName(itemName) != null;
        }

        /// <summary>
        /// Get total count of items in catalog
        /// </summary>
        public int Count => items?.Count ?? 0;
    }
}