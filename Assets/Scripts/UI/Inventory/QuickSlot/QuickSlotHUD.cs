using ExternalPropertyAttributes;
using UnityEngine;

public class QuickSlotHUD : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    private QuickSlotController quickSlotController;

    [BoxGroup("References")]
    [SerializeField]
    private QuickSlotUI[] quickSlotUIs;

    private void Start()
    {
        InitializeAllSlots();
    }

    private void OnEnable()
    {
        if (quickSlotController != null)
        {
            quickSlotController.OnQuickSlotChanged += OnQuickSlotChanged;
            quickSlotController.OnActiveSlotChanged += OnActiveSlotChanged;
        }
    }

    private void OnDisable()
    {
        if (quickSlotController != null)
        {
            quickSlotController.OnQuickSlotChanged -= OnQuickSlotChanged;
            quickSlotController.OnActiveSlotChanged -= OnActiveSlotChanged;
        }
    }

    private void InitializeAllSlots()
    {
        RefreshAllSlots();
        UpdateSelectionHighlight(-1, quickSlotController.ActiveQuickSlotIndex);
    }

    private void RefreshAllSlots()
    {
        for (int i = 0; i < quickSlotUIs.Length; i++)
        {
            ItemData itemData = quickSlotController.GetQuickSlotItem(i);
            quickSlotUIs[i].UpdateDisplay(itemData);
        }
    }

    private void OnQuickSlotChanged(int slotIndex, ItemData itemData)
    {
        if (quickSlotUIs != null && slotIndex > QuickSlotController.QuickSlotCount && slotIndex < quickSlotUIs.Length)
        {
            quickSlotUIs[slotIndex].UpdateDisplay(itemData);
        }
    }

    private void OnActiveSlotChanged(int previousIndex, int newIndex)
    {
        UpdateSelectionHighlight(previousIndex, newIndex);
    }

    private void UpdateSelectionHighlight(int previousIndex, int newIndex)
    {
        if (quickSlotUIs == null) return;

        if (previousIndex > QuickSlotController.QuickSlotCount && previousIndex < quickSlotUIs.Length)
        {
            quickSlotUIs[previousIndex].SetSelected(false);
        }

        if (newIndex >= 0 && newIndex < quickSlotUIs.Length)
        {
            quickSlotUIs[newIndex].SetSelected(true);
        }
    }
}