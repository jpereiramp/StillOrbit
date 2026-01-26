using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles resource storage for the companion.
/// Implements <see cref="IResourceHolder"/> for compatibility with existing systems.
///
/// <para><b>Extension Points:</b></para>
/// <list type="bullet">
///   <item><see cref="OnResourcesChanged"/> - Subscribe to track individual resource changes</item>
///   <item><see cref="OnInventoryCleared"/> - Subscribe to react when inventory is emptied</item>
/// </list>
///
/// <para><b>Capacity Limits (Future Extension):</b></para>
/// <para>To add capacity limits, override <see cref="AddResources"/> to check against a max capacity
/// defined in <see cref="CompanionData"/>, and return partial amounts when exceeding capacity.</para>
///
/// <para><b>Integration:</b></para>
/// <list type="bullet">
///   <item>Uses <see cref="ResourceInventory"/> internally for O(1) storage operations</item>
///   <item>Respects <see cref="CompanionData.CanAcceptResource"/> for resource filtering</item>
///   <item>Compatible with <see cref="IResourceStorage"/> for depot deposits</item>
/// </list>
/// </summary>
public class CompanionInventory : MonoBehaviour, IResourceHolder
{
    [BoxGroup("References")]
    [SerializeField] private CompanionCoreController controller;

    [BoxGroup("Storage")]
    [ShowInInspector, ReadOnly]
    private ResourceInventory inventory = new ResourceInventory();

    // Events (from IResourceHolder)
    public event Action<ResourceType, int> OnResourcesChanged;

    // Additional events
    public event Action OnInventoryCleared;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CompanionCoreController>();
        }

        // Subscribe to internal inventory changes
        inventory.OnResourceChanged += HandleResourceChanged;
    }

    private void OnDestroy()
    {
        inventory.OnResourceChanged -= HandleResourceChanged;
    }

    /// <summary>
    /// Get the amount of a specific resource.
    /// </summary>
    public int GetResourceAmount(ResourceType type)
    {
        return inventory.Get(type);
    }

    /// <summary>
    /// Add resources to the companion's inventory.
    /// Returns the amount actually added.
    /// </summary>
    public int AddResources(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        if (type == ResourceType.None) return 0;

        // Check if companion accepts this resource type
        if (controller != null && controller.Data != null)
        {
            if (!controller.Data.CanAcceptResource(type))
            {
                Debug.Log($"[CompanionInventory] Companion does not accept {type}");
                return 0;
            }
        }

        inventory.Add(type, amount);
        Debug.Log($"[CompanionInventory] Added {amount}x {type}. Total: {inventory.Get(type)}");

        return amount;
    }

    /// <summary>
    /// Remove resources from the companion's inventory.
    /// All-or-nothing operation.
    /// </summary>
    public bool RemoveResources(ResourceType type, int amount)
    {
        if (!HasResources(type, amount)) return false;

        bool success = inventory.TryRemove(type, amount);

        if (success)
        {
            Debug.Log($"[CompanionInventory] Removed {amount}x {type}. Remaining: {inventory.Get(type)}");
        }

        return success;
    }

    /// <summary>
    /// Check if the companion has at least this many resources.
    /// </summary>
    public bool HasResources(ResourceType type, int amount)
    {
        return inventory.Has(type, amount);
    }

    /// <summary>
    /// Check if the companion is carrying any resources.
    /// </summary>
    public bool HasAnyResources()
    {
        return inventory.GetTotalCount() > 0;
    }

    /// <summary>
    /// Get total count of all resources.
    /// </summary>
    public int GetTotalResourceCount()
    {
        return inventory.GetTotalCount();
    }

    /// <summary>
    /// Get all stored resources.
    /// </summary>
    public IEnumerable<KeyValuePair<ResourceType, int>> GetAllResources()
    {
        return inventory.GetAll();
    }

    /// <summary>
    /// Clear all resources from inventory.
    /// </summary>
    public void ClearInventory()
    {
        inventory.Clear();
        OnInventoryCleared?.Invoke();
        Debug.Log("[CompanionInventory] Inventory cleared");
    }

    /// <summary>
    /// Transfer all resources from a source (typically player) to companion.
    /// Returns total amount transferred.
    /// </summary>
    public int TransferAllFrom(IResourceHolder source)
    {
        if (source == null) return 0;

        int totalTransferred = 0;

        // We need to iterate over all resource types
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;

            int sourceAmount = source.GetResourceAmount(type);
            if (sourceAmount <= 0) continue;

            // Check if companion accepts this type
            if (controller != null && controller.Data != null)
            {
                if (!controller.Data.CanAcceptResource(type)) continue;
            }

            // Remove from source
            if (source.RemoveResources(type, sourceAmount))
            {
                // Add to companion
                AddResources(type, sourceAmount);
                totalTransferred += sourceAmount;
            }
        }

        if (totalTransferred > 0)
        {
            Debug.Log($"[CompanionInventory] Transferred {totalTransferred} total resources from source");
        }

        return totalTransferred;
    }

    /// <summary>
    /// Transfer all resources to a storage building.
    /// Returns total amount deposited.
    /// </summary>
    public int TransferAllTo(IResourceStorage storage)
    {
        if (storage == null) return 0;

        int totalDeposited = 0;

        // Snapshot current inventory to avoid modification during iteration
        var snapshot = new List<KeyValuePair<ResourceType, int>>(GetAllResources());

        foreach (var kvp in snapshot)
        {
            if (!storage.CanAcceptResource(kvp.Key)) continue;

            int toDeposit = kvp.Value;

            // Check storage capacity
            int capacity = storage.GetRemainingCapacity(kvp.Key);
            if (capacity < toDeposit && capacity != int.MaxValue)
            {
                toDeposit = capacity;
            }

            if (toDeposit <= 0) continue;

            // Deposit to storage
            int deposited = storage.TryDeposit(kvp.Key, toDeposit);

            if (deposited > 0)
            {
                // Remove from companion
                RemoveResources(kvp.Key, deposited);
                totalDeposited += deposited;
            }
        }

        if (totalDeposited > 0)
        {
            Debug.Log($"[CompanionInventory] Deposited {totalDeposited} total resources to storage");
        }

        return totalDeposited;
    }

    private void HandleResourceChanged(ResourceType type, int newAmount)
    {
        OnResourcesChanged?.Invoke(type, newAmount);
    }

#if UNITY_EDITOR
    [Button("Add Test Resources"), BoxGroup("Debug")]
    private void DebugAddResources()
    {
        AddResources(ResourceType.Wood, 10);
        AddResources(ResourceType.Stone, 5);
        AddResources(ResourceType.IronOre, 3);
    }

    [Button("Clear Inventory"), BoxGroup("Debug")]
    private void DebugClearInventory()
    {
        ClearInventory();
    }

    [Button("Log Contents"), BoxGroup("Debug")]
    private void DebugLogContents()
    {
        Debug.Log($"[CompanionInventory] Total: {GetTotalResourceCount()}");
        foreach (var kvp in GetAllResources())
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value}");
        }
    }
#endif
}