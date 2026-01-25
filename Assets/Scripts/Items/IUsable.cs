using UnityEngine;

/// <summary>
/// Interface for items that can be used when equipped/held.
/// Use is triggered by PrimaryAction input.
/// </summary>
public interface IUsable
{
    /// <summary>
    /// Whether this item can currently be used.
    /// </summary>
    bool CanUse { get; }

    /// <summary>
    /// Use the item. Only called when item is equipped and player presses PrimaryAction.
    /// </summary>
    /// <param name="user">The GameObject using this item (typically the player)</param>
    /// <returns>Result indicating what happened (for equipment controller to handle)</returns>
    UseResult Use(GameObject user);
}

/// <summary>
/// Result of using an item, tells the equipment system how to respond.
/// </summary>
public enum UseResult
{
    /// <summary>Item was used successfully, remains equipped</summary>
    Success,
    /// <summary>Item was consumed/destroyed, should be unequipped</summary>
    Consumed,
    /// <summary>Item could not be used (cooldown, no ammo, etc.)</summary>
    Failed
}