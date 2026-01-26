# Companion & Building Systems Architecture Guide

> **StillOrbit** — Comprehensive architecture for Companion Resource Carriers and Building Placement/Interaction

---

## Table of Contents

1. [Design Principles](#1-design-principles)
2. [System Overview](#2-system-overview)
3. [Companion Resource Carrier System](#3-companion-resource-carrier-system)
4. [Building Placement System](#4-building-placement-system)
5. [Integration Points](#5-integration-points)
6. [Data Flow Diagrams](#6-data-flow-diagrams)
7. [Implementation Roadmap](#7-implementation-roadmap)
8. [Do's and Don'ts](#8-dos-and-donts)
9. [Extensibility Guide](#9-extensibility-guide)

---

## 1. Design Principles

These principles align with the existing codebase patterns established in `BuildingsSystemGuide.md` and the current implementation.

### 1.1 Core Tenets

| Principle | Application |
|-----------|-------------|
| **Interface-First** | Companion implements `IResourceHolder`, `IInteractable`. Systems interact via interfaces, not concrete types |
| **Data-Driven** | `CompanionData` (ScriptableObject) defines behavior parameters. No hardcoded values |
| **Composition over Inheritance** | Use components (state machine, navigation, inventory) rather than deep class hierarchies |
| **Registry Pattern** | Buildings self-register via `BuildingRegistry`. Companion queries registry to find targets |
| **Event-Driven** | Use events (`OnResourcesChanged`, `OnStorageChanged`) for loose coupling |

### 1.2 Existing Patterns to Leverage

```
┌─────────────────────────────────────────────────────────────────┐
│                    EXISTING INTERFACES                          │
├─────────────────────────────────────────────────────────────────┤
│  IResourceHolder     │ Player, Companion, (future: NPCs)        │
│  IResourceStorage    │ ResourceDepot, (future: storage modules) │
│  IInteractable       │ Buildings, WorldItem, Companion          │
│  IBuilding           │ All buildings                            │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 Resources vs Items: Decision

**Decision: Resources remain a parallel system to Items**

**Rationale:**
- Resources are **bulk quantities** (100 Wood, 50 Iron Ore) — optimized for storage/transfer
- Items are **discrete objects** with unique properties (weapons, armor, tools)
- Companion carries **resources only** (like Deep Rock Galactic's MULE)
- Different UI treatment: resources show totals, items show slots
- Performance: Dictionary-based `ResourceInventory` is O(1) for resource operations

**When to use Resources:**
- Harvested materials (wood, stone, ore)
- Crafting ingredients
- Bulk quantities

**When to use Items:**
- Equipment (weapons, tools, armor)
- Unique consumables
- Anything with individual state

---

## 2. System Overview

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              PLAYER LAYER                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  PlayerManager                                                               │
│  ├── PlayerInputHandler          (raw input)                                │
│  ├── PlayerInteractionController (interact with world)                      │
│  ├── PlayerResourceInventory     (implements IResourceHolder)               │
│  └── PlayerBuildController       (NEW: building placement mode)             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
┌─────────────────────────┐  ┌─────────────┐  ┌─────────────────────────────┐
│   COMPANION SYSTEM      │  │  BUILDING   │  │     BUILDING PLACEMENT      │
├─────────────────────────┤  │  REGISTRY   │  ├─────────────────────────────┤
│ CompanionController     │  │             │  │ BuildModeController         │
│ ├── CompanionStateMachine│ │ GetAll<T>() │  │ ├── BuildMenuUI             │
│ ├── CompanionInventory  │◄─┤ FindNearest │  │ ├── BuildingGhostController │
│ └── CompanionNavigation │  │ <T>()       │  │ └── BuildingPlacementValidator│
│                         │  │             │  │                             │
│ Implements:             │  └─────────────┘  │ Uses:                       │
│ • IResourceHolder       │        ▲          │ • BuildingData              │
│ • IInteractable         │        │          │ • BuildingRegistry          │
└─────────────────────────┘        │          └─────────────────────────────┘
            │                      │                      │
            ▼                      │                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                             BUILDINGS LAYER                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  ResourceDepot : Building, IResourceStorage, IInteractable                   │
│  (future: Factory, Generator, Fabricator, etc.)                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 System Interactions Summary

| From | To | Interaction |
|------|----|-------------|
| Player | Companion | Call companion, deposit resources |
| Companion | BuildingRegistry | Find nearest `IResourceStorage` |
| Companion | ResourceDepot | Deposit carried resources |
| Player | BuildModeController | Enter placement mode, select building |
| BuildModeController | BuildingGhost | Show placement preview |
| BuildModeController | World | Instantiate building at valid position |
| Player | Building | Interact to deposit resources |

---

## 3. Companion Resource Carrier System

### 3.1 Architectural Overview

The companion is a **stateful AI agent** that carries resources and autonomously deposits them. Inspired by Deep Rock Galactic's MULE.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         COMPANION ARCHITECTURE                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────┐    ┌─────────────────────┐                         │
│  │   CompanionData     │    │  CompanionController │                         │
│  │   (ScriptableObject)│───▶│   (MonoBehaviour)    │                         │
│  │                     │    │                      │                         │
│  │ • followDistance    │    │ • stateMachine       │                         │
│  │ • idleTimeout       │    │ • inventory          │                         │
│  │ • moveSpeed         │    │ • navAgent           │                         │
│  │ • callSFX/VFX       │    │ • animator           │                         │
│  └─────────────────────┘    └──────────┬───────────┘                         │
│                                        │                                     │
│                     ┌──────────────────┼──────────────────┐                  │
│                     ▼                  ▼                  ▼                  │
│           ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────┐   │
│           │ StateMachine    │ │ CompanionInventory│ │ CompanionNavigation│   │
│           │                 │ │                  │ │                     │   │
│           │ • Idle          │ │ Implements       │ │ • NavMeshAgent      │   │
│           │ • FollowPlayer  │ │ IResourceHolder  │ │ • SetDestination()  │   │
│           │ • MovingToDepot │ │                  │ │ • HasArrived()      │   │
│           │ • Depositing    │ │ Uses             │ │ • Stop()            │   │
│           │ • Returning     │ │ ResourceInventory│ │                     │   │
│           │ • BeingCalled   │ │                  │ │                     │   │
│           └─────────────────┘ └─────────────────┘ └─────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Core Components

#### 3.2.1 CompanionData (ScriptableObject)

Configuration for companion behavior. Allows tuning without code changes.

```csharp
// Assets/Scripts/Companion/CompanionData.cs

[CreateAssetMenu(fileName = "New Companion", menuName = "StillOrbit/Companion/Companion Data")]
public class CompanionData : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string companionName = "Resource Mule";
    [SerializeField] private Sprite icon;

    [BoxGroup("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float followDistance = 5f;          // Distance to maintain from player
    [SerializeField] private float arrivalThreshold = 1f;        // "Close enough" distance

    [BoxGroup("Behavior")]
    [SerializeField] private float idleTimeBeforeAutoDeposit = 5f; // Seconds before auto-travel
    [SerializeField] private float depositInteractionRange = 2f;   // Range to trigger deposit

    [BoxGroup("Spawn")]
    [SerializeField] private float spawnDistanceFromPlayer = 8f;
    [SerializeField] private float preferredAngleBehindPlayer = 135f; // Degrees behind player

    [BoxGroup("Effects")]
    [SerializeField] private AudioClip callResponseSFX;
    [SerializeField] private GameObject spawnVFX;

    // Public accessors
    public string CompanionName => companionName;
    public float MoveSpeed => moveSpeed;
    public float FollowDistance => followDistance;
    public float ArrivalThreshold => arrivalThreshold;
    public float IdleTimeBeforeAutoDeposit => idleTimeBeforeAutoDeposit;
    public float DepositInteractionRange => depositInteractionRange;
    public float SpawnDistanceFromPlayer => spawnDistanceFromPlayer;
    public float PreferredAngleBehindPlayer => preferredAngleBehindPlayer;
    public AudioClip CallResponseSFX => callResponseSFX;
    public GameObject SpawnVFX => spawnVFX;
}
```

#### 3.2.2 CompanionController (MonoBehaviour)

Main controller that coordinates state machine, navigation, and inventory.

```csharp
// Assets/Scripts/Companion/CompanionController.cs

[RequireComponent(typeof(NavMeshAgent))]
public class CompanionController : MonoBehaviour, IResourceHolder, IInteractable
{
    [SerializeField] private CompanionData data;

    // Components (injected or GetComponent)
    private CompanionStateMachine stateMachine;
    private CompanionInventory inventory;
    private NavMeshAgent navAgent;

    // Runtime state
    private Transform playerTransform;
    private float timeSinceLastInteraction;

    // IResourceHolder event
    public event Action<ResourceType, int> OnResourcesChanged;

    // Called by player input system
    public void OnPlayerCalled(Vector3 playerPosition, Vector3 playerForward) { }

    // State queries for external systems
    public bool IsCarryingResources => inventory.HasAnyResources();
    public bool IsIdle => stateMachine.CurrentState == CompanionState.Idle;
}
```

#### 3.2.3 CompanionInventory

Resource storage implementing `IResourceHolder`. Wraps `ResourceInventory`.

```csharp
// Assets/Scripts/Companion/CompanionInventory.cs

/// <summary>
/// Companion's resource storage. Implements IResourceHolder for compatibility
/// with existing resource transfer systems.
/// </summary>
public class CompanionInventory : MonoBehaviour, IResourceHolder
{
    [SerializeField] private ResourceInventory storage = new ResourceInventory();
    [SerializeField] private int maxTotalResources = 200; // Optional capacity limit

    public event Action<ResourceType, int> OnResourcesChanged;

    public int GetResourceAmount(ResourceType type) => storage.Get(type);

    public int AddResources(ResourceType type, int amount)
    {
        // Respect capacity limit if set
        int remaining = maxTotalResources - storage.GetTotalCount();
        int toAdd = Mathf.Min(amount, remaining);

        if (toAdd > 0)
        {
            storage.Add(type, toAdd);
            OnResourcesChanged?.Invoke(type, storage.Get(type));
        }
        return toAdd;
    }

    public bool RemoveResources(ResourceType type, int amount)
    {
        bool success = storage.TryRemove(type, amount);
        if (success)
        {
            OnResourcesChanged?.Invoke(type, storage.Get(type));
        }
        return success;
    }

    public bool HasResources(ResourceType type, int amount) => storage.Has(type, amount);

    public bool HasAnyResources() => storage.GetDistinctTypeCount() > 0;

    public IEnumerable<KeyValuePair<ResourceType, int>> GetAllResources() => storage.GetAll();

    public void Clear() => storage.Clear();
}
```

### 3.3 State Machine Design

The companion uses a simple state machine with clear transitions.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         COMPANION STATE MACHINE                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│                            ┌──────────────┐                                  │
│                   ┌────────│    IDLE      │◄────────────────────┐           │
│                   │        └──────┬───────┘                     │           │
│                   │               │                             │           │
│           Player called    Idle timeout &                Arrived at         │
│                   │        has resources                 player area        │
│                   ▼               ▼                             │           │
│           ┌──────────────┐ ┌─────────────────┐                  │           │
│           │ BEING_CALLED │ │ MOVING_TO_DEPOT │                  │           │
│           └──────┬───────┘ └────────┬────────┘                  │           │
│                  │                  │                           │           │
│           Arrived near       Arrived at depot                   │           │
│           player                    │                           │           │
│                  │                  ▼                           │           │
│                  │         ┌─────────────────┐                  │           │
│                  │         │   DEPOSITING    │                  │           │
│                  │         └────────┬────────┘                  │           │
│                  │                  │                           │           │
│                  │         Deposit complete                     │           │
│                  │                  │                           │           │
│                  │                  ▼                           │           │
│                  │         ┌─────────────────┐                  │           │
│                  └────────▶│   FOLLOW_PLAYER │──────────────────┘           │
│                            └─────────────────┘                              │
│                                                                              │
│  TRANSITIONS:                                                                │
│  • IDLE → BEING_CALLED: Player presses call button                          │
│  • IDLE → MOVING_TO_DEPOT: Idle timeout elapsed AND has resources           │
│  • BEING_CALLED → FOLLOW_PLAYER: NavAgent arrived near player                │
│  • FOLLOW_PLAYER → IDLE: Player stops moving, companion in range             │
│  • MOVING_TO_DEPOT → DEPOSITING: NavAgent arrived at depot                   │
│  • DEPOSITING → FOLLOW_PLAYER: All resources deposited                       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### State Descriptions

| State | Entry Condition | Behavior | Exit Condition |
|-------|-----------------|----------|----------------|
| **Idle** | Default / Returned to player | Stay still, watch for input | Player calls OR idle timeout with resources |
| **BeingCalled** | Player pressed call key | Calculate spawn point, navigate to player | Arrived near player |
| **FollowPlayer** | Completed call / completed deposit | Follow at `followDistance` behind player | Player stationary and close enough |
| **MovingToDepot** | Idle timeout elapsed + has resources | Query `BuildingRegistry.FindNearest<IResourceStorage>()`, navigate | Arrived at depot |
| **Depositing** | Arrived at depot | Transfer all resources to depot | Inventory empty |

### 3.4 Spawn Position Calculation

When called, companion spawns **outside the player's field of view** when possible.

```csharp
// In CompanionController.cs or CompanionSpawnHelper.cs

public Vector3 CalculateSpawnPosition(Vector3 playerPosition, Vector3 playerForward)
{
    float spawnDistance = data.SpawnDistanceFromPlayer;
    float angleOffset = data.PreferredAngleBehindPlayer; // e.g., 135 degrees

    // Try behind-left first, then behind-right, then sides, then front as fallback
    float[] anglesToTry = { angleOffset, -angleOffset, 90f, -90f, 45f, -45f, 0f };

    foreach (float angle in anglesToTry)
    {
        Vector3 direction = Quaternion.Euler(0, angle, 0) * -playerForward;
        Vector3 candidatePosition = playerPosition + direction * spawnDistance;

        // Validate position on NavMesh
        if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            // Check line of sight is NOT to player (spawn out of view)
            if (!IsInPlayerFieldOfView(hit.position, playerPosition, playerForward))
            {
                return hit.position;
            }
        }
    }

    // Fallback: just spawn behind player
    return playerPosition - playerForward * spawnDistance;
}

private bool IsInPlayerFieldOfView(Vector3 position, Vector3 playerPos, Vector3 playerForward)
{
    Vector3 toPosition = (position - playerPos).normalized;
    float dot = Vector3.Dot(playerForward, toPosition);
    return dot > 0.5f; // Roughly 60-degree half-angle cone
}
```

### 3.5 Resource Transfer Flow

#### Player → Companion

```csharp
// In PlayerInteractionController.cs (when interacting with companion)

private void HandleCompanionDeposit(CompanionController companion)
{
    var playerResources = PlayerManager.Instance.ResourceInventory;
    var companionInventory = companion.GetComponent<IResourceHolder>();

    // Transfer all player resources to companion
    foreach (var kvp in playerResources.GetInventory().GetAll().ToList())
    {
        ResourceType type = kvp.Key;
        int amount = kvp.Value;

        // How much can companion accept?
        int deposited = companionInventory.AddResources(type, amount);

        // Remove from player
        if (deposited > 0)
        {
            playerResources.RemoveResources(type, deposited);
            Debug.Log($"[Transfer] Player → Companion: {deposited}x {type}");
        }
    }
}
```

#### Companion → Building

```csharp
// In CompanionController.cs (Depositing state)

private void DepositToBuilding(IResourceStorage storage)
{
    foreach (var kvp in inventory.GetAllResources().ToList())
    {
        ResourceType type = kvp.Key;
        int amount = kvp.Value;

        if (!storage.CanAcceptResource(type))
            continue;

        int deposited = storage.TryDeposit(type, amount);

        if (deposited > 0)
        {
            inventory.RemoveResources(type, deposited);
            Debug.Log($"[Companion] Deposited {deposited}x {type} into depot");
        }
    }
}
```

### 3.6 IInteractable Implementation

The companion is interactable — players can deposit resources directly.

```csharp
// In CompanionController.cs

#region IInteractable Implementation

public string InteractionPrompt => IsCarryingResources
    ? $"Deposit Resources ({inventory.storage.GetTotalCount()} total)"
    : "Deposit Resources";

public bool CanInteract(GameObject interactor)
{
    // Can interact if companion is idle or following
    return stateMachine.CurrentState == CompanionState.Idle ||
           stateMachine.CurrentState == CompanionState.FollowPlayer;
}

public void Interact(GameObject interactor)
{
    var playerResources = interactor.GetComponent<IResourceHolder>()
                       ?? interactor.GetComponentInChildren<IResourceHolder>();

    if (playerResources == null)
    {
        Debug.LogWarning("[Companion] Interactor has no IResourceHolder");
        return;
    }

    TransferResourcesFrom(playerResources);
    ResetIdleTimer(); // Reset auto-deposit countdown
}

#endregion
```

### 3.7 File Organization

```
Assets/Scripts/Companion/
├── CompanionData.cs              # ScriptableObject configuration
├── CompanionController.cs        # Main controller, IResourceHolder, IInteractable
├── CompanionInventory.cs         # Resource storage wrapper
├── CompanionStateMachine.cs      # State machine logic
├── CompanionNavigation.cs        # NavMesh wrapper (optional, for cleaner code)
└── States/                       # Optional: separate state classes
    ├── IdleState.cs
    ├── FollowPlayerState.cs
    ├── MovingToDepotState.cs
    └── DepositingState.cs

Assets/Data/Companions/
└── Default Mule.asset            # CompanionData instance
```

---

## 4. Building Placement System

### 4.1 Architectural Overview

Building placement uses a **mode-based controller** that temporarily takes over camera and input.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      BUILDING PLACEMENT ARCHITECTURE                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────┐         ┌─────────────────────────────────────┐    │
│  │ PlayerInputHandler  │────────▶│      BuildModeController             │    │
│  │                     │  'B'    │                                      │    │
│  │ • BuildModeInput    │  key    │ Responsibilities:                    │    │
│  └─────────────────────┘         │ • Enter/exit build mode              │    │
│                                  │ • Manage camera transition           │    │
│                                  │ • Coordinate UI and ghost            │    │
│                                  │ • Handle placement confirmation      │    │
│                                  └──────────────┬────────────────────────┘    │
│                                                 │                            │
│               ┌─────────────────────────────────┼─────────────────────────┐  │
│               │                                 │                         │  │
│               ▼                                 ▼                         ▼  │
│   ┌───────────────────────┐    ┌───────────────────────┐   ┌─────────────────┐│
│   │    BuildMenuUI        │    │ BuildingGhostController│   │ PlacementValidator││
│   │                       │    │                        │   │                 ││
│   │ • Show building list  │    │ • Instantiate preview  │   │ • CheckCollisions││
│   │ • Handle selection    │    │ • Follow mouse/cursor  │   │ • CheckLayerMask││
│   │ • Display costs       │    │ • Show valid/invalid   │   │ • CheckNavMesh  ││
│   │ • Filter by resources │    │   material state       │   │ • CheckSlope    ││
│   └───────────────────────┘    └───────────────────────┘   └─────────────────┘│
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Mode Transitions

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                          BUILD MODE STATE FLOW                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│   ┌─────────────┐                                                             │
│   │   NORMAL    │  (Standard first-person gameplay)                          │
│   │   GAMEPLAY  │                                                             │
│   └──────┬──────┘                                                             │
│          │                                                                    │
│          │ Press 'B'                                                          │
│          ▼                                                                    │
│   ┌─────────────────────────────────────────────────────────────┐            │
│   │                      BUILD_MENU                              │            │
│   ├─────────────────────────────────────────────────────────────┤            │
│   │ • Player movement: DISABLED                                  │            │
│   │ • Camera: Normal FPS (or slight zoom out)                    │            │
│   │ • UI: Build menu visible                                     │            │
│   │ • Input: Navigate menu, select building                      │            │
│   └──────────────────────────┬──────────────────────────────────┘            │
│          │                   │                                               │
│   Press ESC            Select building                                       │
│          │                   │                                               │
│          ▼                   ▼                                               │
│   ┌──────────┐     ┌─────────────────────────────────────────────┐          │
│   │  NORMAL  │     │               PLACEMENT_MODE                 │          │
│   │ GAMEPLAY │     ├─────────────────────────────────────────────┤          │
│   └──────────┘     │ • Player movement: DISABLED                  │          │
│          ▲         │ • Camera: TOP-DOWN / RTS view                │          │
│          │         │ • Ghost: Follows mouse raycast               │          │
│          │         │ • Input: Rotate (R), Confirm (LMB), Cancel   │          │
│          │         └──────────────────────┬──────────────────────┘          │
│          │                                │                                  │
│          │         ┌──────────────────────┼──────────────────────┐          │
│          │         │                      │                      │          │
│          │   Press ESC              LMB (Invalid)          LMB (Valid)      │
│          │         │                      │                      │          │
│          │         ▼                      ▼                      ▼          │
│          │  ┌────────────┐         ┌────────────┐         ┌────────────┐   │
│          └──│ BUILD_MENU │         │   (beep)   │         │  CONFIRM   │   │
│             └────────────┘         │ Stay in    │         │  PLACEMENT │   │
│                                    │ placement  │         └─────┬──────┘   │
│                                    └────────────┘               │          │
│                                                                 │          │
│                                                          Instantiate       │
│                                                          building          │
│                                                                 │          │
│                                                                 ▼          │
│                                                          ┌────────────┐    │
│                                                          │   NORMAL   │    │
│                                                          │  GAMEPLAY  │    │
│                                                          └────────────┘    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.3 Core Components

#### 4.3.1 BuildModeController

Central coordinator for build mode. Manages state and delegates to subsystems.

```csharp
// Assets/Scripts/Building/Placement/BuildModeController.cs

public class BuildModeController : MonoBehaviour
{
    public enum BuildModeState
    {
        Inactive,
        MenuOpen,
        Placing
    }

    [BoxGroup("References")]
    [SerializeField] private BuildMenuUI menuUI;
    [SerializeField] private BuildingGhostController ghostController;
    [SerializeField] private BuildPlacementCamera placementCamera;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerLocomotionController locomotion;

    [BoxGroup("Settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer;

    [ShowInInspector, ReadOnly]
    private BuildModeState currentState = BuildModeState.Inactive;

    [ShowInInspector, ReadOnly]
    private BuildingData selectedBuilding;

    public bool IsInBuildMode => currentState != BuildModeState.Inactive;
    public BuildModeState CurrentState => currentState;

    public event Action<BuildModeState> OnStateChanged;
    public event Action<Building> OnBuildingPlaced;

    // Called by PlayerInputHandler when 'B' is pressed
    public void ToggleBuildMode()
    {
        if (currentState == BuildModeState.Inactive)
            EnterBuildMenu();
        else
            ExitBuildMode();
    }

    public void SelectBuilding(BuildingData building)
    {
        selectedBuilding = building;
        EnterPlacementMode();
    }

    private void EnterBuildMenu() { /* ... */ }
    private void EnterPlacementMode() { /* ... */ }
    private void ExitBuildMode() { /* ... */ }
    private void ConfirmPlacement() { /* ... */ }
}
```

#### 4.3.2 BuildingGhostController

Manages the placement preview (ghost) that follows the mouse.

```csharp
// Assets/Scripts/Building/Placement/BuildingGhostController.cs

public class BuildingGhostController : MonoBehaviour
{
    [BoxGroup("Settings")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private float rotationStep = 45f;

    [BoxGroup("References")]
    [SerializeField] private BuildPlacementValidator validator;

    private GameObject currentGhost;
    private BuildingData currentBuilding;
    private float currentRotation;
    private bool isValidPlacement;

    public bool IsValidPlacement => isValidPlacement;
    public Vector3 PlacementPosition => currentGhost?.transform.position ?? Vector3.zero;
    public Quaternion PlacementRotation => Quaternion.Euler(0, currentRotation, 0);

    public void ShowGhost(BuildingData building)
    {
        ClearGhost();

        if (building.BuildingPrefab == null) return;

        currentBuilding = building;
        currentGhost = Instantiate(building.BuildingPrefab);
        currentGhost.name = $"{building.BuildingName}_Ghost";

        // Disable all functional components, keep renderers
        DisableFunctionalComponents(currentGhost);

        // Set initial ghost material
        SetGhostMaterial(validPlacementMaterial);
    }

    public void UpdatePosition(Vector3 worldPosition)
    {
        if (currentGhost == null) return;

        currentGhost.transform.position = worldPosition;
        currentGhost.transform.rotation = PlacementRotation;

        // Validate placement
        isValidPlacement = validator.IsValidPlacement(
            worldPosition,
            PlacementRotation,
            currentBuilding
        );

        SetGhostMaterial(isValidPlacement ? validPlacementMaterial : invalidPlacementMaterial);
    }

    public void Rotate()
    {
        currentRotation = (currentRotation + rotationStep) % 360f;
        if (currentGhost != null)
        {
            currentGhost.transform.rotation = PlacementRotation;
        }
    }

    public void ClearGhost()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }
        currentBuilding = null;
        currentRotation = 0f;
    }

    private void DisableFunctionalComponents(GameObject obj)
    {
        // Disable Building, Colliders (non-trigger), NavMeshObstacle, etc.
        foreach (var building in obj.GetComponentsInChildren<Building>())
            building.enabled = false;

        foreach (var collider in obj.GetComponentsInChildren<Collider>())
            if (!collider.isTrigger) collider.enabled = false;

        // Keep renderers active for visual preview
    }

    private void SetGhostMaterial(Material mat)
    {
        foreach (var renderer in currentGhost.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[renderer.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            renderer.materials = mats;
        }
    }
}
```

#### 4.3.3 BuildPlacementValidator

Validates whether a position is suitable for building placement.

```csharp
// Assets/Scripts/Building/Placement/BuildPlacementValidator.cs

public class BuildPlacementValidator : MonoBehaviour
{
    [BoxGroup("Validation Rules")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float maxSlopeAngle = 30f;
    [SerializeField] private bool requireNavMesh = true;
    [SerializeField] private float navMeshSampleDistance = 2f;

    /// <summary>
    /// Check if a building can be placed at the given position.
    /// </summary>
    public bool IsValidPlacement(Vector3 position, Quaternion rotation, BuildingData building)
    {
        // 1. Check ground exists
        if (!IsOnGround(position))
            return false;

        // 2. Check slope
        if (!IsSlopeAcceptable(position))
            return false;

        // 3. Check obstacle collision
        if (HasObstacleCollision(position, rotation, building))
            return false;

        // 4. Check NavMesh (optional)
        if (requireNavMesh && !IsOnNavMesh(position))
            return false;

        // 5. Check resources (optional - could also be UI-only)
        // Skipped here; handled by BuildMenuUI

        return true;
    }

    private bool IsOnGround(Vector3 position)
    {
        return Physics.Raycast(position + Vector3.up * 5f, Vector3.down, 10f, groundLayers);
    }

    private bool IsSlopeAcceptable(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundLayers))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle <= maxSlopeAngle;
        }
        return false;
    }

    private bool HasObstacleCollision(Vector3 position, Quaternion rotation, BuildingData building)
    {
        // Use building's collider bounds for overlap check
        // Simplified: use a box check based on prefab renderer bounds

        var prefab = building.BuildingPrefab;
        if (prefab == null) return false;

        Bounds bounds = CalculatePrefabBounds(prefab);
        Vector3 center = position + rotation * bounds.center;
        Vector3 halfExtents = bounds.extents * 0.9f; // Slight margin

        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, obstacleLayers);
        return overlaps.Length > 0;
    }

    private bool IsOnNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out _, navMeshSampleDistance, NavMesh.AllAreas);
    }

    private Bounds CalculatePrefabBounds(GameObject prefab)
    {
        var renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        return bounds;
    }
}
```

#### 4.3.4 BuildPlacementCamera

Handles camera transition to top-down/RTS view during placement.

```csharp
// Assets/Scripts/Building/Placement/BuildPlacementCamera.cs

public class BuildPlacementCamera : MonoBehaviour
{
    [BoxGroup("Settings")]
    [SerializeField] private float placementHeight = 20f;
    [SerializeField] private float placementAngle = 60f; // Degrees from horizontal
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private float panSpeed = 20f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private Vector2 zoomRange = new Vector2(10f, 40f);

    [BoxGroup("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerCameraController playerCamera;

    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private bool isInPlacementMode;
    private float currentHeight;

    public void EnterPlacementMode(Vector3 playerPosition)
    {
        // Save current camera state
        savedCameraPosition = mainCamera.transform.position;
        savedCameraRotation = mainCamera.transform.rotation;

        // Disable normal camera control
        playerCamera.enabled = false;

        // Move to placement view
        currentHeight = placementHeight;
        Vector3 targetPos = playerPosition + Vector3.up * currentHeight;
        Quaternion targetRot = Quaternion.Euler(placementAngle, 0, 0);

        StartCoroutine(TransitionCamera(targetPos, targetRot));
        isInPlacementMode = true;
    }

    public void ExitPlacementMode()
    {
        isInPlacementMode = false;

        // Restore camera
        StartCoroutine(TransitionCamera(savedCameraPosition, savedCameraRotation, () =>
        {
            playerCamera.enabled = true;
        }));
    }

    private void Update()
    {
        if (!isInPlacementMode) return;

        // Pan camera with WASD or arrow keys
        Vector3 pan = new Vector3(
            Input.GetAxis("Horizontal"),
            0,
            Input.GetAxis("Vertical")
        ) * panSpeed * Time.deltaTime;

        mainCamera.transform.position += pan;

        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            currentHeight = Mathf.Clamp(currentHeight - scroll * zoomSpeed, zoomRange.x, zoomRange.y);
            Vector3 pos = mainCamera.transform.position;
            pos.y = currentHeight;
            mainCamera.transform.position = pos;
        }
    }

    /// <summary>
    /// Get world position from screen point for ghost placement.
    /// </summary>
    public bool TryGetGroundPosition(Vector2 screenPos, LayerMask groundLayer, out Vector3 worldPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            worldPos = hit.point;
            return true;
        }
        worldPos = Vector3.zero;
        return false;
    }

    private IEnumerator TransitionCamera(Vector3 targetPos, Quaternion targetRot, Action onComplete = null)
    {
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            t = t * t * (3f - 2f * t); // Smoothstep

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        onComplete?.Invoke();
    }
}
```

### 4.4 Building Placement Flow (Detailed)

```
1. Player presses 'B'
   └─► BuildModeController.ToggleBuildMode()
       └─► EnterBuildMenu()
           ├─► Disable PlayerLocomotionController
           ├─► Show BuildMenuUI
           └─► Set state = MenuOpen

2. Player selects building from menu
   └─► BuildMenuUI.OnBuildingSelected(BuildingData)
       └─► BuildModeController.SelectBuilding(building)
           └─► EnterPlacementMode()
               ├─► BuildPlacementCamera.EnterPlacementMode()
               ├─► BuildingGhostController.ShowGhost(building)
               └─► Set state = Placing

3. Every frame in Placing state:
   └─► BuildModeController.Update()
       ├─► Get mouse position → BuildPlacementCamera.TryGetGroundPosition()
       ├─► BuildingGhostController.UpdatePosition(worldPos)
       ├─► If 'R' pressed → ghostController.Rotate()
       ├─► If ESC pressed → ExitBuildMode() or EnterBuildMenu()
       └─► If LMB pressed:
           └─► If ghostController.IsValidPlacement:
               └─► ConfirmPlacement()
                   ├─► Instantiate(building.BuildingPrefab, pos, rot)
                   ├─► Deduct resources from player
                   ├─► OnBuildingPlaced?.Invoke(newBuilding)
                   └─► ExitBuildMode()
```

### 4.5 Build Menu UI

Simple grid of available buildings with cost display.

```csharp
// Assets/Scripts/UI/Building/BuildMenuUI.cs

public class BuildMenuUI : MonoBehaviour
{
    [SerializeField] private Transform buildingGridParent;
    [SerializeField] private BuildingSlotUI slotPrefab;
    [SerializeField] private BuildingDatabase buildingDatabase;

    public event Action<BuildingData> OnBuildingSelected;

    private List<BuildingSlotUI> slots = new List<BuildingSlotUI>();

    public void Show()
    {
        gameObject.SetActive(true);
        RefreshSlots();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RefreshSlots()
    {
        // Clear existing
        foreach (var slot in slots)
            Destroy(slot.gameObject);
        slots.Clear();

        // Create slots for each building
        foreach (var building in buildingDatabase.AllBuildings)
        {
            var slot = Instantiate(slotPrefab, buildingGridParent);
            slot.Setup(building, OnSlotClicked);
            slot.UpdateAffordability(CanAfford(building));
            slots.Add(slot);
        }
    }

    private bool CanAfford(BuildingData building)
    {
        var playerResources = PlayerManager.Instance.ResourceInventory;

        foreach (var cost in building.ConstructionCosts)
        {
            if (!playerResources.HasResources(cost.resourceType, cost.amount))
                return false;
        }
        return true;
    }

    private void OnSlotClicked(BuildingData building)
    {
        if (CanAfford(building))
        {
            OnBuildingSelected?.Invoke(building);
        }
    }
}
```

### 4.6 File Organization

```
Assets/Scripts/Building/
├── Core/
│   ├── Building.cs                    # (existing)
│   ├── BuildingRegistry.cs            # (existing)
│   └── BuildingData.cs                # (existing)
├── Interfaces/
│   ├── IBuilding.cs                   # (existing)
│   └── IResourceStorage.cs            # (existing)
├── Depot/
│   ├── ResourceDepot.cs               # (existing)
│   └── ResourceDepotData.cs           # (existing)
└── Placement/
    ├── BuildModeController.cs         # Coordinator
    ├── BuildingGhostController.cs     # Preview management
    ├── BuildPlacementValidator.cs     # Validation rules
    ├── BuildPlacementCamera.cs        # Camera control
    └── BuildingDatabase.cs            # ScriptableObject registry

Assets/Scripts/UI/Building/
├── BuildMenuUI.cs                     # Main menu panel
├── BuildingSlotUI.cs                  # Individual building button
└── BuildingTooltipUI.cs               # Hover tooltip

Assets/Data/Buildings/
├── BuildingDatabase.asset             # List of all buildings
├── Depot/
│   └── Resource Depot.asset
└── (future buildings...)
```

---

## 5. Integration Points

### 5.1 Existing Systems Integration

| New Component | Integrates With | How |
|---------------|-----------------|-----|
| `CompanionController` | `PlayerInteractionController` | Companion implements `IInteractable` |
| `CompanionInventory` | `ResourceInventory` / `IResourceHolder` | Uses same storage class and interface |
| `CompanionController` | `BuildingRegistry` | Queries `FindNearest<IResourceStorage>()` |
| `BuildModeController` | `PlayerInputHandler` | Adds `BuildModeInput` action |
| `BuildModeController` | `PlayerLocomotionController` | Disables during build mode |
| `BuildingGhostController` | `BuildingData` | Uses prefab and bounds |
| Building placement | `PlayerResourceInventory` | Deducts costs on placement |

### 5.2 New Input Actions

Add to `PlayerInputHandler`:

```csharp
// In PlayerInputHandler.cs

[BoxGroup("Build Mode")]
public bool BuildModeInput { get; private set; }      // 'B' key

[BoxGroup("Companion")]
public bool CallCompanionInput { get; private set; } // 'C' key

private void Update()
{
    // ... existing input ...

    BuildModeInput = Input.GetKeyDown(KeyCode.B);
    CallCompanionInput = Input.GetKeyDown(KeyCode.C);
}
```

### 5.3 PlayerManager Integration

```csharp
// In PlayerManager.cs

[BoxGroup("References")]
[SerializeField] private CompanionController companion;
[SerializeField] private BuildModeController buildMode;

public CompanionController Companion => companion;
public BuildModeController BuildMode => buildMode;

private void Update()
{
    // Handle companion call
    if (inputHandler.CallCompanionInput && companion != null)
    {
        companion.OnPlayerCalled(transform.position, transform.forward);
    }

    // Handle build mode toggle
    if (inputHandler.BuildModeInput && buildMode != null)
    {
        buildMode.ToggleBuildMode();
    }
}
```

---

## 6. Data Flow Diagrams

### 6.1 Resource Harvesting → Companion → Depot

```
┌─────────────┐   destroy    ┌─────────────────┐   add      ┌─────────────────┐
│ResourceNode │─────────────►│IResourceHolder  │◄───────────│     Player      │
│             │              │(Player)         │   interact │                 │
└─────────────┘              └────────┬────────┘            └─────────────────┘
                                      │                              │
                                      │ transfer                     │ call
                                      ▼                              ▼
                             ┌─────────────────┐            ┌─────────────────┐
                             │IResourceHolder  │◄───────────│   Companion     │
                             │(Companion)      │   nav to   │   Controller    │
                             └────────┬────────┘   player   └─────────────────┘
                                      │
                          idle timeout│+ has resources
                                      ▼
                             ┌─────────────────┐
                             │BuildingRegistry │
                             │.FindNearest<    │
                             │ IResourceStorage>│
                             └────────┬────────┘
                                      │
                                      ▼
                             ┌─────────────────┐   deposit  ┌─────────────────┐
                             │IResourceStorage │◄───────────│ResourceDepot    │
                             │(Building)       │            │                 │
                             └─────────────────┘            └─────────────────┘
```

### 6.2 Building Placement Flow

```
┌─────────────┐   'B'     ┌─────────────────┐
│   Player    │──────────►│BuildModeController│
│   Input     │           │.ToggleBuildMode()│
└─────────────┘           └────────┬─────────┘
                                   │
                                   ▼
                         ┌─────────────────────┐
                         │    BuildMenuUI      │
                         │    .Show()          │
                         └──────────┬──────────┘
                                    │ select
                                    ▼
                         ┌─────────────────────┐
                         │BuildingGhostController│
                         │.ShowGhost(data)     │
                         └──────────┬──────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │                     │                     │
              ▼                     ▼                     ▼
    ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
    │BuildPlacementCamera│ │UpdatePosition() │   │PlacementValidator│
    │.TryGetGroundPos()│   │(every frame)    │   │.IsValidPlacement()│
    └─────────────────┘   └─────────────────┘   └─────────────────┘
                                    │
                            LMB + valid
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │ Instantiate Prefab  │
                         │ Deduct Resources    │
                         │ Register Building   │
                         └─────────────────────┘
```

---

## 7. Implementation Roadmap

### Phase 1: Foundation (Companion Core)

1. Create `CompanionData` ScriptableObject
2. Implement `CompanionInventory` (IResourceHolder wrapper)
3. Create basic `CompanionController` with NavMeshAgent
4. Implement `IInteractable` on companion
5. Test: Player can interact and deposit resources

### Phase 2: Companion AI

1. Implement `CompanionStateMachine` with Idle and FollowPlayer states
2. Add spawn position calculation
3. Integrate with `PlayerInputHandler` (call key)
4. Add BeingCalled state with navigation
5. Test: Companion spawns and follows player when called

### Phase 3: Autonomous Behavior

1. Add idle timeout timer
2. Integrate `BuildingRegistry.FindNearest<IResourceStorage>()`
3. Implement MovingToDepot and Depositing states
4. Test: Companion auto-deposits after idle timeout

### Phase 4: Building Placement Foundation

1. Create `BuildModeController` skeleton
2. Implement `BuildMenuUI` with building grid
3. Create `BuildingDatabase` ScriptableObject
4. Add build mode input to `PlayerInputHandler`
5. Test: Can open/close build menu

### Phase 5: Placement System

1. Implement `BuildingGhostController`
2. Create valid/invalid placement materials
3. Implement `BuildPlacementValidator`
4. Test: Ghost follows mouse with validation feedback

### Phase 6: Camera & Finalization

1. Implement `BuildPlacementCamera` with transitions
2. Add rotation controls
3. Implement resource deduction on placement
4. Polish transitions and feedback
5. Test: Full placement flow end-to-end

---

## 8. Do's and Don'ts

### Do's

| Practice | Reason |
|----------|--------|
| **Query by interface** (`IResourceStorage`, not `ResourceDepot`) | Allows swapping implementations |
| **Use `BuildingRegistry` for discovery** | Decouples companion from specific buildings |
| **Fire events on state changes** | UI and other systems can react without polling |
| **Configure behavior via ScriptableObjects** | Designers can tune without code changes |
| **Validate NavMesh positions** | Companion won't get stuck |
| **Disable components on ghost, not destroy** | Consistent preview without side effects |
| **Use squared distance for comparisons** | Avoids sqrt, better performance |
| **Reset idle timer on player interaction** | Intuitive auto-deposit behavior |

### Don'ts

| Anti-Pattern | Why It's Bad |
|--------------|--------------|
| **Hardcoding building references** | Breaks when buildings added/removed |
| **Checking concrete types** (`if (building is ResourceDepot)`) | Defeats interface abstraction |
| **Companion directly modifying player inventory** | Violates single responsibility |
| **Skipping NavMesh validation** | Companion will clip through terrain |
| **Polling for state in Update** | Use events instead |
| **Placing buildings with zero resource cost** | Breaks progression; check affordability |
| **Tight coupling between Companion and specific Building types** | Query by `IResourceStorage` instead |
| **Spawning companion at exact player position** | Looks jarring; spawn out of view |

---

## 9. Extensibility Guide

### 9.1 Adding New Buildings

1. Create `[BuildingName]Data : BuildingData` if custom config needed
2. Create `[BuildingName] : Building` MonoBehaviour
3. Implement relevant interfaces (`IResourceStorage`, `IManufacturing`, etc.)
4. Create prefab with components
5. Add to `BuildingDatabase` asset
6. **No code changes to placement system required**

### 9.2 Adding New Resource Types

1. Add entry to `ResourceType` enum
2. Add metadata to `ResourceDatabase`
3. Configure `ResourceDepotData.AcceptedResources` as needed
4. **No code changes to companion or transfer logic**

### 9.3 Multiple Companions

```csharp
// In PlayerManager or CompanionManager

[SerializeField] private List<CompanionController> companions;

public void CallAllCompanions()
{
    foreach (var companion in companions)
    {
        companion.OnPlayerCalled(transform.position, transform.forward);
    }
}

public CompanionController GetNearestCompanion(Vector3 position)
{
    return companions
        .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - position))
        .FirstOrDefault();
}
```

### 9.4 Custom Validation Rules

Extend `BuildPlacementValidator`:

```csharp
public class ExtendedPlacementValidator : BuildPlacementValidator
{
    [SerializeField] private float minDistanceFromOtherBuildings = 5f;

    public override bool IsValidPlacement(Vector3 position, Quaternion rotation, BuildingData building)
    {
        if (!base.IsValidPlacement(position, rotation, building))
            return false;

        // Custom: Check distance from other buildings
        var nearbyBuildings = BuildingRegistry.Instance.FindWithinRadius<IBuilding>(
            position,
            minDistanceFromOtherBuildings
        );

        return nearbyBuildings.Count == 0;
    }
}
```

### 9.5 Alternative Deposit Targets

If you want companion to deposit to non-building storage:

```csharp
// Any MonoBehaviour implementing IResourceStorage becomes a valid target

public class PortableStorageContainer : MonoBehaviour, IResourceStorage
{
    // Companion's FindNearest<IResourceStorage>() will find this too
}
```

---

## Quick Reference

### Interfaces

| Interface | Purpose | Key Methods |
|-----------|---------|-------------|
| `IResourceHolder` | Any entity carrying resources | `AddResources()`, `RemoveResources()`, `HasResources()` |
| `IResourceStorage` | Buildings that store resources | `TryDeposit()`, `TryWithdraw()`, `CanAcceptResource()` |
| `IInteractable` | World objects player can interact with | `Interact()`, `CanInteract()`, `InteractionPrompt` |
| `IBuilding` | Base building contract | `Data`, `Transform`, `Health`, `IsOperational` |

### Key Components

| Component | File | Purpose |
|-----------|------|---------|
| `CompanionController` | `Companion/CompanionController.cs` | Main companion logic |
| `CompanionInventory` | `Companion/CompanionInventory.cs` | Resource storage |
| `BuildModeController` | `Building/Placement/BuildModeController.cs` | Placement coordinator |
| `BuildingGhostController` | `Building/Placement/BuildingGhostController.cs` | Preview management |
| `BuildPlacementValidator` | `Building/Placement/BuildPlacementValidator.cs` | Placement rules |
| `BuildPlacementCamera` | `Building/Placement/BuildPlacementCamera.cs` | Camera control |

### Companion States

| State | When | Behavior |
|-------|------|----------|
| `Idle` | Default | Stationary, watching |
| `BeingCalled` | Player pressed call | Navigate to player |
| `FollowPlayer` | Near player, active | Maintain follow distance |
| `MovingToDepot` | Idle timeout + resources | Navigate to nearest storage |
| `Depositing` | At depot | Transfer all resources |

---

## Checklist: New Companion

- [ ] Create `CompanionData` asset
- [ ] Create prefab with NavMeshAgent
- [ ] Add `CompanionController` component
- [ ] Add `CompanionInventory` component
- [ ] Configure movement and behavior values
- [ ] Add to scene or spawn system
- [ ] Test call/follow behavior
- [ ] Test resource deposit
- [ ] Test auto-deposit to depot

## Checklist: New Building Type

- [ ] Create `[Name]Data : BuildingData` if needed
- [ ] Create `[Name] : Building` MonoBehaviour
- [ ] Implement relevant interfaces
- [ ] Create prefab with mesh, colliders, HealthComponent
- [ ] Create ScriptableObject asset
- [ ] Add to BuildingDatabase
- [ ] Test placement
- [ ] Test interaction
- [ ] Test destruction

---

*Last updated: Current implementation based on existing `BuildingsSystemGuide.md` patterns*
