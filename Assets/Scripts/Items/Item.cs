using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Component that links a runtime GameObject to its ItemData.
/// Attach to held item prefabs or world item prefabs to maintain a reference
/// to the defining ScriptableObject.
/// </summary>
public class Item : MonoBehaviour
{
    [Required("Item Data is required")]
    [SerializeField]
    private ItemData itemData;

    /// <summary>
    /// The ItemData ScriptableObject defining this item's properties.
    /// </summary>
    public ItemData Data => itemData;

    /// <summary>
    /// Convenience accessor for item name.
    /// </summary>
    public string ItemName => itemData != null ? itemData.ItemName : "Unknown";

    /// <summary>
    /// Sets the item data at runtime (e.g., when instantiating from inventory).
    /// </summary>
    public void SetItemData(ItemData data)
    {
        itemData = data;
    }
}