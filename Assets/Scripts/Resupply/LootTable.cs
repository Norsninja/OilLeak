using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines loot rewards for resupply crates
/// </summary>
[CreateAssetMenu(fileName = "LootTable", menuName = "OilLeak/LootTable")]
public class LootTable : ScriptableObject
{
    [Header("Tier Info")]
    public string tierName = "Grassroots";
    public LootTier tier = LootTier.Grassroots;

    [Header("Loot Entries")]
    public List<LootEntry> lootEntries = new List<LootEntry>();

    [Header("Quantity Modifiers")]
    [Tooltip("Multiplier for all quantities in this table")]
    public float quantityMultiplier = 1f;

    [Tooltip("Guaranteed minimum total items")]
    public int minimumTotalItems = 5;

    /// <summary>
    /// Generate loot from this table
    /// </summary>
    public List<(Item, int)> GenerateLoot()
    {
        var result = new List<(Item, int)>();
        int totalItems = 0;

        // Process each entry
        foreach (var entry in lootEntries)
        {
            if (entry.item == null) continue;

            // Check if this item drops (based on weight/chance)
            if (Random.value <= entry.dropChance)
            {
                // Calculate quantity
                int quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                quantity = Mathf.RoundToInt(quantity * quantityMultiplier);

                if (quantity > 0)
                {
                    result.Add((entry.item, quantity));
                    totalItems += quantity;
                }
            }
        }

        // Ensure minimum items (add concrete blocks as filler)
        if (totalItems < minimumTotalItems)
        {
            // Find concrete block or first available item
            Item fillerItem = null;
            foreach (var entry in lootEntries)
            {
                if (entry.item != null && entry.item.itemName.Contains("Concrete"))
                {
                    fillerItem = entry.item;
                    break;
                }
            }

            // Fallback to first item
            if (fillerItem == null && lootEntries.Count > 0)
            {
                fillerItem = lootEntries[0].item;
            }

            if (fillerItem != null)
            {
                int deficit = minimumTotalItems - totalItems;
                result.Add((fillerItem, deficit));
            }
        }

        return result;
    }

    /// <summary>
    /// Get a description of possible loot
    /// </summary>
    public string GetLootDescription()
    {
        string desc = $"{tierName} Supplies:\n";
        foreach (var entry in lootEntries)
        {
            if (entry.item != null)
            {
                desc += $"- {entry.item.itemName}: {entry.minQuantity}-{entry.maxQuantity}\n";
            }
        }
        return desc;
    }
}

[System.Serializable]
public class LootEntry
{
    public Item item;

    [Range(0f, 1f)]
    [Tooltip("Chance this item will be included (0-1)")]
    public float dropChance = 1f;

    [Tooltip("Minimum quantity to give")]
    public int minQuantity = 1;

    [Tooltip("Maximum quantity to give")]
    public int maxQuantity = 5;

    [Tooltip("Weight for weighted random selection")]
    public float weight = 1f;
}

public enum LootTier
{
    Grassroots,    // Basic supplies: sponges, sandbags, tarps
    Corporate,     // "Professional": golf balls, rubber, chemicals
    Desperation,   // Junk shot: metal scraps, concrete, debris
    Absurdist      // Satirical: thoughts & prayers, money bundles
}