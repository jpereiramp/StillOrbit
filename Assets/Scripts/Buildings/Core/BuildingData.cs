using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Base ScriptableObject for building definitions.
/// Holds static configuration data (not instance state).
/// </summary>
[CreateAssetMenu(fileName = "New Building", menuName = "StillOrbit/Buildings/Building Data")]
public class BuildingData : ScriptableObject
{
    [BoxGroup("Identity")]
    [PreviewField(75), HideLabel, HorizontalGroup("Identity/Split", Width = 75)]
    [SerializeField] private Sprite icon;

    [VerticalGroup("Identity/Split/Info")]
    [LabelWidth(100)]
    [SerializeField] private string buildingName = "New Building";

    [VerticalGroup("Identity/Split/Info")]
    [LabelWidth(100)]
    [SerializeField] private string buildingId;

    [TextArea(2, 4)]
    [SerializeField] private string description;

    [BoxGroup("Prefab")]
    [AssetsOnly]
    [Required]
    [SerializeField] private GameObject buildingPrefab;

    [BoxGroup("Construction")]
    [SerializeField] private List<ResourceCost> constructionCosts = new List<ResourceCost>();

    [BoxGroup("Durability")]
    [Min(1)]
    [SerializeField] private int maxHealth = 100;

    [BoxGroup("Durability")]
    [SerializeField] private bool isIndestructible = false;

    // Public accessors
    public string BuildingName => buildingName;
    public string BuildingId => string.IsNullOrEmpty(buildingId) ? name : buildingId;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject BuildingPrefab => buildingPrefab;
    public IReadOnlyList<ResourceCost> ConstructionCosts => constructionCosts;
    public int MaxHealth => maxHealth;
    public bool IsIndestructible => isIndestructible;

#if UNITY_EDITOR
    [Button("Generate ID from Name"), BoxGroup("Identity")]
    private void GenerateId()
    {
        buildingId = buildingName.ToLowerInvariant().Replace(" ", "_");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

/// <summary>
/// Defines a resource cost for construction or upgrades.
/// </summary>
[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;

    [Min(1)]
    public int amount = 1;
}