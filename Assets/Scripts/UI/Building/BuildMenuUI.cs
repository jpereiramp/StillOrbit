using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class BuildMenuUI : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private Transform slotsContainer;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildingSlotUI slotPrefab;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildModeController buildModeController;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CanvasGroup canvasGroup;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    [SerializeField] private List<BuildingSlotUI> activeSlots = new List<BuildingSlotUI>();

    // Events
    public event Action<BuildingData> OnBuildingSelected;

    private void Start()
    {
        buildModeController.OnBuildModeStateChanged += HandleStateChange;

        // Start hidden
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
    }

    private void OnDestroy()
    {
        buildModeController.OnBuildModeStateChanged -= HandleStateChange;
    }

    private void HandleStateChange(BuildModeController.BuildModeState newState)
    {
        Debug.Log("[BuildMenuUI] Build mode state changed to: " + newState);
        // Show menu only when in MenuOpen state
        if (newState == BuildModeController.BuildModeState.MenuOpen)
        {
            RefreshSlots();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
        }
    }

    private void RefreshSlots()
    {
        // Clear existing slots
        foreach (var slot in activeSlots)
        {
            if (slot == null) continue;
            Destroy(slot.gameObject);
        }
        activeSlots.Clear();

        // Create a slot for each building from database
        var buildingsDatabase = buildModeController.BuildingsDatabase;
        foreach (var buildingData in buildingsDatabase.AllBuildings)
        {
            if (buildingData == null) continue;

            BuildingSlotUI newSlot = Instantiate(slotPrefab, slotsContainer);
            bool canAfford = buildModeController.CanAffordBuilding(buildingData);

            newSlot.Setup(buildingData, HandleSlotClicked);
            newSlot.UpdateAffordability(canAfford);

            activeSlots.Add(newSlot);
        }

        Debug.Log("[BuildMenuUI] Refreshed building slots. Total slots: " + activeSlots.Count);
    }

    private void HandleSlotClicked(BuildingData building)
    {
        Debug.Log("[BuildMenuUI] Building selected: " + (building != null ? building.BuildingName : "None"));
        OnBuildingSelected?.Invoke(building);

        // Tells controller to enter placement mode
        buildModeController.SetSelectedBuilding(building);
    }
}