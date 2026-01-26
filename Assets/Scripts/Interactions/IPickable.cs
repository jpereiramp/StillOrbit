using UnityEngine;

/// <summary>
/// Interface for world objects that can be picked up.
/// Implements IInteractable so pickable items are also interactable.
/// </summary>
public interface IPickable : IInteractable
{
    /// <summary>
    /// The item data associated with this pickable.
    /// </summary>
    ItemData ItemData { get; }

    /// <summary>
    /// The quantity of items in this stack.
    /// </summary>
    int Quantity { get; }

    /// <summary>
    /// Picks up the item, removing it from the world.
    /// Returns the item data for inventory, or null if pickup failed.
    /// </summary>
    ItemData PickUp();

    /// <summary>
    /// Gets the GameObject to equip in hand (may be different from world object).
    /// Returns null if item should only go to inventory.
    /// </summary>
    GameObject GetEquippableObject();
}