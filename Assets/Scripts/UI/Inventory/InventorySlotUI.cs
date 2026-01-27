using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    private Image iconImage;

    [BoxGroup("References")]
    [SerializeField]
    private TextMeshProUGUI itemNameText;

    [BoxGroup("References")]
    [SerializeField]
    private TextMeshProUGUI quantityText;

    private int _slotIndex;

    // Public Accessors
    public int SlotIndex => _slotIndex;

    public void Initialize(int slotIndex)
    {
        _slotIndex = slotIndex;
        ClearSlot();
    }

    public void UpdateDisplay(ItemData itemData, int quantity)
    {
        if (itemData != null && quantity > 0)
        {
            itemNameText.text = itemData.ItemName;
            iconImage.sprite = itemData.Icon;
            iconImage.enabled = true;
            quantityText.text = quantity > 1 ? quantity.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    private void ClearSlot()
    {
        itemNameText.text = "Empty";
        iconImage.sprite = null;
        iconImage.enabled = false;
        quantityText.text = "";
    }
}