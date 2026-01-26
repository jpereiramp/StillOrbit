using UnityEngine;

public class DebugPanel : MonoBehaviour
{
    [SerializeField]
    private PlayerManager playerManager;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300), "Debug Panel", GUI.skin.window);
        if (playerManager == null) return;

        // Equipment info
        var equipmentController = playerManager.EquipmentController;
        if (equipmentController != null)
        {
            string equippedName = equipmentController.HasEquippedItem
                ? (equipmentController.EquippedItemData != null ? equipmentController.EquippedItemData.ItemName : equipmentController.EquippedObject.name)
                : "None";
            GUILayout.Label("Equipped Item: " + equippedName);
            GUILayout.Label("Has Equipped Item: " + equipmentController.HasEquippedItem);
        }

        // Aim info
        var aimController = playerManager.AimController;
        if (aimController != null)
        {
            var hitInfo = aimController.CurrentAimHitInfo;
            GUILayout.Label("Hit Object: " + (hitInfo.HasHit ? hitInfo.HitObject.name : "None"));
            GUILayout.Label("Hit Distance: " + hitInfo.Distance.ToString("F2"));
            GUILayout.Label("Hit Collider: " + (hitInfo.HasHit ? hitInfo.HitCollider.name : "None"));
        }

        // Interaction info
        var interactionController = playerManager.InteractionController;
        if (interactionController != null)
        {
            string prompt = interactionController.GetCurrentInteractionPrompt();
            GUILayout.Label("Interaction: " + (prompt ?? "None"));
        }

        // Inventory info
        var inventory = playerManager.Inventory;
        if (inventory != null)
        {
            int usedSlots = 0;
            foreach (var slot in inventory.Slots)
            {
                if (!slot.IsEmpty) usedSlots++;
            }
            GUILayout.Label($"Inventory: {usedSlots}/{inventory.SlotCount} slots used");
        }

        // Resource inventory info
        var resourceInventory = playerManager.ResourceInventory;
        if (resourceInventory != null)
        {
            GUILayout.Label("Resources:");
            foreach (var resourceType in System.Enum.GetValues(typeof(ResourceType)))
            {
                if ((ResourceType)resourceType == ResourceType.None) continue;
                int amount = resourceInventory.GetResourceAmount((ResourceType)resourceType);
                if (amount > 0)
                    GUILayout.Label($"- {resourceType}: {amount}");
            }
        }

        GUILayout.EndArea();
    }
}