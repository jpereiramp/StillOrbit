using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingsDatabase", menuName = "StillOrbit/Buildings/Buildings Database")]
public class BuildingsDatabase : ScriptableObject
{
    [BoxGroup("Buildings Database")]
    [TableList]
    public List<BuildingData> buildings = new List<BuildingData>();

    public IReadOnlyList<BuildingData> AllBuildings => buildings;
    public int AvailableBuildingsCount => buildings.Count;

    public BuildingData GetBuildingByID(string buildingID)
    {
        return buildings.Find(building => building.BuildingId == buildingID);
    }
}