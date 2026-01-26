using System;

/// <summary>
/// Interface for any entity that can hold resources.
/// Implemented by: Player, Buildings (ResourceDepot), Companions.
///
/// This abstraction allows systems to interact with resource storage
/// without knowing the concrete implementation.
/// </summary>
public interface IResourceHolder
{
    /// <summary>
    /// Get current amount of a specific resource.
    /// </summary>
    int GetResourceAmount(ResourceType type);

    /// <summary>
    /// Try to add resources. Returns the amount actually added.
    /// May be less than requested if storage has capacity limits.
    /// </summary>
    int AddResources(ResourceType type, int amount);

    /// <summary>
    /// Try to remove resources. Returns true if the full amount was removed.
    /// Returns false if insufficient resources (no partial removal).
    /// </summary>
    bool RemoveResources(ResourceType type, int amount);

    /// <summary>
    /// Check if this holder has at least the specified amount of a resource.
    /// </summary>
    bool HasResources(ResourceType type, int amount);

    /// <summary>
    /// Fired when any resource amount changes.
    /// Parameters: (ResourceType type, int newAmount)
    /// </summary>
    event Action<ResourceType, int> OnResourcesChanged;
}
