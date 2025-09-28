using UnityEngine;
using OilLeak.Inventory;

namespace Core
{
    /// <summary>
    /// Adapter that bridges ItemCatalog ScriptableObject to IItemLookupService.
    /// Follows the adapter pattern used throughout the codebase.
    /// </summary>
    public class ItemCatalogAdapter : IItemLookupService
    {
        private readonly ItemCatalog catalog;

        public ItemCatalogAdapter(ItemCatalog catalog)
        {
            this.catalog = catalog;

            if (catalog == null)
            {
                Debug.LogError("[ItemCatalogAdapter] Created with null catalog!");
            }
        }

        /// <summary>
        /// Get an item by its name from the catalog
        /// </summary>
        public Item GetByName(string itemName)
        {
            if (catalog == null)
            {
                Debug.LogError("[ItemCatalogAdapter] Catalog is null!");
                return null;
            }

            var item = catalog.GetByName(itemName);

            if (item == null && !string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[ItemCatalogAdapter] Item '{itemName}' not found in catalog");
            }

            return item;
        }

        /// <summary>
        /// Check if an item exists in the catalog
        /// </summary>
        public bool Exists(string itemName)
        {
            if (catalog == null)
            {
                Debug.LogError("[ItemCatalogAdapter] Catalog is null!");
                return false;
            }

            return catalog.Exists(itemName);
        }

        /// <summary>
        /// Get the total number of items in the catalog
        /// </summary>
        public int TotalItemCount
        {
            get
            {
                if (catalog == null)
                {
                    Debug.LogError("[ItemCatalogAdapter] Catalog is null!");
                    return 0;
                }
                return catalog.Count;
            }
        }
    }
}