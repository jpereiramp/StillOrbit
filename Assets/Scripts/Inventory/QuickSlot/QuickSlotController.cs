using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class QuickSlotController : MonoBehaviour
{
    public const int QuickSlotCount = 5;
    public const int EmptySlotIndex = -1;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerInventory playerInventory;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerEquipmentController equipmentController;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerInputHandler inputHandler;

    // Each element is the inventory slot index, or -1 if empty
    private int[] quickSlots = new int[QuickSlotCount];

    // Currently active quick slot index
    private int activeQuickSlotIndex = -1;

    /// <summary>
    /// Fired when a quick slot is changed (e.g. new item is assigned).
    /// Parameters: (slotIndex, itemData or null)
    /// </summary>
    public event Action<int, ItemData> OnQuickSlotChanged;

    // <summary>
    /// Fired when the active quick slot changes.
    /// Parameters: (previousIndex, newIndex)
    /// </summary>
    public event Action<int, int> OnActiveSlotChanged;

    // Public Accessors
    public int ActiveQuickSlotIndex => activeQuickSlotIndex;

    #region Lifecycle
    private void Awake()
    {
        // Initialize all slots to empty
        for (int i = 0; i < QuickSlotCount; i++)
        {
            quickSlots[i] = EmptySlotIndex;
        }
    }

    private void Update()
    {
        if (inputHandler.QuickSlotPressed != EmptySlotIndex)
        {
            SelectQuickSlot(inputHandler.QuickSlotPressed);
            inputHandler.QuickSlotPressed = EmptySlotIndex;
        }
    }

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged += OnInventorySlotChanged;
        }
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= OnInventorySlotChanged;
        }
    }
    #endregion

    /// <summary>
    /// Get the item data assigned to the given quick slot index.
    /// </summary>
    /// <param name="quickSlotIndex">Index of the quick slot</param>
    /// <returns>ItemData assigned to the quick slot, or null if empty</returns>
    public ItemData GetQuickSlotItem(int quickSlotIndex)
    {
        if (!IsValidQuickSlotIndex(quickSlotIndex))
        {
            return null;
        }

        int inventoryIndex = quickSlots[quickSlotIndex];
        if (inventoryIndex == EmptySlotIndex)
        {
            return null;
        }

        InventorySlot slot = playerInventory.GetSlot(inventoryIndex);
        return slot?.ItemData;
    }

    /// <summary>
    /// Get the inventory slot index assigned to the given quick slot index.
    /// </summary>
    /// <param name="quickSlotIndex">Index of the quick slot</param>
    /// <returns>Inventory slot index assigned to the quick slot, or -1 if empty</returns>
    public int GetInventorySlotIndex(int quickSlotIndex)
    {
        if (!IsValidQuickSlotIndex(quickSlotIndex))
        {
            return EmptySlotIndex;
        }

        return quickSlots[quickSlotIndex];
    }

    public bool AssignToQuickSlot(int quickSlotIndex, int inventorySlotIndex)
    {
        if (!IsValidQuickSlotIndex(quickSlotIndex))
        {
            return false; // Invalid quick slot
        }

        // Validate inventory slot
        if (inventorySlotIndex > EmptySlotIndex)
        {
            InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);
            if (slot == null || slot.IsEmpty || !slot.ItemData.CanEquip)
            {
                return false; // Invalid inventory slot or unequippable item
            }

            // Remove from any other quick slots
            RemoveInventoryItemFromAllQuickSlots(inventorySlotIndex);
        }

        // Set assignment
        quickSlots[quickSlotIndex] = inventorySlotIndex;

        // Notify listeners
        ItemData item = inventorySlotIndex != EmptySlotIndex ? playerInventory.GetSlot(inventorySlotIndex).ItemData : null;
        OnQuickSlotChanged?.Invoke(quickSlotIndex, item);

        return true;
    }

    /// <summary>
    /// Clears the given quick slot.
    /// </summary>
    /// <param name="quickSlotIndex">Index of the quick slot to clear</param>
    public void ClearQuickSlot(int quickSlotIndex)
    {
        AssignToQuickSlot(quickSlotIndex, EmptySlotIndex);
    }

    /// <summary>
    /// Selects the given quick slot as active.
    /// </summary>
    /// <param name="quickSlotIndex">Index of the quick slot to select</param>
    public void SelectQuickSlot(int quickSlotIndex)
    {
        Debug.Log($"[QuickSlot] SelectQuickSlot called with index: {quickSlotIndex}");

        if (!IsValidQuickSlotIndex(quickSlotIndex))
        {
            Debug.LogWarning($"[QuickSlot] Invalid quick slot index: {quickSlotIndex}");
            return; // Invalid index
        }

        int previousIndex = activeQuickSlotIndex;

        // If selecting same slot, ignore
        if (activeQuickSlotIndex == quickSlotIndex)
        {
            Debug.Log($"[QuickSlot] Same slot selected, ignoring");
            return;
        }

        // Select new slot
        activeQuickSlotIndex = quickSlotIndex;
        Debug.Log($"[QuickSlot] Active slot changed from {previousIndex} to {activeQuickSlotIndex}");

        // Equip item in this slot
        int inventoryIndex = quickSlots[quickSlotIndex];
        Debug.Log($"[QuickSlot] Inventory slot index for quick slot {quickSlotIndex}: {inventoryIndex}");

        ItemData itemData = GetQuickSlotItem(quickSlotIndex);
        Debug.Log($"[QuickSlot] ItemData for quick slot {quickSlotIndex}: {(itemData != null ? itemData.ItemName : "NULL")}");

        if (itemData != null && itemData.CanEquip)
        {
            Debug.Log($"[QuickSlot] Calling EquipItem for: {itemData.ItemName}");
            equipmentController.EquipItem(itemData);
        }
        else // Empty slot selected -- unequip current item
        {
            Debug.Log($"[QuickSlot] Empty slot or non-equippable, unequipping current item");
            equipmentController.UnequipItem();
        }

        OnActiveSlotChanged?.Invoke(previousIndex, activeQuickSlotIndex);
    }

    /// <summary>
    /// Find which quick slot, if any, contains the given inventory slot.
    /// </summary>
    /// <param name="inventorySlotIndex">Inventory index to find in quick slots</param>
    /// <returns>Quick slot index containing the inventory slot, or -1 if not found</returns>
    public int FindQuickSlotForInventorySlot(int inventorySlotIndex)
    {
        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (quickSlots[i] == inventorySlotIndex)
            {
                return i;
            }
        }

        return EmptySlotIndex;
    }

    /// <summary>
    /// Automatically assigns a newly picked up equippable item to the first available quick slot, if any.
    /// </summary>
    /// <param name="inventorySlotIndex">Inventory index of the item to assign</param>
    /// <returns>True if assignment was successful, false otherwise</returns>
    public bool TryAutoAssign(int inventorySlotIndex)
    {
        InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);

        // Validate slot and equip ability
        if (slot == null || slot.IsEmpty || !slot.ItemData.CanEquip)
        {
            return false; // Invalid slot
        }

        // Find first empty quick slot
        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (quickSlots[i] == EmptySlotIndex)
            {
                return AssignToQuickSlot(i, inventorySlotIndex);
            }
        }

        return false; // No empty slots
    }

    private void RemoveInventoryItemFromAllQuickSlots(int inventorySlotIndex)
    {
        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (quickSlots[i] == inventorySlotIndex)
            {
                quickSlots[i] = EmptySlotIndex;
                OnQuickSlotChanged?.Invoke(i, null);
            }
        }
    }

    private void OnInventorySlotChanged(int inventorySlotIndex)
    {
        // Check if inventory slot is assigned to any quick slot
        int quickSlot = FindQuickSlotForInventorySlot(inventorySlotIndex);
        if (quickSlot == EmptySlotIndex) return;

        InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);

        // If slot is now empty, clear the quick slot assignment
        if (slot == null || slot.IsEmpty)
        {
            quickSlots[quickSlot] = EmptySlotIndex;
            OnQuickSlotChanged?.Invoke(quickSlot, null);

            // If this was the active quick slot, deactivate it
            if (activeQuickSlotIndex == quickSlot)
            {
                int previousIndex = activeQuickSlotIndex;
                activeQuickSlotIndex = EmptySlotIndex;
                OnActiveSlotChanged?.Invoke(previousIndex, activeQuickSlotIndex);
            }
        }
        else
        {
            // Item still exists, just notify of potential change
            OnQuickSlotChanged?.Invoke(quickSlot, slot.ItemData);
        }
    }

    private bool IsValidQuickSlotIndex(int index)
    {
        return index > EmptySlotIndex && index < QuickSlotCount;
    }

    /// <summary>
    /// Sets the active quick slot index without equipping the item.
    /// Use when the item is already equipped via another mechanism (e.g., pickup auto-equip).
    /// </summary>
    public void SetActiveSlotIndex(int quickSlotIndex)
    {
        if (!IsValidQuickSlotIndex(quickSlotIndex)) return;
        if (activeQuickSlotIndex == quickSlotIndex) return;

        int previousIndex = activeQuickSlotIndex;
        activeQuickSlotIndex = quickSlotIndex;
        OnActiveSlotChanged?.Invoke(previousIndex, activeQuickSlotIndex);
    }
}