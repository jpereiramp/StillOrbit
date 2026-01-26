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
        if (playerManager.InputHandler.ToggleBuildModePressed)
        {
            ToggleBuildMode();
            playerManager.InputHandler.ToggleBuildModePressed = false;
        }

        if (playerManager.InputHandler.CancelBuildPressed)
        {
            if (currentState == BuildModeState.Placing)
            {
                EnterMenuMode();
            }
            else
            {
                ExitBuildMode();
            }
            playerManager.InputHandler.CancelBuildPressed = false;
        }
    }
    #endregion

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
        SetBuildModeState(BuildModeState.MenuOpen);
        SetSelectedBuilding(null);

        if (ghostController != null)
        {
            ghostController.ClearGhost();
        }
    }

    private void ExitBuildMode()
    {
        SetBuildModeState(BuildModeState.Inactive);
        SetSelectedBuilding(null);

        if (ghostController != null)
        {
            ghostController.ClearGhost();
        }
    }

    public void SetSelectedBuilding(BuildingData buildingData)
    {
        if (selectedBuilding != buildingData)
        {
            selectedBuilding = buildingData;
            OnSelectedBuildingChanged?.Invoke(selectedBuilding);

            if (ghostController != null)
            {
                ghostController.ShowGhost(selectedBuilding);
            }

            SetBuildModeState(BuildModeState.Placing);
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
}