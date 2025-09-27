using System;
using System.Collections.Generic;
using UnityEngine;

namespace OilLeak.Inventory
{
    /// <summary>
    /// Configuration for integrity-tier based item gating.
    /// Controls which items are available at each integrity tier for resupply drops.
    /// </summary>
    [CreateAssetMenu(menuName = "OilLeak/ItemGatingConfig", fileName = "ItemGatingConfig")]
    public class ItemGatingConfig : ScriptableObject
    {
        /// <summary>
        /// Defines item availability for a specific integrity tier
        /// </summary>
        [Serializable]
        public class TierConfig
        {
            [Header("Tier Settings")]
            [Tooltip("Integrity tier (1=Failing, 2=Critical, 3=Damaged, 4=Stable, 5=Pristine)")]
            [Range(1, 5)]
            public int tier = 5;

            [Tooltip("Display name for this tier")]
            public string tierName = "Pristine";

            [Header("Available Items")]
            [Tooltip("Items that can appear in resupply drops at this tier")]
            public List<GatedItem> availableItems = new List<GatedItem>();

            [Serializable]
            public class GatedItem
            {
                [Tooltip("The item reference")]
                public Item item;

                [Tooltip("Can this item appear in resupply drops at this tier?")]
                public bool allowedInResupply = true;

                [Tooltip("Can this item be in starting inventory? (Usually false except for starter items)")]
                public bool allowedInStartingInventory = false;
            }
        }

        [Header("Tier Configurations")]
        [Tooltip("Configure item availability per integrity tier. Tier numbers match FutilitySystem tiers.")]
        [SerializeField] private List<TierConfig> tierConfigs = new List<TierConfig>
        {
            // Default tier setup matching Senior Dev's spec
            new TierConfig { tier = 5, tierName = "Pristine" },  // 90-100%
            new TierConfig { tier = 4, tierName = "Stable" },    // 80-90%
            new TierConfig { tier = 3, tierName = "Damaged" },   // 60-80%
            new TierConfig { tier = 2, tierName = "Critical" },  // 30-60%
            new TierConfig { tier = 1, tierName = "Failing" }    // 0-30%
        };

        // Cache for fast tier lookups
        private Dictionary<int, TierConfig> tierLookup;

        void OnEnable()
        {
            BuildTierCache();
        }

        void OnValidate()
        {
            BuildTierCache();
            ValidateTierUniqueness();
        }

        private void BuildTierCache()
        {
            tierLookup = new Dictionary<int, TierConfig>();

            foreach (var config in tierConfigs)
            {
                if (tierLookup.ContainsKey(config.tier))
                {
                    Debug.LogWarning($"[ItemGatingConfig] Duplicate tier {config.tier} found!");
                    continue;
                }
                tierLookup[config.tier] = config;
            }
        }

        private void ValidateTierUniqueness()
        {
            HashSet<int> seen = new HashSet<int>();
            foreach (var config in tierConfigs)
            {
                if (seen.Contains(config.tier))
                {
                    Debug.LogError($"[ItemGatingConfig] Duplicate tier configuration for tier {config.tier}!");
                }
                seen.Add(config.tier);
            }

            // Ensure we have all 5 tiers
            for (int i = 1; i <= 5; i++)
            {
                if (!seen.Contains(i))
                {
                    Debug.LogWarning($"[ItemGatingConfig] Missing configuration for tier {i}");
                }
            }
        }

        /// <summary>
        /// Get items allowed in resupply for the given integrity tier
        /// </summary>
        public List<Item> GetAllowedItemsForTier(int tier)
        {
            if (tierLookup == null || tierLookup.Count == 0)
                BuildTierCache();

            var allowedItems = new List<Item>();

            // Get config for this tier
            if (!tierLookup.TryGetValue(tier, out TierConfig tierConfig))
            {
                Debug.LogWarning($"[ItemGatingConfig] No configuration for tier {tier}");
                return allowedItems;
            }

            // Collect all allowed items
            foreach (var gatedItem in tierConfig.availableItems)
            {
                if (gatedItem.item != null && gatedItem.allowedInResupply)
                {
                    allowedItems.Add(gatedItem.item);
                }
            }

            return allowedItems;
        }

        /// <summary>
        /// Check if a specific item is allowed at the given tier
        /// </summary>
        public bool IsItemAllowedAtTier(Item item, int tier)
        {
            if (item == null) return false;

            if (tierLookup == null || tierLookup.Count == 0)
                BuildTierCache();

            if (!tierLookup.TryGetValue(tier, out TierConfig tierConfig))
                return false;

            foreach (var gatedItem in tierConfig.availableItems)
            {
                if (gatedItem.item == item)
                    return gatedItem.allowedInResupply;
            }

            return false;
        }

        /// <summary>
        /// Filter a list of items to only those allowed at the current tier
        /// </summary>
        public List<Item> FilterItemsForTier(List<Item> candidateItems, int tier)
        {
            var filtered = new List<Item>();

            foreach (var item in candidateItems)
            {
                if (IsItemAllowedAtTier(item, tier))
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Get the tier configuration for a specific tier
        /// </summary>
        public TierConfig GetTierConfig(int tier)
        {
            if (tierLookup == null || tierLookup.Count == 0)
                BuildTierCache();

            tierLookup.TryGetValue(tier, out TierConfig config);
            return config;
        }
    }
}