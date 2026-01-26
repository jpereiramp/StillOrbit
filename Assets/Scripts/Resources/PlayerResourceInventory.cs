using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Player's resource inventory component.
/// Stores bulk resources (wood, stone, ore, etc.) separately from slot-based item inventory.
///
/// Attach to the Player GameObject alongside PlayerInventory.
/// </summary>
public class PlayerResourceInventory : MonoBehaviour, IResourceHolder
{
    [BoxGroup("Storage")]
    [SerializeField]
    private ResourceInventory inventory = new ResourceInventory();

    /// <summary>
    /// Fired when any resource amount changes.
    /// </summary>
    public event Action<ResourceType, int> OnResourcesChanged;

    private void Awake()
    {
        inventory.OnResourceChanged += HandleResourceChanged;
    }

    private void OnDestroy()
    {
        inventory.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType type, int newAmount)
    {
        OnResourcesChanged?.Invoke(type, newAmount);
    }

    #region IResourceHolder Implementation

    /// <inheritdoc/>
    public int GetResourceAmount(ResourceType type)
    {
        return inventory.Get(type);
    }

    /// <inheritdoc/>
    public int AddResources(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;

        inventory.Add(type, amount);

        Debug.Log($"[PlayerResourceInventory] Added {amount}x {type}. Total: {inventory.Get(type)}");
        return amount;
    }

    /// <inheritdoc/>
    public bool RemoveResources(ResourceType type, int amount)
    {
        bool success = inventory.TryRemove(type, amount);

        if (success)
        {
            Debug.Log($"[PlayerResourceInventory] Removed {amount}x {type}. Remaining: {inventory.Get(type)}");
        }
        else
        {
            Debug.LogWarning($"[PlayerResourceInventory] Failed to remove {amount}x {type}. Only have {inventory.Get(type)}");
        }

        return success;
    }

    /// <inheritdoc/>
    public bool HasResources(ResourceType type, int amount)
    {
        return inventory.Has(type, amount);
    }

    #endregion

    #region Additional Utility Methods

    /// <summary>
    /// Get the underlying resource inventory for direct access (e.g., UI binding).
    /// </summary>
    public ResourceInventory GetInventory() => inventory;

    /// <summary>
    /// Get total count of all resources held.
    /// </summary>
    public int GetTotalResourceCount() => inventory.GetTotalCount();

    /// <summary>
    /// Check if any resources are held.
    /// </summary>
    public bool HasAnyResources() => inventory.GetDistinctTypeCount() > 0;

    #endregion

#if UNITY_EDITOR
    [Button("Add Test Resources"), BoxGroup("Debug")]
    private void DebugAddTestResources()
    {
        AddResources(ResourceType.Wood, 10);
        AddResources(ResourceType.Stone, 5);
        AddResources(ResourceType.IronOre, 3);
    }

    [Button("Clear All Resources"), BoxGroup("Debug")]
    private void DebugClearResources()
    {
        inventory.Clear();
        Debug.Log("[PlayerResourceInventory] Cleared all resources");
    }

    [Button("Log All Resources"), BoxGroup("Debug")]
    private void DebugLogResources()
    {
        Debug.Log("[PlayerResourceInventory] Current resources:");
        foreach (var kvp in inventory.GetAll())
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }
#endif
}
