using System;

/// <summary>
/// Represents a single slot in the inventory, holding item data and quantity.
/// </summary>
[Serializable]
public class InventorySlot
{
    public ItemData ItemData;
    public int Quantity;

    public InventorySlot()
    {
        ItemData = null;
        Quantity = 0;
    }

    public InventorySlot(ItemData itemData, int quantity = 1)
    {
        ItemData = itemData;
        Quantity = quantity;
    }

    public bool IsEmpty => ItemData == null || Quantity <= 0;

    public bool CanAddToStack(ItemData item, int amount = 1)
    {
        if (IsEmpty) return true;
        if (ItemData != item) return false;
        if (!ItemData.IsStackable) return false;
        return Quantity + amount <= ItemData.MaxStackSize;
    }

    public int GetAvailableStackSpace()
    {
        if (IsEmpty) return int.MaxValue;
        if (!ItemData.IsStackable) return 0;
        return ItemData.MaxStackSize - Quantity;
    }

    public void Clear()
    {
        ItemData = null;
        Quantity = 0;
    }
}
