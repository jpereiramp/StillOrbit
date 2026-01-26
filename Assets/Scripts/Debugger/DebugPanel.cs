using UnityEngine;
using UnityEngine.InputSystem;

public enum DebugPanelTab
{
    PlayerStatus,
    BuildingStatus,
    CompanionStatus,
}

public class DebugPanel : MonoBehaviour
{
    [SerializeField]
    private PlayerManager playerManager;

    [SerializeField]
    private BuildModeController buildModeController;

    [Header("Companion")]
    [SerializeField]
    private CompanionCoreController companionController;

    private int tabCount = DebugPanelTab.GetNames(typeof(DebugPanelTab)).Length;
    private int currentTab = 0;
    private Vector2 companionInventoryScrollPos;

    private void Update()
    {
        if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
            currentTab = (currentTab - 1 + tabCount) % tabCount;
        if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
            currentTab = (currentTab + 1 + tabCount) % tabCount;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300), "Debug Panel (" + ((DebugPanelTab)currentTab).ToString() + ")", GUI.skin.window);

        switch ((DebugPanelTab)currentTab)
        {
            case DebugPanelTab.PlayerStatus:
                RenderPlayerStatusTab();
                break;
            case DebugPanelTab.BuildingStatus:
                RenderBuildingStatusTab();
                break;
            case DebugPanelTab.CompanionStatus:
                RenderCompanionStatusTab();
                break;
        }

        GUILayout.EndArea();
    }

    private void RenderPlayerStatusTab()
    {
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
    }

    private void RenderBuildingStatusTab()
    {
        if (buildModeController == null) return;

        GUILayout.Label("Selected Building: " + buildModeController.SelectedBuilding?.BuildingName ?? "None");
        GUILayout.Label("Build Mode State: " + buildModeController.CurrentState.ToString());
    }

    private void RenderCompanionStatusTab()
    {
        if (companionController == null)
        {
            GUILayout.Label("(No companion reference)");
            return;
        }

        // State info
        GUILayout.Label($"State: {companionController.CurrentState}");
        GUILayout.Label($"Active: {(companionController.IsActive ? "Yes" : "No")}");

        float dist = companionController.GetDistanceToPlayer();
        GUILayout.Label($"Distance: {(dist < float.MaxValue ? $"{dist:F1}m" : "N/A")}");

        bool inRange = companionController.IsWithinInteractionRange();
        GUILayout.Label($"In Range: {(inRange ? "Yes" : "No")}");

        GUILayout.Space(5);

        // Inventory info 
        CompanionInventory companionInventory = companionController.Inventory;
        if (companionInventory != null)
        {
            int total = companionInventory.GetTotalResourceCount();
            GUILayout.Label($"Inventory: {total} total");

            if (total > 0)
            {
                companionInventoryScrollPos = GUILayout.BeginScrollView(companionInventoryScrollPos, GUILayout.Height(60));
                foreach (var kvp in companionInventory.GetAllResources())
                {
                    GUILayout.Label($"  {kvp.Key}: {kvp.Value}");
                }
                GUILayout.EndScrollView();
            }
        }

        GUILayout.Space(5);

        // Auto-deposit info
        CompanionAutoDeposit companionAutoDeposit = companionController.AutoDepositController;
        if (companionAutoDeposit != null)
        {
            var idleTimerField = typeof(CompanionAutoDeposit).GetField("idleTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var triggeredField = typeof(CompanionAutoDeposit).GetField("isAutoDepositTriggered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            float idleTimer = idleTimerField != null ? (float)idleTimerField.GetValue(companionAutoDeposit) : 0f;
            bool isTriggered = triggeredField != null && (bool)triggeredField.GetValue(companionAutoDeposit);

            float threshold = companionController.Data?.IdleTimeBeforeAutoDeposit ?? 5f;
            GUILayout.Label($"Auto-Deposit: {(isTriggered ? "Active" : "Idle")}");
            GUILayout.Label($"Timer: {idleTimer:F1}s / {threshold:F1}s");
        }

        GUILayout.Space(5);

        // Depot info
        var depots = BuildingRegistry.Instance?.GetAll<IResourceStorage>();
        GUILayout.Label($"Depots Found: {depots?.Count ?? 0}");
    }
}