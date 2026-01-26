using System;
using Sirenix.OdinInspector;
using UnityEngine;

public enum BuildModeState
{
    Inactive,
    MenuOpen,
    Placing
}

public class BuildModeController : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildingsDatabase buildingsDatabase;


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
        if (playerManager.InputHandler.ToggleBuildModeInput)
            ToggleBuildMode();

        if (playerManager.InputHandler.CancelBuildInput)
        {
            if (currentState == BuildModeState.Placing)
            {
                EnterMenuMode();
            }
            else
            {
                ExitBuildMode();
            }
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
    }

    private void ExitBuildMode()
    {
        SetBuildModeState(BuildModeState.Inactive);
        SetSelectedBuilding(null);
    }

    private void SetSelectedBuilding(BuildingData buildingData)
    {
        if (selectedBuilding != buildingData)
        {
            selectedBuilding = buildingData;
            OnSelectedBuildingChanged?.Invoke(selectedBuilding);
        }
    }

    private void SetBuildModeState(BuildModeState newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
            OnBuildModeStateChanged?.Invoke(currentState);
        }
    }

    #region Controls
    private void SetPlayerControlsEnabled(bool enabled)
    {
        if (playerManager == null) return;

        if (playerManager.LocomotionController != null)
        {
            playerManager.LocomotionController.SetCharacterControllerMotorEnabled(enabled);
        }
    }
    #endregion
}