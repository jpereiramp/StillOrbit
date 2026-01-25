using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles player interactions with world objects.
/// Processes Interact input for IInteractable/IPickable objects.
/// Processes PrimaryAction input for using equipped items.
/// </summary>
public class PlayerInteractionController : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    private PlayerInputHandler inputHandler;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerAimController aimController;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerEquipmentController equipmentController;

    [BoxGroup("References")]
    [SerializeField]
    private PlayerInventory inventory;

    [BoxGroup("Settings")]
    [SerializeField]
    private float interactionRange = 3f;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private IInteractable currentTarget;

    // Edge detection for inputs (prevent held button = repeated actions)
    private bool previousInteractInput;
    private bool previousPrimaryActionInput;

    private void Awake()
    {
        // Auto-find components if not assigned
        if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        if (aimController == null) aimController = GetComponent<PlayerAimController>();
        if (equipmentController == null) equipmentController = GetComponent<PlayerEquipmentController>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        UpdateCurrentTarget();

        // Interact input - rising edge only (pressed this frame)
        bool interactPressed = inputHandler.InteractInput && !previousInteractInput;
        if (interactPressed)
        {
            HandleInteraction();
        }

        // Primary action input - rising edge only
        bool primaryActionPressed = inputHandler.PrimaryActionInput && !previousPrimaryActionInput;
        if (primaryActionPressed)
        {
            HandlePrimaryAction();
        }

        // Update previous states for edge detection
        previousInteractInput = inputHandler.InteractInput;
        previousPrimaryActionInput = inputHandler.PrimaryActionInput;
    }

    private void UpdateCurrentTarget()
    {
        var hitInfo = aimController.CurrentAimHitInfo;

        // Check if we're looking at something in range
        if (!hitInfo.HasHit || hitInfo.Distance > interactionRange)
        {
            currentTarget = null;
            return;
        }

        currentTarget = FindInteractableInObject(hitInfo.HitObject);
    }

    private void HandleInteraction()
    {
        if (currentTarget == null)
        {
            Debug.Log("[Interaction] No current target");
            return;
        }

        Debug.Log($"[Interaction] Target: {(currentTarget as MonoBehaviour)?.gameObject.name}, Type: {currentTarget.GetType().Name}");

        if (!currentTarget.CanInteract(gameObject))
        {
            Debug.Log("[Interaction] Target cannot be interacted with (CanInteract returned false)");
            return;
        }

        // Check if it's a pickable item
        if (currentTarget is IPickable pickable)
        {
            Debug.Log("[Interaction] Target is IPickable, handling pickup...");
            HandlePickup(pickable);
            return;
        }

        // Generic interaction (doors, levers, etc.)
        Debug.Log("[Interaction] Generic interaction");
        currentTarget.Interact(gameObject);
    }

    private void HandlePickup(IPickable pickable)
    {
        var itemData = pickable.ItemData;
        if (itemData == null)
        {
            Debug.LogWarning("[Pickup] FAILED: ItemData is null on the pickable object!");
            return;
        }

        Debug.Log($"[Pickup] Attempting to pick up: {itemData.ItemName}");

        // Can we pick it up?
        if (!itemData.CanPickup)
        {
            Debug.LogWarning($"[Pickup] FAILED: {itemData.ItemName} has CanPickup = false");
            return;
        }

        // Check if we have inventory space
        if (inventory == null)
        {
            Debug.LogWarning("[Pickup] WARNING: No PlayerInventory component found on player!");
        }
        else if (!inventory.HasSpace(itemData))
        {
            Debug.Log($"[Pickup] FAILED: Inventory full, cannot pick up {itemData.ItemName}");
            return;
        }

        // Pick up the item (removes from world)
        var pickedItemData = pickable.PickUp();
        if (pickedItemData == null)
        {
            Debug.LogWarning("[Pickup] FAILED: PickUp() returned null");
            return;
        }

        Debug.Log($"[Pickup] Successfully picked up: {pickedItemData.ItemName}");

        // Decide: equip it or just add to inventory?
        bool shouldEquip = ShouldEquipOnPickup(pickedItemData);
        Debug.Log($"[Pickup] ShouldEquip: {shouldEquip}, CanEquip: {pickedItemData.CanEquip}, HasHeldPrefab: {pickedItemData.HeldPrefab != null}");

        if (shouldEquip && pickedItemData.CanEquip)
        {
            // If we're already holding something, put it in inventory first
            if (equipmentController.HasEquippedItem && inventory != null)
            {
                var currentlyEquipped = equipmentController.EquippedItemData;
                if (currentlyEquipped != null)
                {
                    inventory.TryAddItem(currentlyEquipped);
                }
            }

            // Equip the new item
            bool equipped = equipmentController.EquipItem(pickedItemData);
            Debug.Log($"[Pickup] Equip result: {equipped}");

            // Also add to inventory (we're holding it, but it's "in" our inventory)
            if (inventory != null)
            {
                inventory.TryAddItem(pickedItemData);
            }
        }
        else
        {
            // Just add to inventory
            if (inventory != null)
            {
                inventory.TryAddItem(pickedItemData);
                Debug.Log($"[Pickup] Added to inventory only");
            }
        }
    }

    /// <summary>
    /// Determines if an item should be auto-equipped when picked up.
    /// Override this logic as needed for your game design.
    /// </summary>
    private bool ShouldEquipOnPickup(ItemData itemData)
    {
        // If hands are empty and item can be equipped, auto-equip it
        return !equipmentController.HasEquippedItem && itemData.CanEquip;
    }

    private void HandlePrimaryAction()
    {
        // Use the equipped item
        if (!equipmentController.HasEquippedItem)
            return;

        var result = equipmentController.TryUseEquippedItem(gameObject);

        // If item was consumed, remove it from inventory too
        if (result == UseResult.Consumed && inventory != null)
        {
            var consumedItem = equipmentController.EquippedItemData;
            if (consumedItem != null)
            {
                inventory.TryRemoveItem(consumedItem);
            }
        }
    }

    /// <summary>
    /// Gets the current interaction target (for UI prompts).
    /// </summary>
    public IInteractable GetCurrentTarget()
    {
        return currentTarget;
    }

    /// <summary>
    /// Gets the interaction prompt for the current target.
    /// </summary>
    public string GetCurrentInteractionPrompt()
    {
        if (currentTarget == null)
            return null;

        if (!currentTarget.CanInteract(gameObject))
            return null;

        return currentTarget.InteractionPrompt;
    }

    private IInteractable FindInteractableInObject(GameObject obj)
    {
        IInteractable interactable = null;

        // Check self
        if (obj != null)
        {
            interactable = obj.GetComponent<IInteractable>();
        }

        // Check parent
        if (interactable == null && obj.transform.parent != null)
        {
            interactable = obj.transform.parent.GetComponent<IInteractable>();
        }

        // Check root
        if (interactable == null && obj.transform.root != null)
        {
            interactable = obj.transform.root.GetComponent<IInteractable>();
        }

        // Check children recursively
        if (interactable == null)
        {
            foreach (Transform child in obj.transform)
            {
                interactable = FindInteractableInObject(child.gameObject);
                if (interactable != null)
                    break;
            }
        }

        return interactable;
    }
}