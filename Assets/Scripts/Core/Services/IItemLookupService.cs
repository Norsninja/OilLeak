namespace Core
{
    /// <summary>
    /// Service interface for looking up items from the central catalog.
    /// Avoids Resources lookups and string searches at runtime.
    /// </summary>
    public interface IItemLookupService
    {
        /// <summary>
        /// Get an item by its name
        /// </summary>
        Item GetByName(string itemName);

        /// <summary>
        /// Check if an item exists in the catalog
        /// </summary>
        bool Exists(string itemName);

        /// <summary>
        /// Get the total number of items in the catalog
        /// </summary>
        int TotalItemCount { get; }
    }
}