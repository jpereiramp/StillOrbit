using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Simple inventory system for the player.
/// Stores items as data references, not GameObjects.
/// No UI - data and logic only.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [BoxGroup("Settings")]
    [Min(1)]
    [SerializeField]
    private int slotCount = 20;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private List<InventorySlot> slots = new List<InventorySlot>();

    /// <summary>
    /// Fired when inventory contents change. Passes the slot index that changed.
    /// </summary>
    public event Action<int> OnInventoryChanged;

    /// <summary>
    /// Total number of slots in inventory.
    /// </summary>
    public int SlotCount => slotCount;

    /// <summary>
    /// Read-only access to slots for UI or queries.
    /// </summary>
    public IReadOnlyList<InventorySlot> Slots => slots;

    private void Awake()
    {
        InitializeSlots();
    }

    private void InitializeSlots()
    {
        slots.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(new InventorySlot());
        }
    }

    /// <summary>
    /// Attempts to add an item to inventory.
    /// </summary>
    /// <returns>True if item was added, false if inventory is full</returns>
    public bool TryAddItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
            return false;

        int remaining = quantity;

        // First, try to stack with existing items
        if (item.IsStackable)
        {
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].ItemData == item && slots[i].CanAddToStack(item, 1))
                {
                    int spaceAvailable = slots[i].GetAvailableStackSpace();
                    int toAdd = Mathf.Min(remaining, spaceAvailable);
                    slots[i].Quantity += toAdd;
                    remaining -= toAdd;
                    OnInventoryChanged?.Invoke(i);
                }
            }
        }

        // Then fill empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty)
            {
                int toAdd = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;
                slots[i].ItemData = item;
                slots[i].Quantity = toAdd;
                remaining -= toAdd;
                OnInventoryChanged?.Invoke(i);
            }
        }

        return remaining == 0;
    }

    /// <summary>
    /// Removes a quantity of an item from inventory.
    /// </summary>
    /// <returns>True if the full quantity was removed</returns>
    public bool TryRemoveItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
            return false;

        int toRemove = quantity;

        // Count how many we have first
        int totalHave = GetItemCount(item);
        if (totalHave < quantity)
            return false;

        // Remove from slots (prefer partial stacks first)
        for (int i = slots.Count - 1; i >= 0 && toRemove > 0; i--)
        {
            if (slots[i].ItemData == item)
            {
                int removeFromSlot = Mathf.Min(toRemove, slots[i].Quantity);
                slots[i].Quantity -= removeFromSlot;
                toRemove -= removeFromSlot;

                if (slots[i].Quantity <= 0)
                {
                    slots[i].Clear();
                }

                OnInventoryChanged?.Invoke(i);
            }
        }

        return true;
    }

    /// <summary>
    /// Gets total count of a specific item across all slots.
    /// </summary>
    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        int count = 0;
        foreach (var slot in slots)
        {
            if (slot.ItemData == item)
            {
                count += slot.Quantity;
            }
        }
        return count;
    }

    /// <summary>
    /// Checks if inventory contains at least the specified quantity of an item.
    /// </summary>
    public bool HasItem(ItemData item, int quantity = 1)
    {
        return GetItemCount(item) >= quantity;
    }

    /// <summary>
    /// Gets the item at a specific slot index.
    /// </summary>
    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return null;
        return slots[index];
    }

    /// <summary>
    /// Finds the first slot containing the specified item.
    /// </summary>
    /// <returns>Slot index, or -1 if not found</returns>
    public int FindItem(ItemData item)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].ItemData == item)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Checks if inventory has space for an item.
    /// </summary>
    public bool HasSpace(ItemData item, int quantity = 1)
    {
        if (item == null) return false;

        int canFit = 0;

        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                canFit += item.IsStackable ? item.MaxStackSize : 1;
            }
            else if (slot.ItemData == item && item.IsStackable)
            {
                canFit += slot.GetAvailableStackSpace();
            }

            if (canFit >= quantity)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all unique items in inventory.
    /// </summary>
    public List<ItemData> GetAllUniqueItems()
    {
        var items = new List<ItemData>();
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty && !items.Contains(slot.ItemData))
            {
                items.Add(slot.ItemData);
            }
        }
        return items;
    }

#if UNITY_EDITOR
    [Button("Clear Inventory"), BoxGroup("Debug")]
    private void DebugClearInventory()
    {
        foreach (var slot in slots)
        {
            slot.Clear();
        }
        Debug.Log("Inventory cleared");
    }
#endif
}
