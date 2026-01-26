using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour
{
    [BoxGroup("UI Elements")]
    [SerializeField] private Image iconImage;
    [BoxGroup("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [BoxGroup("UI Elements")]
    [SerializeField] private Button button;
    [BoxGroup("UI Elements")]
    [SerializeField] private Image backgroundImage;

    [BoxGroup("Visual Settings")]
    [SerializeField] private Color affordableColor = Color.yellow;
    [BoxGroup("Visual Settings")]
    [SerializeField] private Color unaffordableColor = Color.white;

    [BoxGroup("Data")]
    [SerializeField]
    private BuildingData buildingData;

    public BuildingData BuildingData => buildingData;

    // Events
    public Action<BuildingData> OnBuildingSlotClicked;

    private void Awake()
    {
        button.onClick.AddListener(HandleButtonClick);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(HandleButtonClick);
    }

    public void Setup(BuildingData buildingData, Action<BuildingData> onSlotClicked)
    {
        if (buildingData == null) return;

        this.buildingData = buildingData;
        OnBuildingSlotClicked = onSlotClicked;

        // Update icon and name
        if (iconImage != null)
            iconImage.sprite = buildingData.Icon;
        if (nameText != null)
            nameText.text = buildingData.BuildingName;
    }

    public void UpdateAffordability(bool canAfford)
    {
        // Update affordability color & interactions
        if (backgroundImage != null)
        {
            backgroundImage.color = canAfford ? affordableColor : unaffordableColor;
        }

        if (nameText != null)
        {
            nameText.color = canAfford ? Color.black : Color.lightGray;
        }

        button.interactable = canAfford;

    }

    private void HandleButtonClick()
    {
        if (buildingData != null)
        {
            OnBuildingSlotClicked?.Invoke(buildingData);
        }
    }
}