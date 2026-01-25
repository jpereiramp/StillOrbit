using System.Collections.Generic;

public interface IInventory
{
    // --- Queries ---
    IReadOnlyList<IItem> Items { get; }
    IItem CurrentItem { get; }

    // --- Selection ---
    void SetCurrentItem(int index);
    void SetCurrentItem(IItem item);

    // --- Modification ---
    bool AddItem(IItem item);
    bool RemoveItem(IItem item);
    bool Contains(IItem item);

    // --- Utility ---
    bool IsFull { get; }
}
