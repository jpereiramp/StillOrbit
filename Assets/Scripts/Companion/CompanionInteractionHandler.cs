using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles player interaction with the companion.
/// Implements IInteractable to integrate with existing interaction system.
/// </summary>
public class CompanionInteractionHandler : MonoBehaviour, IInteractable
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionCoreController controller;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionInventory inventory;

    // Events
    public event Action<int> OnResourcesDeposited;

    // IInteractable implementation
    public string InteractionPrompt
    {
        get
        {
            if (controller?.Data != null)
            {
                return controller.Data.InteractionPrompt;
            }
            return "Deposit Resources";
        }
    }

    private void Awake()
    {
        if (controller == null || inventory == null)
        {
            Debug.LogError("Missing references in CompanionInteractionHandler.");
            return;
        }
    }

    /// <summary>
    /// Check if player can interact with companion.
    /// </summary>
    public bool CanInteract(GameObject interactor)
    {
        // Must be active
        if (controller == null || !controller.IsActive) return false;

        // Must be in a state that allows interaction
        if (controller.CurrentState == CompanionState.MovingToDepot ||
            controller.CurrentState == CompanionState.Depositing)
        {
            return false;
        }

        // Check range
        if (!controller.IsWithinInteractionRange()) return false;

        // Check if player has resources to deposit
        var playerInventory = GetPlayerInventory(interactor);
        if (playerInventory == null || !HasAnyResources(playerInventory)) return false;

        return true;
    }

    /// <summary>
    /// Perform interaction - deposit player resources into companion.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            Debug.LogWarning("[CompanionInteraction] Cannot interact");
            return;
        }

        var playerInventory = GetPlayerInventory(interactor);
        if (playerInventory == null || inventory == null)
        {
            Debug.LogWarning("[CompanionInteraction] Missing inventory references");
            return;
        }

        // Transfer all resources from player to companion
        int transferred = inventory.TransferAllFrom(playerInventory);

        if (transferred > 0)
        {
            OnResourcesDeposited?.Invoke(transferred);
            Debug.Log($"[CompanionInteraction] Player deposited {transferred} resources");

            // Ensure companion is following after deposit
            if (controller.CurrentState != CompanionState.FollowingPlayer)
            {
                controller.RequestStateChange(CompanionState.FollowingPlayer);
            }
        }
        else
        {
            Debug.Log("[CompanionInteraction] No resources to deposit");
        }
    }

    private IResourceHolder GetPlayerInventory(GameObject interactor)
    {
        // Try to get from interactor
        var holder = interactor.GetComponent<IResourceHolder>();
        if (holder != null) return holder;

        // Try PlayerManager
        var playerInventory = PlayerManager.Instance?.ResourceInventory;
        return playerInventory;
    }

    private bool HasAnyResources(IResourceHolder holder)
    {
        // Check all resource types
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;
            if (holder.GetResourceAmount(type) > 0) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    [Button("Simulate Deposit"), BoxGroup("Debug")]
    private void DebugSimulateDeposit()
    {
        if (Application.isPlaying)
        {
            var player = PlayerManager.Instance?.gameObject;
            if (player != null)
            {
                Interact(player);
            }
        }
    }
#endif
}