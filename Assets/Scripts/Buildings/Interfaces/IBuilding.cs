using System;
using UnityEngine;

/// <summary>
/// Base interface for all buildings.
/// Provides common properties and events without forcing inheritance.
/// </summary>
public interface IBuilding
{
    /// <summary>
    /// The building's configuration data.
    /// </summary>
    BuildingData Data { get; }

    /// <summary>
    /// The building's transform for positioning queries.
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// The building's health component (may be null for indestructible buildings).
    /// </summary>
    HealthComponent Health { get; }

    /// <summary>
    /// Whether this building is fully constructed and operational.
    /// </summary>
    bool IsOperational { get; }

    /// <summary>
    /// Fired when the building is destroyed or removed.
    /// </summary>
    event Action OnBuildingDestroyed;
}
