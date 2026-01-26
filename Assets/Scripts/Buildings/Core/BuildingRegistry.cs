using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Central registry for all active buildings in the scene.
/// Allows systems to discover buildings by capability without direct references.
/// </summary>
public class BuildingRegistry : MonoBehaviour
{
    public static BuildingRegistry Instance { get; private set; }

    [ShowInInspector, ReadOnly]
    private List<Building> allBuildings = new List<Building>();

    /// <summary>
    /// Fired when a building is registered.
    /// </summary>
    public event Action<Building> OnBuildingAdded;

    /// <summary>
    /// Fired when a building is unregistered.
    /// </summary>
    public event Action<Building> OnBuildingRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BuildingRegistry] Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Register a building with the registry.
    /// Called automatically by Building.Start().
    /// </summary>
    public void Register(Building building)
    {
        if (building == null) return;

        if (!allBuildings.Contains(building))
        {
            allBuildings.Add(building);
            OnBuildingAdded?.Invoke(building);

            Debug.Log($"[BuildingRegistry] Registered: {building.Data?.BuildingName ?? building.name}");
        }
    }

    /// <summary>
    /// Unregister a building from the registry.
    /// Called automatically by Building.OnDestroy().
    /// </summary>
    public void Unregister(Building building)
    {
        if (building == null) return;

        if (allBuildings.Remove(building))
        {
            OnBuildingRemoved?.Invoke(building);

            Debug.Log($"[BuildingRegistry] Unregistered: {building.Data?.BuildingName ?? building.name}");
        }
    }

    /// <summary>
    /// Get all buildings currently registered.
    /// </summary>
    public IReadOnlyList<Building> GetAllBuildings() => allBuildings;

    /// <summary>
    /// Get count of all registered buildings.
    /// </summary>
    public int BuildingCount => allBuildings.Count;

    /// <summary>
    /// Get all buildings that implement a specific capability interface.
    /// </summary>
    /// <typeparam name="T">The interface type to search for.</typeparam>
    public List<T> GetAll<T>() where T : class
    {
        var result = new List<T>();

        foreach (var building in allBuildings)
        {
            if (building is T capability)
            {
                result.Add(capability);
            }
        }

        return result;
    }

    /// <summary>
    /// Find the nearest building with a specific capability to a position.
    /// </summary>
    /// <typeparam name="T">The interface type to search for.</typeparam>
    /// <param name="position">The position to measure distance from.</param>
    /// <returns>The nearest building with the capability, or null if none found.</returns>
    public T FindNearest<T>(Vector3 position) where T : class
    {
        T nearest = null;
        float nearestDistSq = float.MaxValue;

        foreach (var building in allBuildings)
        {
            if (building is T capability)
            {
                float distSq = (building.transform.position - position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = capability;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Find the nearest building with a specific capability to a position.
    /// Also outputs the distance.
    /// </summary>
    public T FindNearest<T>(Vector3 position, out float distance) where T : class
    {
        T nearest = null;
        float nearestDistSq = float.MaxValue;

        foreach (var building in allBuildings)
        {
            if (building is T capability)
            {
                float distSq = (building.transform.position - position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = capability;
                }
            }
        }

        distance = nearest != null ? Mathf.Sqrt(nearestDistSq) : 0f;
        return nearest;
    }

    /// <summary>
    /// Find all buildings with a specific capability within a radius.
    /// </summary>
    /// <typeparam name="T">The interface type to search for.</typeparam>
    /// <param name="position">The center position.</param>
    /// <param name="radius">The search radius.</param>
    public List<T> FindWithinRadius<T>(Vector3 position, float radius) where T : class
    {
        var result = new List<T>();
        float radiusSq = radius * radius;

        foreach (var building in allBuildings)
        {
            if (building is T capability)
            {
                float distSq = (building.transform.position - position).sqrMagnitude;
                if (distSq <= radiusSq)
                {
                    result.Add(capability);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Find a building by its ID.
    /// </summary>
    public Building FindById(string buildingId)
    {
        foreach (var building in allBuildings)
        {
            if (building.Data != null && building.Data.BuildingId == buildingId)
            {
                return building;
            }
        }
        return null;
    }

    /// <summary>
    /// Get count of buildings with a specific capability.
    /// </summary>
    public int Count<T>() where T : class
    {
        int count = 0;
        foreach (var building in allBuildings)
        {
            if (building is T)
            {
                count++;
            }
        }
        return count;
    }

#if UNITY_EDITOR
    [Button("Log All Buildings"), BoxGroup("Debug")]
    private void DebugLogBuildings()
    {
        Debug.Log($"[BuildingRegistry] Total buildings: {allBuildings.Count}");
        foreach (var building in allBuildings)
        {
            Debug.Log($"  - {building.Data?.BuildingName ?? building.name} ({building.GetType().Name})");
        }
    }
#endif
}
