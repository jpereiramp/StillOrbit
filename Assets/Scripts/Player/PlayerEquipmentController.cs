using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages the currently equipped/held item in the player's hands.
/// Handles equipping, unequipping, and using items.
/// </summary>
public class PlayerEquipmentController : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField]
    private Transform itemHoldPoint;

    [BoxGroup("Settings")]
    [Tooltip("Offset applied to held items (adjust per-item via HeldItemBehaviour if needed)")]
    [SerializeField]
    private Vector3 defaultHoldOffset = Vector3.zero;

    [BoxGroup("Settings")]
    [SerializeField]
    private Vector3 defaultHoldRotation = Vector3.zero;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private GameObject equippedObject;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private ItemData equippedItemData;

    private IUsable currentUsable;

    /// <summary>
    /// The currently equipped GameObject (the visual in hand).
    /// </summary>
    public GameObject EquippedObject => equippedObject;

    /// <summary>
    /// The ItemData of the currently equipped item.
    /// </summary>
    public ItemData EquippedItemData => equippedItemData;

    /// <summary>
    /// Whether an item is currently equipped.
    /// </summary>
    public bool HasEquippedItem => equippedObject != null;

    /// <summary>
    /// Equips an item from ItemData, instantiating its held prefab.
    /// </summary>
    /// <returns>True if item was equipped successfully</returns>
    public bool EquipItem(ItemData itemData)
    {
        Debug.Log($"[Equipment] EquipItem called with: {itemData?.ItemName ?? "NULL"}");

        if (itemData == null || !itemData.CanEquip)
        {
            Debug.LogWarning($"[Equipment] FAILED: itemData null or CanEquip=false");
            return false;
        }

        if (itemData.HeldPrefab == null)
        {
            Debug.LogWarning($"[Equipment] FAILED: Cannot equip {itemData.ItemName}: no HeldPrefab assigned in ItemData!");
            return false;
        }

        if (itemHoldPoint == null)
        {
            Debug.LogError($"[Equipment] FAILED: itemHoldPoint is not assigned on PlayerEquipmentController!");
            return false;
        }

        // Unequip current item first
        if (HasEquippedItem)
        {
            UnequipItem(destroy: true);
        }

        // Instantiate held prefab
        Debug.Log($"[Equipment] Instantiating HeldPrefab: {itemData.HeldPrefab.name}");
        equippedObject = Instantiate(itemData.HeldPrefab, itemHoldPoint);
        equippedItemData = itemData;

        // Apply positioning
        SetupHeldItem(equippedObject);

        // Cache IUsable if present
        currentUsable = equippedObject.GetComponent<IUsable>();
        Debug.Log($"[Equipment] SUCCESS: Equipped {itemData.ItemName}, IUsable: {currentUsable != null}");

        return true;
    }

    /// <summary>
    /// Equips an existing GameObject directly (for backwards compatibility or special cases).
    /// </summary>
    public bool EquipObject(GameObject item, ItemData itemData = null)
    {
        if (item == null)
            return false;

        if (HasEquippedItem)
        {
            UnequipItem(destroy: true);
        }

        equippedObject = item;
        equippedItemData = itemData;
        item.transform.SetParent(itemHoldPoint);

        SetupHeldItem(item);

        currentUsable = item.GetComponent<IUsable>();

        return true;
    }

    /// <summary>
    /// Unequips the current item.
    /// </summary>
    /// <param name="destroy">If true, destroys the held object. If false, detaches it.</param>
    /// <returns>The unequipped GameObject if not destroyed, null otherwise</returns>
    public GameObject UnequipItem(bool destroy = true)
    {
        if (!HasEquippedItem)
            return null;

        var item = equippedObject;
        equippedObject = null;
        equippedItemData = null;
        currentUsable = null;

        if (destroy)
        {
            Destroy(item);
            return null;
        }
        else
        {
            item.transform.SetParent(null);
            return item;
        }
    }

    /// <summary>
    /// Attempts to use the currently equipped item.
    /// Should be called when PrimaryAction input is triggered.
    /// </summary>
    /// <param name="user">The GameObject using the item (typically the player root)</param>
    /// <returns>The result of the use attempt</returns>
    public UseResult TryUseEquippedItem(GameObject user)
    {
        if (currentUsable == null)
            return UseResult.Failed;

        if (!currentUsable.CanUse)
            return UseResult.Failed;

        var result = currentUsable.Use(user);

        // Handle consumed items
        if (result == UseResult.Consumed)
        {
            UnequipItem(destroy: true);
        }

        return result;
    }

    /// <summary>
    /// Drops the currently equipped item into the world.
    /// Spawns the WorldPrefab from ItemData and applies physics.
    /// </summary>
    /// <returns>The dropped ItemData, or null if nothing was dropped</returns>
    public ItemData DropItem()
    {
        if (!HasEquippedItem)
            return null;

        // Save item data before unequipping
        var droppedItemData = equippedItemData;

        if (droppedItemData == null)
        {
            Debug.LogWarning("[Equipment] Cannot drop: no ItemData associated with equipped item");
            UnequipItem(destroy: true);
            return null;
        }

        if (droppedItemData.WorldPrefab == null)
        {
            Debug.LogWarning($"[Equipment] Cannot drop {droppedItemData.ItemName}: no WorldPrefab assigned");
            UnequipItem(destroy: true);
            return null;
        }

        // Calculate drop position (in front of player, slightly up)
        Vector3 dropPosition = transform.position + transform.forward * 1f + Vector3.up * 1f;

        // Destroy held item
        UnequipItem(destroy: true);

        // Spawn world prefab
        var worldItem = Instantiate(droppedItemData.WorldPrefab, dropPosition, Quaternion.identity);
        Debug.Log($"[Equipment] Dropped {droppedItemData.ItemName} at {dropPosition}");

        // Apply drop physics
        var rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Apply forward and upward force
            Vector3 dropForce = transform.forward * 2f + Vector3.up * 1f;
            rb.AddForce(dropForce, ForceMode.VelocityChange);
        }

        return droppedItemData;
    }

    private void SetupHeldItem(GameObject item)
    {
        // Check for custom hold behaviour
        var holdBehaviour = item.GetComponent<HeldItemBehaviour>();
        if (holdBehaviour != null)
        {
            item.transform.localPosition = holdBehaviour.HoldOffset;
            item.transform.localRotation = Quaternion.Euler(holdBehaviour.HoldRotation);
        }
        else
        {
            item.transform.localPosition = defaultHoldOffset;
            item.transform.localRotation = Quaternion.Euler(defaultHoldRotation);
        }

        item.transform.localScale = Vector3.one;
    }
}