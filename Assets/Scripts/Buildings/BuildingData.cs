using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Building Data", menuName = "StillOrbit/Data/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Name of the building")]
    public string buildingName;

    [Tooltip("Prefab used for the building in the world")]
    public GameObject buildingPrefab;
}