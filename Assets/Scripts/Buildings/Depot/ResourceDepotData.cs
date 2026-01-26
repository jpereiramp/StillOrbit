using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Configuration data for Resource Depot buildings.
/// Extends BuildingData with storage-specific settings.
/// </summary>
[CreateAssetMenu(fileName = "New Resource Depot", menuName = "StillOrbit/Buildings/Resource Depot Data")]
public class ResourceDepotData : BuildingData
{
    [BoxGroup("Storage Settings")]
    [Min(1)]
    [Tooltip("Maximum amount of each resource type this depot can hold")]
    [SerializeField] private int capacityPerResource = 500;

    [BoxGroup("Storage Settings")]
    [Tooltip("Which resource types this depot accepts. Empty list = accepts all.")]
    [SerializeField] private List<ResourceType> acceptedResources = new List<ResourceType>();

    [BoxGroup("Storage Settings")]
    [Tooltip("If true, accepts all resource types regardless of the list above")]
    [SerializeField] private bool acceptAllResources = true;

    public int CapacityPerResource => capacityPerResource;
    public IReadOnlyList<ResourceType> AcceptedResources => acceptedResources;
    public bool AcceptAllResources => acceptAllResources;
}