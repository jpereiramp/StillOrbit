using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotUI : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    private Image itemIconImage;

    [BoxGroup("References")]
    [SerializeField]
    private GameObject highlightObject;

    [BoxGroup("References")]
    [SerializeField]
    private TextMeshProUGUI hotkeyText;

    private int slotIndex;
    private bool isSelected;

    // Public Accessors
    public int SlotIndex => slotIndex;

    public void Initialize(int index)
    {
        slotIndex = index;

        // Display hotkey number (1-5)
        if (hotkeyText != null)
        {
            hotkeyText.text = (index + 1).ToString();
        }

        SetEmpty();
        SetSelected(false);
    }

    public void UpdateDisplay(ItemData itemData)
    {
        if (itemData == null)
        {
            SetEmpty();
        }
        else
        {
            SetFilled(itemData);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (highlightObject != null)
        {
            highlightObject.SetActive(selected);
        }
    }

    private void SetEmpty()
    {
        if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.enabled = false;
        }
    }

    private void SetFilled(ItemData itemData)
    {
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.Icon;
            itemIconImage.enabled = true;
        }
    }
}