# Buildings System Architecture Guide

> **StillOrbit** — A modular, interface-driven buildings system for Unity

---

## Table of Contents

1. [Design Philosophy](#1-design-philosophy)
2. [Architecture Overview](#2-architecture-overview)
3. [Core Components](#3-core-components)
4. [Implementation Guide](#4-implementation-guide)
5. [Data Flow Examples](#5-data-flow-examples)
6. [Future-Proofing for Modules](#6-future-proofing-for-modules)
7. [File Organization](#7-file-organization)

---

## 1. Design Philosophy

### Core Principles

| Principle | Description |
|-----------|-------------|
| **Interface-First** | External systems interact via interfaces, not concrete types |
| **Data-Driven** | ScriptableObjects define building configuration |
| **Composition over Inheritance** | Use components (`HealthComponent`) instead of deep hierarchies |
| **Discovery via Registry** | Buildings register themselves; consumers query the registry |

### Buildings as Service Providers

Buildings are **service providers**. Other systems (player, companions, automation) don't care about building internals—they only care about what services a building exposes via interfaces.

```
┌─────────────────────────────────────────────────────────────┐
│                     CONSUMER SYSTEMS                        │
│  (Player, Companions, Automation, UI)                       │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ queries via interface
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                     INTERFACE LAYER                         │
│  IResourceStorage, IInteractable, IManufacturing, etc.      │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ implemented by
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                   BUILDING IMPLEMENTATIONS                  │
│  ResourceDepot, Factory, Generator, etc.                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        DATA LAYER                               │
├─────────────────────────────────────────────────────────────────┤
│  BuildingData (ScriptableObject)                                │
│  └── Defines static configuration (name, icon, prefab, costs)   │
│                                                                 │
│  ResourceDepotData : BuildingData                               │
│  └── Extends with capacity, accepted resource types             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      INTERFACE LAYER                            │
├─────────────────────────────────────────────────────────────────┤
│  IResourceStorage        │  IInteractable (existing)            │
│  └── Deposit/withdraw    │  └── Player interaction              │
│                          │                                      │
│  IBuilding               │  IDamageable (existing)              │
│  └── Base building       │  └── Building health                 │
│      contract            │                                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      RUNTIME LAYER                              │
├─────────────────────────────────────────────────────────────────┤
│  Building (MonoBehaviour)                                       │
│  └── Base class for all buildings                               │
│  └── Holds BuildingData reference                               │
│  └── Manages HealthComponent                                    │
│  └── Registers with BuildingRegistry                            │
│                                                                 │
│  ResourceDepot : Building, IResourceStorage, IInteractable      │
│  └── Implements storage capabilities                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     DISCOVERY LAYER                             │
├─────────────────────────────────────────────────────────────────┤
│  BuildingRegistry (Singleton MonoBehaviour)                     │
│  └── Tracks all active buildings                                │
│  └── Provides queries: FindNearest<T>(), GetAll<T>()            │
│  └── Events: OnBuildingAdded, OnBuildingRemoved                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Core Components

### 3.1 IBuilding Interface

Base contract for all buildings. Provides common properties without forcing inheritance.

```csharp
// Assets/Scripts/Buildings/Interfaces/IBuilding.cs

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
```

### 3.2 IResourceStorage Interface

Contract for any building that stores resources.

```csharp
// Assets/Scripts/Buildings/Interfaces/IResourceStorage.cs

using System;
using System.Collections.Generic;

/// <summary>
/// Interface for buildings that can store resources.
/// Implemented by: ResourceDepot, and potentially player backpacks, companion storage, etc.
/// </summary>
public interface IResourceStorage
{
    /// <summary>
    /// Check if this storage accepts a specific resource type.
    /// </summary>
    bool CanAcceptResource(ResourceType resourceType);

    /// <summary>
    /// Try to deposit resources into storage.
    /// </summary>
    /// <param name="resourceType">The type of resource to deposit.</param>
    /// <param name="amount">The amount to deposit.</param>
    /// <returns>The actual amount deposited (may be less if storage is full).</returns>
    int TryDeposit(ResourceType resourceType, int amount);

    /// <summary>
    /// Try to withdraw resources from storage.
    /// </summary>
    /// <param name="resourceType">The type of resource to withdraw.</param>
    /// <param name="amount">The amount to withdraw.</param>
    /// <returns>The actual amount withdrawn (may be less if insufficient stock).</returns>
    int TryWithdraw(ResourceType resourceType, int amount);

    /// <summary>
    /// Get current amount of a specific resource.
    /// </summary>
    int GetStoredAmount(ResourceType resourceType);

    /// <summary>
    /// Get remaining capacity for a specific resource.
    /// Returns int.MaxValue if unlimited.
    /// </summary>
    int GetRemainingCapacity(ResourceType resourceType);

    /// <summary>
    /// Get all stored resources and their amounts.
    /// </summary>
    IEnumerable<KeyValuePair<ResourceType, int>> GetAllStored();

    /// <summary>
    /// Fired when storage contents change.
    /// Parameters: (ResourceType type, int newAmount)
    /// </summary>
    event Action<ResourceType, int> OnStorageChanged;
}
```

### 3.3 BuildingData (ScriptableObject)

Base configuration for all building types.

```csharp
// Assets/Scripts/Buildings/Data/BuildingData.cs

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

    [BoxGroup("Construction")]
    [Min(0f)]
    [SerializeField] private float constructionTime = 5f;

    [BoxGroup("Durability")]
    [Min(1f)]
    [SerializeField] private float maxHealth = 100f;

    [BoxGroup("Durability")]
    [SerializeField] private bool isIndestructible = false;

    // Public accessors
    public string BuildingName => buildingName;
    public string BuildingId => string.IsNullOrEmpty(buildingId) ? name : buildingId;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject BuildingPrefab => buildingPrefab;
    public IReadOnlyList<ResourceCost> ConstructionCosts => constructionCosts;
    public float ConstructionTime => constructionTime;
    public float MaxHealth => maxHealth;
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
```

### 3.4 Building Base Class

Runtime component attached to building prefabs.

```csharp
// Assets/Scripts/Buildings/Core/Building.cs

using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Base class for all buildings.
/// Handles lifecycle, registration, and common functionality.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class Building : MonoBehaviour, IBuilding
{
    [BoxGroup("Configuration")]
    [Required]
    [SerializeField]
    protected BuildingData buildingData;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isOperational = true;

    // Cached components
    private HealthComponent healthComponent;

    // IBuilding implementation
    public BuildingData Data => buildingData;
    public Transform Transform => transform;
    public HealthComponent Health => healthComponent;
    public bool IsOperational => isOperational;

    public event Action OnBuildingDestroyed;

    protected virtual void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();

        // Configure health from building data
        if (buildingData != null && healthComponent != null)
        {
            healthComponent.SetMaxHealth(buildingData.MaxHealth);

            if (buildingData.IsIndestructible)
            {
                healthComponent.SetInvulnerable(true);
            }
        }
    }

    protected virtual void Start()
    {
        // Register with the building registry
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.Register(this);
        }
    }

    protected virtual void OnEnable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath += HandleDestruction;
        }
    }

    protected virtual void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleDestruction;
        }
    }

    protected virtual void OnDestroy()
    {
        // Unregister from the building registry
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.Unregister(this);
        }

        OnBuildingDestroyed?.Invoke();
    }

    /// <summary>
    /// Called when the building's health reaches zero.
    /// </summary>
    protected virtual void HandleDestruction()
    {
        isOperational = false;

        Debug.Log($"[Building] {buildingData?.BuildingName ?? gameObject.name} destroyed!");

        // Subclasses can override to add destruction effects, drops, etc.
        OnDestroyBuilding();
    }

    /// <summary>
    /// Override in subclasses to add destruction behavior.
    /// </summary>
    protected virtual void OnDestroyBuilding()
    {
        // Default: just destroy the GameObject
        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// Set the building's operational state.
    /// </summary>
    public void SetOperational(bool operational)
    {
        if (isOperational == operational) return;

        isOperational = operational;
        OnOperationalStateChanged(operational);
    }

    /// <summary>
    /// Override to respond to operational state changes.
    /// </summary>
    protected virtual void OnOperationalStateChanged(bool operational)
    {
        // Subclasses can override (e.g., disable production, visual changes)
    }

#if UNITY_EDITOR
    [Button("Damage (10)"), BoxGroup("Debug")]
    private void DebugDamage()
    {
        if (Application.isPlaying && healthComponent != null)
        {
            healthComponent.TakeDamage(10f, DamageType.Generic, null);
        }
    }
#endif
}
```

### 3.5 BuildingRegistry

Discovery system for finding buildings by capability.

```csharp
// Assets/Scripts/Buildings/Core/BuildingRegistry.cs

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
```

---

## 4. Implementation Guide

### 4.1 Creating a Resource Depot

The Resource Depot is a building that stores resources. It implements both `IResourceStorage` (for resource operations) and `IInteractable` (for player interaction).

#### Step 1: Create ResourceDepotData

```csharp
// Assets/Scripts/Buildings/Depot/ResourceDepotData.cs

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
    [Tooltip("Which resource types this depot accepts. Empty = accepts all.")]
    [SerializeField] private List<ResourceType> acceptedResources = new List<ResourceType>();

    [BoxGroup("Storage Settings")]
    [Tooltip("If true, accepts all resource types regardless of the list above")]
    [SerializeField] private bool acceptAllResources = true;

    public int CapacityPerResource => capacityPerResource;
    public IReadOnlyList<ResourceType> AcceptedResources => acceptedResources;
    public bool AcceptAllResources => acceptAllResources;
}
```

#### Step 2: Create ResourceDepot MonoBehaviour

```csharp
// Assets/Scripts/Buildings/Depot/ResourceDepot.cs

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// A building that stores resources.
/// Implements IResourceStorage for resource operations and IInteractable for player interaction.
/// </summary>
public class ResourceDepot : Building, IResourceStorage, IInteractable
{
    [BoxGroup("Depot Settings")]
    [ShowInInspector, ReadOnly]
    private ResourceInventory storage = new ResourceInventory();

    // Cache the typed data for convenience
    private ResourceDepotData DepotData => buildingData as ResourceDepotData;

    // Events
    public event Action<ResourceType, int> OnStorageChanged;

    #region IInteractable Implementation

    public string InteractionPrompt => $"Access {buildingData?.BuildingName ?? "Depot"}";

    public bool CanInteract(GameObject interactor)
    {
        return IsOperational;
    }

    public void Interact(GameObject interactor)
    {
        if (!IsOperational)
        {
            Debug.Log("[ResourceDepot] Cannot interact - depot is not operational");
            return;
        }

        Debug.Log($"[ResourceDepot] {interactor.name} is accessing the depot");

        // TODO: Open depot UI
        // DepotUI.Instance?.Open(this, interactor.GetComponent<IResourceHolder>());

        // For now, just log contents
        LogContents();
    }

    #endregion

    #region IResourceStorage Implementation

    public bool CanAcceptResource(ResourceType resourceType)
    {
        if (resourceType == ResourceType.None) return false;
        if (!IsOperational) return false;

        if (DepotData == null) return true;

        if (DepotData.AcceptAllResources) return true;

        return DepotData.AcceptedResources.Contains(resourceType);
    }

    public int TryDeposit(ResourceType resourceType, int amount)
    {
        if (!CanAcceptResource(resourceType) || amount <= 0)
            return 0;

        int capacity = GetRemainingCapacity(resourceType);
        int toDeposit = Mathf.Min(amount, capacity);

        if (toDeposit > 0)
        {
            storage.Add(resourceType, toDeposit);
            OnStorageChanged?.Invoke(resourceType, storage.Get(resourceType));

            Debug.Log($"[ResourceDepot] Deposited {toDeposit}x {resourceType}. " +
                      $"Total: {storage.Get(resourceType)}");
        }

        return toDeposit;
    }

    public int TryWithdraw(ResourceType resourceType, int amount)
    {
        if (resourceType == ResourceType.None || amount <= 0 || !IsOperational)
            return 0;

        int available = storage.Get(resourceType);
        int toWithdraw = Mathf.Min(amount, available);

        if (toWithdraw > 0)
        {
            storage.TryRemove(resourceType, toWithdraw);
            OnStorageChanged?.Invoke(resourceType, storage.Get(resourceType));

            Debug.Log($"[ResourceDepot] Withdrew {toWithdraw}x {resourceType}. " +
                      $"Remaining: {storage.Get(resourceType)}");
        }

        return toWithdraw;
    }

    public int GetStoredAmount(ResourceType resourceType)
    {
        return storage.Get(resourceType);
    }

    public int GetRemainingCapacity(ResourceType resourceType)
    {
        if (!CanAcceptResource(resourceType))
            return 0;

        int maxCapacity = DepotData?.CapacityPerResource ?? int.MaxValue;
        int current = storage.Get(resourceType);

        return maxCapacity - current;
    }

    public IEnumerable<KeyValuePair<ResourceType, int>> GetAllStored()
    {
        return storage.GetAll();
    }

    #endregion

    #region Building Overrides

    protected override void Awake()
    {
        base.Awake();

        // Subscribe to storage changes
        storage.OnResourceChanged += HandleStorageChanged;
    }

    protected override void OnDestroy()
    {
        storage.OnResourceChanged -= HandleStorageChanged;
        base.OnDestroy();
    }

    protected override void OnDestroyBuilding()
    {
        // When depot is destroyed, drop stored resources or handle them somehow
        Debug.Log($"[ResourceDepot] Depot destroyed! Resources lost: {storage.GetTotalCount()}");

        // TODO: Optionally spawn resource pickups, transfer to player, etc.

        base.OnDestroyBuilding();
    }

    #endregion

    private void HandleStorageChanged(ResourceType type, int newAmount)
    {
        OnStorageChanged?.Invoke(type, newAmount);
    }

    [Button("Log Contents"), BoxGroup("Debug")]
    private void LogContents()
    {
        Debug.Log($"[ResourceDepot] {buildingData?.BuildingName ?? name} contents:");
        foreach (var kvp in storage.GetAll())
        {
            int capacity = DepotData?.CapacityPerResource ?? 0;
            Debug.Log($"  {kvp.Key}: {kvp.Value}/{capacity}");
        }
    }

#if UNITY_EDITOR
    [Button("Add Test Resources"), BoxGroup("Debug")]
    private void DebugAddResources()
    {
        TryDeposit(ResourceType.Wood, 50);
        TryDeposit(ResourceType.Stone, 30);
        TryDeposit(ResourceType.IronOre, 10);
    }
#endif
}
```

### 4.2 Setting Up the Scene

1. **Create BuildingRegistry GameObject:**
   ```
   - Create empty GameObject named "BuildingRegistry"
   - Add BuildingRegistry component
   - This should persist across scenes (or exist in each scene)
   ```

2. **Create Building Prefab:**
   ```
   - Create prefab with visual mesh
   - Add Building (or ResourceDepot) component
   - Add HealthComponent
   - Add colliders for interaction
   - Assign BuildingData ScriptableObject
   ```

3. **Create BuildingData Asset:**
   ```
   - Right-click in Project: Create → StillOrbit → Buildings → Resource Depot Data
   - Configure name, icon, costs, capacity
   - Assign the building prefab
   ```

---

## 5. Data Flow Examples

### 5.1 Companion Deposits Resources

```csharp
// In CompanionAI.cs or similar

private void DepositCollectedResources()
{
    // 1. Find nearest storage
    var storage = BuildingRegistry.Instance?.FindNearest<IResourceStorage>(transform.position);

    if (storage == null)
    {
        Debug.Log("[Companion] No storage depot found nearby");
        return;
    }

    // 2. Deposit each resource type
    foreach (var kvp in myResources.GetAll().ToList())
    {
        ResourceType type = kvp.Key;
        int amount = kvp.Value;

        // Check if depot accepts this resource
        if (!storage.CanAcceptResource(type))
            continue;

        // Deposit as much as possible
        int deposited = storage.TryDeposit(type, amount);

        // Remove deposited amount from companion inventory
        if (deposited > 0)
        {
            myResources.TryRemove(type, deposited);
            Debug.Log($"[Companion] Deposited {deposited}x {type}");
        }
    }
}
```

### 5.2 Crafting System Checks Resources

```csharp
// In CraftingManager.cs or similar

public bool CanCraft(CraftingRecipe recipe)
{
    // Get all storage buildings
    var storages = BuildingRegistry.Instance?.GetAll<IResourceStorage>();
    if (storages == null || storages.Count == 0)
        return false;

    // Check each required resource
    foreach (var cost in recipe.Costs)
    {
        int totalAvailable = 0;

        // Sum across all storage buildings
        foreach (var storage in storages)
        {
            totalAvailable += storage.GetStoredAmount(cost.resourceType);
        }

        if (totalAvailable < cost.amount)
            return false;
    }

    return true;
}

public void Craft(CraftingRecipe recipe)
{
    var storages = BuildingRegistry.Instance?.GetAll<IResourceStorage>();

    foreach (var cost in recipe.Costs)
    {
        int remaining = cost.amount;

        foreach (var storage in storages)
        {
            if (remaining <= 0) break;

            int withdrawn = storage.TryWithdraw(cost.resourceType, remaining);
            remaining -= withdrawn;
        }
    }

    // Create crafted item...
}
```

### 5.3 Player Interaction Flow

```csharp
// In PlayerInteractionController.cs

private void HandleInteraction()
{
    // Raycast or overlap to find interactable
    if (Physics.Raycast(cameraTransform.position, cameraTransform.forward,
                        out RaycastHit hit, interactionRange))
    {
        var interactable = hit.collider.GetComponent<IInteractable>();

        if (interactable != null && interactable.CanInteract(gameObject))
        {
            // Show prompt
            interactionPrompt.text = interactable.InteractionPrompt;

            // Handle input
            if (Input.GetKeyDown(KeyCode.E))
            {
                interactable.Interact(gameObject);
            }
        }
    }
}
```

---

## 6. Future-Proofing for Modules

The interface-based design makes transitioning to a module system seamless.

### Current: Monolithic Buildings

```csharp
public class ResourceDepot : Building, IResourceStorage
{
    // Implements IResourceStorage directly
}
```

### Future: Modular Buildings

```csharp
// Base module class
public abstract class BuildingModule : MonoBehaviour
{
    public ModularBuilding ParentBuilding { get; private set; }

    public void Initialize(ModularBuilding parent)
    {
        ParentBuilding = parent;
    }
}

// Storage module
public class StorageModule : BuildingModule, IResourceStorage
{
    // Implements IResourceStorage
    // Same interface as before!
}

// Modular building that holds modules
public class ModularBuilding : Building
{
    [SerializeField] private List<BuildingModule> modules;

    protected override void Awake()
    {
        base.Awake();

        foreach (var module in modules)
        {
            module.Initialize(this);
        }
    }
}
```

### Why This Works

**Consumer code never changes:**

```csharp
// This code works with BOTH monolithic and modular buildings!
var storage = BuildingRegistry.Instance.FindNearest<IResourceStorage>(position);
storage.TryDeposit(ResourceType.Wood, 50);
```

The consumer doesn't know (or care) if `IResourceStorage` is:
- A `ResourceDepot` (monolithic)
- A `StorageModule` attached to a `ModularBuilding`
- A `PlayerBackpack`
- A `CompanionStorage`

---

## 7. File Organization

```
Assets/
├── Scripts/
│   └── Buildings/
│       ├── Core/
│       │   ├── Building.cs              # Base MonoBehaviour
│       │   └── BuildingRegistry.cs      # Discovery system
│       ├── Data/
│       │   └── BuildingData.cs          # Base ScriptableObject
│       ├── Interfaces/
│       │   ├── IBuilding.cs             # Base building contract
│       │   └── IResourceStorage.cs      # Storage contract
│       └── Depot/
│           ├── ResourceDepot.cs         # Depot implementation
│           └── ResourceDepotData.cs     # Depot configuration
│
└── Data/
    └── Buildings/
        └── Depot/
            └── Resource Depot.asset     # ScriptableObject instance
```

---

## Quick Reference

| Component | Purpose |
|-----------|---------|
| `BuildingData` | ScriptableObject defining building configuration |
| `Building` | Base MonoBehaviour for all buildings |
| `BuildingRegistry` | Singleton for discovering buildings |
| `IBuilding` | Base interface for buildings |
| `IResourceStorage` | Interface for storage buildings |
| `ResourceDepot` | Concrete storage building |
| `ResourceDepotData` | Configuration for Resource Depot |

---

## Checklist for Adding New Buildings

- [ ] Create `[BuildingName]Data : BuildingData` ScriptableObject class
- [ ] Create `[BuildingName] : Building` MonoBehaviour class
- [ ] Implement relevant interfaces (`IResourceStorage`, `IManufacturing`, etc.)
- [ ] Create prefab with visual mesh, colliders, and components
- [ ] Create ScriptableObject asset with configuration
- [ ] Test registration with `BuildingRegistry`
- [ ] Test interaction and functionality
