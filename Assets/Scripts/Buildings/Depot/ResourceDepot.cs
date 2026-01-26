using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// A building that stores resources.
/// Implements IResourceStorage for resource operations and IInteractable for player interaction.
/// </summary>
public class ResourceDepot : Building, IResourceStorage, IInteractable
{
    [BoxGroup("Depot Storage")]
    [ShowInInspector, ReadOnly]
    private ResourceInventory storage = new ResourceInventory();

    // Cache the typed data for convenience
    private ResourceDepotData DepotData => buildingData as ResourceDepotData;

    // Events
    public event Action<ResourceType, int> OnStorageChanged;

    #region IInteractable Implementation

    public string InteractionPrompt => $"Access {buildingData?.BuildingName ?? "Depot"}";

    public bool CanInteract(GameObject interactor)
    {
        return IsOperational;
    }

    public void Interact(GameObject interactor)
    {
        if (!IsOperational)
        {
            Debug.Log("[ResourceDepot] Cannot interact - depot is not operational");
            return;
        }

        Debug.Log($"[ResourceDepot] {interactor.name} is accessing the depot");

        // TODO: Open depot UI
        // DepotUI.Instance?.Open(this, interactor.GetComponent<IResourceHolder>());

        // For now, just log contents
        LogContents();
    }

    #endregion

    #region IResourceStorage Implementation

    public bool CanAcceptResource(ResourceType resourceType)
    {
        if (resourceType == ResourceType.None) return false;
        if (!IsOperational) return false;

        if (DepotData == null) return true;

        if (DepotData.AcceptAllResources) return true;

        return DepotData.AcceptedResources.Contains(resourceType);
    }

    public int TryDeposit(ResourceType resourceType, int amount)
    {
        if (!CanAcceptResource(resourceType) || amount <= 0)
            return 0;

        int capacity = GetRemainingCapacity(resourceType);
        int toDeposit = Mathf.Min(amount, capacity);

        if (toDeposit > 0)
        {
            storage.Add(resourceType, toDeposit);
            OnStorageChanged?.Invoke(resourceType, storage.Get(resourceType));

            Debug.Log($"[ResourceDepot] Deposited {toDeposit}x {resourceType}. " +
                      $"Total: {storage.Get(resourceType)}");
        }

        return toDeposit;
    }

    public int TryWithdraw(ResourceType resourceType, int amount)
    {
        if (resourceType == ResourceType.None || amount <= 0 || !IsOperational)
            return 0;

        int available = storage.Get(resourceType);
        int toWithdraw = Mathf.Min(amount, available);

        if (toWithdraw > 0)
        {
            storage.TryRemove(resourceType, toWithdraw);
            OnStorageChanged?.Invoke(resourceType, storage.Get(resourceType));

            Debug.Log($"[ResourceDepot] Withdrew {toWithdraw}x {resourceType}. " +
                      $"Remaining: {storage.Get(resourceType)}");
        }

        return toWithdraw;
    }

    public int GetStoredAmount(ResourceType resourceType)
    {
        return storage.Get(resourceType);
    }

    public int GetRemainingCapacity(ResourceType resourceType)
    {
        if (!CanAcceptResource(resourceType))
            return 0;

        int maxCapacity = DepotData?.CapacityPerResource ?? int.MaxValue;
        int current = storage.Get(resourceType);

        return maxCapacity - current;
    }

    public IEnumerable<KeyValuePair<ResourceType, int>> GetAllStored()
    {
        return storage.GetAll();
    }

    #endregion

    #region Building Overrides

    protected override void Awake()
    {
        base.Awake();

        // Subscribe to storage changes
        storage.OnResourceChanged += HandleStorageChanged;
    }

    protected override void OnDestroy()
    {
        storage.OnResourceChanged -= HandleStorageChanged;
        base.OnDestroy();
    }

    protected override void OnDestroyBuilding()
    {
        // When depot is destroyed, log what resources were lost
        int totalLost = storage.GetTotalCount();
        if (totalLost > 0)
        {
            Debug.Log($"[ResourceDepot] Depot destroyed! Resources lost: {totalLost}");

            // TODO: Optionally spawn resource pickups, transfer to player, etc.
        }

        base.OnDestroyBuilding();
    }

    #endregion

    private void HandleStorageChanged(ResourceType type, int newAmount)
    {
        OnStorageChanged?.Invoke(type, newAmount);
    }

    /// <summary>
    /// Get the total number of resources stored.
    /// </summary>
    public int GetTotalStoredCount() => storage.GetTotalCount();

    /// <summary>
    /// Get the number of distinct resource types stored.
    /// </summary>
    public int GetStoredTypeCount() => storage.GetDistinctTypeCount();

    [Button("Log Contents"), BoxGroup("Debug")]
    private void LogContents()
    {
        int capacity = DepotData?.CapacityPerResource ?? 0;
        Debug.Log($"[ResourceDepot] {buildingData?.BuildingName ?? name} contents:");

        var stored = storage.GetAll().ToList();
        if (stored.Count == 0)
        {
            Debug.Log("  (empty)");
        }
        else
        {
            foreach (var kvp in stored)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value}/{capacity}");
            }
        }
    }

#if UNITY_EDITOR
    [Button("Add Test Resources"), BoxGroup("Debug")]
    private void DebugAddResources()
    {
        TryDeposit(ResourceType.Wood, 50);
        TryDeposit(ResourceType.Stone, 30);
        TryDeposit(ResourceType.IronOre, 10);
    }

    [Button("Clear Storage"), BoxGroup("Debug")]
    private void DebugClearStorage()
    {
        storage.Clear();
        Debug.Log("[ResourceDepot] Storage cleared");
    }
#endif
}
