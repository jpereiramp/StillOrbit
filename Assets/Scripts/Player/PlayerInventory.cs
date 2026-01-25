using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour, IInventory
{
    [SerializeField] private int capacity = 9;

    private readonly List<IItem> items = new();
    private int currentIndex = -1;

    // --- Queries ---
    public IReadOnlyList<IItem> Items => items;

    public IItem CurrentItem
    {
        get
        {
            if (currentIndex < 0 || currentIndex >= items.Count)
                return null;

            return items[currentIndex];
        }
    }

    // --- Selection ---
    public void SetCurrentItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return;

        currentIndex = index;
    }

    public void SetCurrentItem(IItem item)
    {
        int index = items.IndexOf(item);
        if (index >= 0)
        {
            currentIndex = index;
        }
    }

    // --- Modification ---
    public bool AddItem(IItem item)
    {
        if (IsFull || item == null)
            return false;

        items.Add(item);

        // Auto-select if nothing selected
        if (currentIndex == -1)
            currentIndex = 0;

        return true;
    }

    public bool RemoveItem(IItem item)
    {
        int index = items.IndexOf(item);
        if (index < 0)
            return false;

        items.RemoveAt(index);

        // Adjust current index
        if (items.Count == 0)
        {
            currentIndex = -1;
        }
        else if (currentIndex >= items.Count)
        {
            currentIndex = items.Count - 1;
        }

        return true;
    }

    public bool Contains(IItem item)
    {
        return items.Contains(item);
    }

    // --- Utility ---
    public bool IsFull => items.Count >= capacity;
}
