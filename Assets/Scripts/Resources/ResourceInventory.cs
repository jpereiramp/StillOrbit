using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Lightweight resource storage using key-value pairs.
/// Unlike slot-based inventory, this is optimized for bulk quantities.
///
/// Used internally by PlayerResourceInventory, ResourceDepot, etc.
/// </summary>
[Serializable]
public class ResourceInventory
{
    [Serializable]
    public struct ResourceEntry
    {
        public ResourceType Type;
        public int Amount;

        public ResourceEntry(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    [SerializeField, TableList]
    private List<ResourceEntry> entries = new List<ResourceEntry>();

    // Runtime dictionary for O(1) lookups (built from serialized list)
    [NonSerialized]
    private Dictionary<ResourceType, int> runtimeCache;

    [NonSerialized]
    private bool cacheInitialized;

    /// <summary>
    /// Fired when any resource amount changes.
    /// Parameters: (ResourceType type, int newAmount)
    /// </summary>
    public event Action<ResourceType, int> OnResourceChanged;

    /// <summary>
    /// Ensure the runtime cache is initialized from serialized data.
    /// </summary>
    private void EnsureCacheInitialized()
    {
        if (cacheInitialized) return;

        runtimeCache = new Dictionary<ResourceType, int>();
        foreach (var entry in entries)
        {
            if (entry.Type != ResourceType.None && entry.Amount > 0)
            {
                runtimeCache[entry.Type] = entry.Amount;
            }
        }
        cacheInitialized = true;
    }

    /// <summary>
    /// Sync runtime cache back to serialized list (for persistence).
    /// </summary>
    private void SyncToSerializedList()
    {
        entries.Clear();
        foreach (var kvp in runtimeCache)
        {
            if (kvp.Value > 0)
            {
                entries.Add(new ResourceEntry(kvp.Key, kvp.Value));
            }
        }
    }

    /// <summary>
    /// Get current amount of a resource type.
    /// </summary>
    public int Get(ResourceType type)
    {
        EnsureCacheInitialized();
        return runtimeCache.TryGetValue(type, out int amount) ? amount : 0;
    }

    /// <summary>
    /// Add resources. Always succeeds (no capacity limit in base class).
    /// </summary>
    public void Add(ResourceType type, int amount)
    {
        if (type == ResourceType.None || amount <= 0) return;

        EnsureCacheInitialized();

        int current = Get(type);
        int newAmount = current + amount;
        runtimeCache[type] = newAmount;

        SyncToSerializedList();
        OnResourceChanged?.Invoke(type, newAmount);
    }

    /// <summary>
    /// Try to remove resources.
    /// </summary>
    /// <returns>True if full amount was removed, false if insufficient.</returns>
    public bool TryRemove(ResourceType type, int amount)
    {
        if (type == ResourceType.None || amount <= 0) return true;

        EnsureCacheInitialized();

        int current = Get(type);
        if (current < amount) return false;

        int newAmount = current - amount;
        if (newAmount > 0)
        {
            runtimeCache[type] = newAmount;
        }
        else
        {
            runtimeCache.Remove(type);
        }

        SyncToSerializedList();
        OnResourceChanged?.Invoke(type, newAmount);
        return true;
    }

    /// <summary>
    /// Check if we have at least the specified amount.
    /// </summary>
    public bool Has(ResourceType type, int amount)
    {
        return Get(type) >= amount;
    }

    /// <summary>
    /// Get all resources as read-only enumerable.
    /// </summary>
    public IEnumerable<KeyValuePair<ResourceType, int>> GetAll()
    {
        EnsureCacheInitialized();
        return runtimeCache;
    }

    /// <summary>
    /// Clear all resources.
    /// </summary>
    public void Clear()
    {
        EnsureCacheInitialized();

        var types = new List<ResourceType>(runtimeCache.Keys);
        runtimeCache.Clear();
        entries.Clear();

        foreach (var type in types)
        {
            OnResourceChanged?.Invoke(type, 0);
        }
    }

    /// <summary>
    /// Get total count of all resources.
    /// </summary>
    public int GetTotalCount()
    {
        EnsureCacheInitialized();

        int total = 0;
        foreach (var kvp in runtimeCache)
        {
            total += kvp.Value;
        }
        return total;
    }

    /// <summary>
    /// Get count of distinct resource types held.
    /// </summary>
    public int GetDistinctTypeCount()
    {
        EnsureCacheInitialized();
        return runtimeCache.Count;
    }
}
