using UnityEngine;

public class ResourceDepotBuildingData : BuildingData
{
    [Header("Resource Depot Settings")]
    [Tooltip("Maximum capacity of resources this depot can hold")]
    public int maxResourceCapacity = 1000;
}