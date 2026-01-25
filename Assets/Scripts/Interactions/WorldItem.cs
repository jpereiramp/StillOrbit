using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Component for items that exist in the world and can be picked up.
/// Attach to any item prefab that should be interactable in the world.
/// Replaces the old PickableObject and PickableItem classes.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IPickable
{
    [Required("Item Data is required for WorldItem to function")]
    [SerializeField]
    private ItemData itemData;

    [Tooltip("Number of items in this stack (for stackable items)")]
    [Min(1)]
    [SerializeField]
    private int quantity = 1;

    [FoldoutGroup("Debug")]
    [SerializeField, ReadOnly]
    private bool hasBeenPickedUp;

    private Rigidbody rb;
    private Collider[] colliders;

    public ItemData ItemData => itemData;
    public int Quantity => quantity;

    // IInteractable implementation
    public string InteractionPrompt => itemData != null ? $"Pick up {itemData.ItemName}" : "Pick up";

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponents<Collider>();
    }

    public bool CanInteract(GameObject interactor)
    {
        bool canInteract = itemData != null && itemData.CanPickup && !hasBeenPickedUp;

        if (!canInteract)
        {
            Debug.Log($"[WorldItem] CanInteract=false: itemData={itemData != null}, CanPickup={itemData?.CanPickup}, hasBeenPickedUp={hasBeenPickedUp}");
        }

        return canInteract;
    }

    public void Interact(GameObject interactor)
    {
        // Default interaction for pickable is to pick it up
        // The interaction controller will handle this through IPickable
    }

    public ItemData PickUp()
    {
        if (hasBeenPickedUp || itemData == null)
            return null;

        hasBeenPickedUp = true;

        // Disable physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Disable colliders so it doesn't block raycasts anymore
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Destroy world object
        Destroy(gameObject);

        return itemData;
    }

    public GameObject GetEquippableObject()
    {
        if (itemData == null || !itemData.CanEquip)
            return null;

        // If there's a separate held prefab, instantiate it
        if (itemData.HeldPrefab != null && itemData.HeldPrefab != itemData.WorldPrefab)
        {
            return Instantiate(itemData.HeldPrefab);
        }

        // Otherwise, we could reuse this object, but since we're destroying it
        // in PickUp(), the caller should instantiate from HeldPrefab
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (itemData != null && itemData.IsStackable)
        {
            quantity = Mathf.Clamp(quantity, 1, itemData.MaxStackSize);
        }
        else
        {
            quantity = 1;
        }
    }
#endif
}
