using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// ScriptableObject database containing display metadata for all resource types.
/// Create one instance and reference it where you need resource display info (UI, tooltips, etc.).
/// </summary>
[CreateAssetMenu(fileName = "Resource Database", menuName = "StillOrbit/Resources/Resource Database")]
public class ResourceDatabase : ScriptableObject
{
    [Serializable]
    public class ResourceInfo
    {
        [HorizontalGroup("Row", Width = 70)]
        [PreviewField(50), HideLabel]
        public Sprite icon;

        [VerticalGroup("Row/Info")]
        [LabelWidth(80)]
        public ResourceType type;

        [VerticalGroup("Row/Info")]
        [LabelWidth(80)]
        public string displayName;

        [VerticalGroup("Row/Info")]
        [LabelWidth(80)]
        public ResourceCategory category;

        [TextArea(1, 2)]
        public string description;
    }

    [TableList(ShowIndexLabels = true)]
    [SerializeField]
    private List<ResourceInfo> resources = new List<ResourceInfo>();

    // Runtime lookup cache
    [NonSerialized]
    private Dictionary<ResourceType, ResourceInfo> lookupCache;

    [NonSerialized]
    private bool cacheInitialized;

    /// <summary>
    /// Singleton instance for easy access. Set this in your game initialization.
    /// </summary>
    public static ResourceDatabase Instance { get; set; }

    private void OnEnable()
    {
        // Auto-set instance if this is the first database loaded
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void EnsureCacheInitialized()
    {
        if (cacheInitialized) return;

        lookupCache = new Dictionary<ResourceType, ResourceInfo>();
        foreach (var info in resources)
        {
            if (info.type != ResourceType.None)
            {
                lookupCache[info.type] = info;
            }
        }
        cacheInitialized = true;
    }

    /// <summary>
    /// Get display info for a resource type.
    /// </summary>
    public ResourceInfo GetInfo(ResourceType type)
    {
        EnsureCacheInitialized();
        return lookupCache.TryGetValue(type, out var info) ? info : null;
    }

    /// <summary>
    /// Get display name for a resource type.
    /// Falls back to enum name if not configured.
    /// </summary>
    public string GetDisplayName(ResourceType type)
    {
        var info = GetInfo(type);
        if (info != null && !string.IsNullOrEmpty(info.displayName))
        {
            return info.displayName;
        }
        return type.ToString();
    }

    /// <summary>
    /// Get icon for a resource type.
    /// </summary>
    public Sprite GetIcon(ResourceType type)
    {
        return GetInfo(type)?.icon;
    }

    /// <summary>
    /// Get category for a resource type.
    /// </summary>
    public ResourceCategory GetCategory(ResourceType type)
    {
        return GetInfo(type)?.category ?? ResourceCategory.Raw;
    }

    /// <summary>
    /// Get all resources in a category.
    /// </summary>
    public List<ResourceInfo> GetResourcesByCategory(ResourceCategory category)
    {
        var result = new List<ResourceInfo>();
        foreach (var info in resources)
        {
            if (info.category == category)
            {
                result.Add(info);
            }
        }
        return result;
    }

#if UNITY_EDITOR
    [Button("Populate From Enum"), PropertyOrder(-1)]
    private void PopulateFromEnum()
    {
        var existing = new HashSet<ResourceType>();
        foreach (var info in resources)
        {
            existing.Add(info.type);
        }

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (type != ResourceType.None && !existing.Contains(type))
            {
                resources.Add(new ResourceInfo
                {
                    type = type,
                    displayName = type.ToString(),
                    category = GuessCategory(type)
                });
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private ResourceCategory GuessCategory(ResourceType type)
    {
        string name = type.ToString().ToLower();
        if (name.Contains("ore")) return ResourceCategory.Ore;
        if (name.Contains("ingot")) return ResourceCategory.Refined;
        if (name.Contains("fiber") || name.Contains("leather")) return ResourceCategory.Organic;
        return ResourceCategory.Raw;
    }
#endif
}
