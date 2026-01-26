using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class BuildModeController : MonoBehaviour
{
    public enum BuildModeState
    {
        Inactive,
        MenuOpen,
        Placing
    }

    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildingsDatabase buildingsDatabase;

    [BoxGroup("References")]
    [SerializeField] private BuildingGhostController ghostController;


    [BoxGroup("References")]
    [Required]
    [SerializeField] private PlayerManager playerManager;

    [BoxGroup("State")]
    [SerializeField] private BuildModeState currentState = BuildModeState.Inactive;


    [BoxGroup("State")]
    [SerializeField] private BuildingData selectedBuilding;

    // Public Accessors
    public BuildModeState CurrentState => currentState;
    public bool IsInBuildMode => currentState != BuildModeState.Inactive;
    public BuildingData SelectedBuilding => selectedBuilding;
    public BuildingsDatabase BuildingsDatabase => buildingsDatabase;
    public BuildingGhostController GhostController => ghostController;


    // Events
    public event Action<BuildModeState> OnBuildModeStateChanged;
    public event Action<BuildingData> OnSelectedBuildingChanged;
    public event Action<Building> OnBuildingPlaced;

    #region Lifecycle
    private void Awake()
    {
        if (buildingsDatabase == null)
        {
            Debug.LogError("BuildingsDatabase reference is missing in BuildModeController.");
        }

        if (playerManager == null)
        {
            Debug.LogError("PlayerManager reference is missing in BuildModeController.");
        }
    }

    private void Update()
    {
        HandleInput();
    }
    #endregion

    #region Inputs
    private void HandleInput()
    {
        if (playerManager.InputHandler.ToggleBuildModePressed)
        {
            ToggleBuildMode();
            playerManager.InputHandler.ToggleBuildModePressed = false;
        }

        if (playerManager.InputHandler.CancelBuildPressed)
        {
            Debug.Log($"[BuildModeController] Cancel pressed. Current state: {currentState}");

            if (currentState == BuildModeState.Placing)
            {
                EnterMenuMode();
            }
            else
            {
                ExitBuildMode();
            }

            playerManager.InputHandler.CancelBuildPressed = false;
            Debug.Log($"[BuildModeController] After cancel. New state: {currentState}");
        }

        if (playerManager.InputHandler.ConfirmBuildPressed)
        {
            Debug.Log("confirm input detected");
            TryConfirmBuildPlacement();
            playerManager.InputHandler.ConfirmBuildPressed = false;
        }
    }
    #endregion

    #region Modes
    private void ToggleBuildMode()
    {
        if (currentState == BuildModeState.Inactive)
        {
            EnterBuildMode();
        }
        else
        {
            ExitBuildMode();
        }
    }

    private void EnterBuildMode()
    {
        SetBuildModeState(BuildModeState.MenuOpen);
    }

    private void EnterMenuMode()
    {
        // Clear ghost before nulling selectedBuilding
        if (ghostController != null)
        {
            ghostController.ClearGhost();
        }

        SetSelectedBuilding(null);
        SetBuildModeState(BuildModeState.MenuOpen);
    }

    private void ExitBuildMode()
    {
        // Clear ghost before nulling selectedBuilding
        if (ghostController != null)
        {
            ghostController.ClearGhost();
        }

        SetSelectedBuilding(null);
        SetBuildModeState(BuildModeState.Inactive);
    }

    public void SetSelectedBuilding(BuildingData buildingData)
    {
        if (selectedBuilding != buildingData)
        {
            selectedBuilding = buildingData;
            OnSelectedBuildingChanged?.Invoke(selectedBuilding);

            if (ghostController != null)
            {
                if (selectedBuilding != null)
                {
                    ghostController.ShowGhost(selectedBuilding);
                    SetBuildModeState(BuildModeState.Placing);
                }
                else
                {
                    ghostController.ClearGhost();
                }
            }
        }
    }

    private void SetBuildModeState(BuildModeState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            OnBuildModeStateChanged?.Invoke(currentState);
        }

        SetPlayerControlsEnabled(newState == BuildModeState.Inactive || newState == BuildModeState.Placing);
        playerManager.AimController.SetCursorInteractionEnabled(newState == BuildModeState.MenuOpen);
    }
    #endregion

    #region Placement
    /// <summary>
    /// Attempt to place the building at the current ghost position.
    /// </summary>
    public void TryConfirmBuildPlacement()
    {
        if (currentState != BuildModeState.Placing)
        {
            return;
        }

        if (selectedBuilding == null)
        {
            Debug.LogWarning("[BuildModeController] No building selected");
            return;
        }

        if (ghostController == null || !ghostController.HasGhost)
        {
            Debug.LogWarning("[BuildModeController] No ghost to place");
            return;
        }

        // Check validity
        if (!ghostController.ValidatePlacementConfirmation())
        {
            Debug.Log("[BuildModeController] Invalid placement position");
            // TODO: Play error sound
            return;
        }

        // Check and deduct resources
        if (!TryDeductBuildingCosts(selectedBuilding))
        {
            Debug.Log("[BuildModeController] Cannot afford building");
            // TODO: Play error sound
            return;
        }

        // Instantiate the building
        Building newBuilding = InstantiateBuilding(
            selectedBuilding,
            ghostController.GhostPosition,
            ghostController.GhostRotation
        );

        if (newBuilding != null)
        {
            OnBuildingPlaced?.Invoke(newBuilding);
            Debug.Log($"[BuildModeController] Placed: {selectedBuilding.BuildingName} at {ghostController.GhostPosition}");
        }

        // Clean up and exit - this should clear ghost, null selection, and go to Inactive
        ExitBuildMode();

        Debug.Log($"[BuildModeController] Post-placement state: {currentState}, Selected: {selectedBuilding}, HasGhost: {ghostController?.HasGhost}");
    }

    private Building InstantiateBuilding(BuildingData buildingData, Vector3 position, Quaternion rotation)
    {
        if (buildingData == null || buildingData.BuildingPrefab == null)
        {
            Debug.LogError("[BuildModeController] Cannot instantiate: null building data or prefab");
            return null;
        }

        GameObject instance = Instantiate(buildingData.BuildingPrefab, position, rotation);
        instance.name = buildingData.BuildingName;

        // Ensure correct layer
        SetLayerRecursive(instance, LayerMask.NameToLayer("Building"));

        // Get and return the Building component
        Building building = instance.GetComponent<Building>();

        if (building == null)
        {
            Debug.LogWarning($"[BuildModeController] Placed object has no Building component: {buildingData.BuildingName}");
        }

        return building;
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
    #endregion

    #region Controls
    private void SetPlayerControlsEnabled(bool enabled)
    {
        if (playerManager == null) return;

        if (playerManager.LocomotionController != null)
        {
            playerManager.LocomotionController.SetCharacterControllerMotorEnabled(enabled);
        }

        if (playerManager.CameraController != null)
        {
            playerManager.CameraController.SetCameraMovementEnabled(enabled);
        }
    }
    #endregion

    #region Cost Calculation
    public bool CanAffordBuilding(BuildingData buildingData)
    {
        if (buildingData == null) return false;

        ResourceInventory playerInventory = playerManager.ResourceInventory.Inventory;
        foreach (var cost in buildingData.ConstructionCosts)
        {
            int playerAmount = playerInventory.Get(cost.resourceType);
            if (playerAmount < cost.amount)
            {
                return false;
            }
        }
        return true;
    }

    private bool TryDeductBuildingCosts(BuildingData buildingData)
    {
        if (buildingData == null) return false;

        ResourceInventory playerInventory = playerManager.ResourceInventory.Inventory;

        // First check if we can afford
        bool canAfford = CanAffordBuilding(buildingData);
        if (!canAfford)
        {
            return false;
        }

        // Deduct costs
        foreach (var cost in buildingData.ConstructionCosts)
        {
            bool success = playerInventory.TryRemove(cost.resourceType, cost.amount);
            if (!success)
            {
                Debug.LogError("Unexpected failure to deduct resources after affordability check.");
                return false;
            }
        }

        return true;
    }
    #endregion
}