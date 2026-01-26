using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Base ScriptableObject for all item definitions.
/// Holds static data about an item type (not instance state).
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "StillOrbit/Items/Item Data")]
public class ItemData : ScriptableObject
{
    [BoxGroup("Identity")]
    [PreviewField(75), HideLabel, HorizontalGroup("Identity/Split", Width = 75)]
    [SerializeField] private Sprite icon;

    [VerticalGroup("Identity/Split/Info")]
    [LabelWidth(80)]
    [SerializeField] private string itemName = "New Item";

    [VerticalGroup("Identity/Split/Info")]
    [LabelWidth(80)]
    [SerializeField] private string itemId;

    [TextArea(2, 4)]
    [SerializeField] private string description;

    [BoxGroup("Behavior")]
    [Tooltip("Can this item be picked up and stored in inventory?")]
    [SerializeField] private bool canPickup = true;

    [BoxGroup("Behavior")]
    [Tooltip("Can this item be equipped/held in hand?")]
    [SerializeField] private bool canEquip = true;

    [BoxGroup("Behavior")]
    [Tooltip("Maximum stack size in inventory (1 = no stacking)")]
    [Min(1)]
    [SerializeField] private int maxStackSize = 1;

    [BoxGroup("Prefabs")]
    [AssetsOnly]
    [Tooltip("Prefab spawned when item is dropped in world")]
    [SerializeField] private GameObject worldPrefab;

    [BoxGroup("Prefabs")]
    [AssetsOnly]
    [Tooltip("Prefab used when item is held in hand (can be same as world prefab)")]
    [SerializeField] private GameObject heldPrefab;

    // Public accessors
    public string ItemName => itemName;
    public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
    public string Description => description;
    public Sprite Icon => icon;
    public bool CanPickup => canPickup;
    public bool CanEquip => canEquip;
    public int MaxStackSize => maxStackSize;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject HeldPrefab => heldPrefab != null ? heldPrefab : worldPrefab;
    public bool IsStackable => maxStackSize > 1;

    /// <summary>
    /// Compare items by their ID (not by reference).
    /// Useful if you ever create runtime item instances.
    /// </summary>
    public bool IsSameItem(ItemData other)
    {
        if (other == null) return false;
        return ItemId == other.ItemId;
    }

#if UNITY_EDITOR
    [Button("Generate ID from Name"), BoxGroup("Identity")]
    private void GenerateId()
    {
        itemId = itemName.ToLowerInvariant().Replace(" ", "_");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        // Warn if itemId is not set - this can cause issues with save/load
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning($"[ItemData] '{name}' has no itemId set! " +
                "This may cause issues if you rename the asset. " +
                "Use 'Generate ID from Name' button to fix.", this);
        }
    }
#endif
}
