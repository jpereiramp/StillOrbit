using System;
using System.Collections.Generic;

/// <summary>
/// Interface for buildings that can store resources.
/// Implemented by: ResourceDepot, and potentially player backpacks, companion storage, etc.
/// </summary>
public interface IResourceStorage
{
    /// <summary>
    /// Check if this storage accepts a specific resource type.
    /// </summary>
    bool CanAcceptResource(ResourceType resourceType);

    /// <summary>
    /// Try to deposit resources into storage.
    /// </summary>
    /// <param name="resourceType">The type of resource to deposit.</param>
    /// <param name="amount">The amount to deposit.</param>
    /// <returns>The actual amount deposited (may be less if storage is full).</returns>
    int TryDeposit(ResourceType resourceType, int amount);

    /// <summary>
    /// Try to withdraw resources from storage.
    /// </summary>
    /// <param name="resourceType">The type of resource to withdraw.</param>
    /// <param name="amount">The amount to withdraw.</param>
    /// <returns>The actual amount withdrawn (may be less if insufficient stock).</returns>
    int TryWithdraw(ResourceType resourceType, int amount);

    /// <summary>
    /// Get current amount of a specific resource.
    /// </summary>
    int GetStoredAmount(ResourceType resourceType);

    /// <summary>
    /// Get remaining capacity for a specific resource.
    /// Returns int.MaxValue if unlimited.
    /// </summary>
    int GetRemainingCapacity(ResourceType resourceType);

    /// <summary>
    /// Get all stored resources and their amounts.
    /// </summary>
    IEnumerable<KeyValuePair<ResourceType, int>> GetAllStored();

    /// <summary>
    /// Fired when storage contents change.
    /// Parameters: (ResourceType type, int newAmount)
    /// </summary>
    event Action<ResourceType, int> OnStorageChanged;
}
