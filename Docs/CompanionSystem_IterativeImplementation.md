# Companion System — Iterative Implementation Guide

> **StillOrbit** — Step-by-step implementation playbook for the Resource Companion System

---

## Document Purpose

This document answers:

> "What exactly do I build first, then second, then third — and how do I test each step — until the companion system is complete and integrated with buildings and resources?"

This is **not** a theory document. It is an **execution playbook**.

**Reference architecture:** Inspired by Deep Rock Galactic's "Molly" resource mule.

**Related document:** `BuildingSystem_IterativeImplementation.md`

---

## Table of Contents

1. [Phase 0 — Prerequisites & Assumptions](#phase-0--prerequisites--assumptions)
2. [Phase 1 — Companion Data (ScriptableObject)](#phase-1--companion-data-scriptableobject)
3. [Phase 2 — Companion Core Controller](#phase-2--companion-core-controller)
4. [Phase 3 — Companion Inventory & Resource Handling](#phase-3--companion-inventory--resource-handling)
5. [Phase 4 — Companion State Machine](#phase-4--companion-state-machine)
6. [Phase 5 — Navigation & Movement](#phase-5--navigation--movement)
7. [Phase 6 — Player Interaction (Calling & Depositing)](#phase-6--player-interaction-calling--depositing)
8. [Phase 7 — Auto-Deposit Logic & Timers](#phase-7--auto-deposit-logic--timers)
9. [Phase 8 — Integration with BuildingRegistry & Resource Buildings](#phase-8--integration-with-buildingregistry--resource-buildings)
10. [Phase 9 — Validation, Debugging Tools & Common Failure Cases](#phase-9--validation-debugging-tools--common-failure-cases)
11. [Phase 10 — Cleanup, Extension Hooks & Future-Proofing](#phase-10--cleanup-extension-hooks--future-proofing)
12. [Common Mistakes & Anti-Patterns](#common-mistakes--anti-patterns)

---

## Core Behavior Summary

Before implementing, understand the companion's behavior model:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         COMPANION LIFECYCLE                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   [Player Calls]                                                            │
│        │                                                                    │
│        ▼                                                                    │
│   ┌─────────────┐      ┌──────────────┐      ┌─────────────────┐           │
│   │   IDLE /    │─────►│   BEING      │─────►│   FOLLOWING     │           │
│   │  AMBIENT    │      │   CALLED     │      │    PLAYER       │           │
│   └─────────────┘      └──────────────┘      └────────┬────────┘           │
│         ▲                                             │                     │
│         │                                             │ [Player Deposits]   │
│         │                                             ▼                     │
│   ┌─────────────┐      ┌──────────────┐      ┌─────────────────┐           │
│   │  RETURNING  │◄─────│  DEPOSITING  │◄─────│  MOVING TO      │           │
│   │  TO PLAYER  │      │  AT DEPOT    │      │   DEPOT         │           │
│   └─────────────┘      └──────────────┘      └─────────────────┘           │
│                                                       ▲                     │
│                                              [Idle Timer Expires]           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Existing Code Reference

Before starting, confirm these files exist and understand their contracts:

| File | Purpose | Location |
|------|---------|----------|
| `IResourceStorage.cs` | Storage interface for depots | `Assets/Scripts/Buildings/Interfaces/` |
| `IResourceHolder.cs` | Resource holder abstraction | `Assets/Scripts/Resources/` |
| `IInteractable.cs` | Player interaction contract | `Assets/Scripts/Interactions/` |
| `ResourceInventory.cs` | Bulk resource storage class | `Assets/Scripts/Resources/` |
| `ResourceType.cs` | Resource type enum | `Assets/Scripts/Resources/` |
| `BuildingRegistry.cs` | Building discovery service | `Assets/Scripts/Buildings/Core/` |
| `PlayerManager.cs` | Player singleton | `Assets/Scripts/Player/` |
| `PlayerResourceInventory.cs` | Player resources | `Assets/Scripts/Resources/` |
| `PlayerInteractionController.cs` | Interaction handling | `Assets/Scripts/Player/` |

---

## Phase 0 — Prerequisites & Assumptions

### Goal
Ensure the project has the correct folder structure, NavMesh setup, and dependencies before writing any new code.

### What is Implemented
- Folder structure for companion scripts
- NavMesh configuration verified
- Input action placeholder for calling companion
- Companion prefab placeholder

### What is Intentionally Deferred
- Actual companion logic
- Visual model
- Animations

### Steps

#### Step 0.1 — Create Folder Structure

Create the following folders if they don't exist:

```
Assets/Scripts/Companion/
Assets/Scripts/Companion/Data/
Assets/Scripts/Companion/States/
Assets/Data/Companion/
Assets/Prefabs/Companion/
```

#### Step 0.2 — Verify NavMesh Setup

1. Open your gameplay scene
2. In the Navigation window (Window > AI > Navigation), verify:
   - Ground surfaces are marked as walkable
   - NavMesh is baked and visible (blue overlay in Scene view)
3. If not baked, select terrain/ground objects and bake navigation

**Test:** Create a temporary NavMeshAgent object, enter Play Mode, call `agent.SetDestination()` — it should path correctly.

#### Step 0.3 — Add Input Action for Calling

Open your `PlayerControls.inputactions` asset and add:

| Action Name | Type | Binding |
|-------------|------|---------|
| `CallCompanion` | Button | `C` |

**Regenerate the C# class** after saving.

> **Assumption:** The project uses Unity's Input System. If using legacy input, you'll handle this in `Update()` with `Input.GetKeyDown(KeyCode.C)`.

#### Step 0.4 — Create Placeholder Companion Prefab

1. Create a new empty GameObject named `Companion`
2. Add a **Capsule** as a child (visual placeholder)
3. Add **NavMeshAgent** component to root:
   - Speed: 5
   - Angular Speed: 360
   - Acceleration: 8
   - Stopping Distance: 1.5
4. Add a **Sphere Collider** (trigger) for interaction:
   - Radius: 2
   - Is Trigger: true
5. Save as prefab in `Assets/Prefabs/Companion/Companion.prefab`

#### Step 0.5 — Verify Existing Interfaces Compile

Open each file and confirm no compile errors:

- `IResourceStorage.cs` — Should have `TryDeposit()`, `CanAcceptResource()`, `GetRemainingCapacity()`
- `IResourceHolder.cs` — Should have `AddResources()`, `RemoveResources()`, `HasResources()`
- `IInteractable.cs` — Should have `Interact()`, `CanInteract()`, `InteractionPrompt`

### ✅ Validation Checklist

- [ ] Folder structure exists
- [ ] NavMesh is baked and working
- [ ] Input action added (or noted for legacy input)
- [ ] Placeholder prefab created with NavMeshAgent
- [ ] All existing interfaces compile

### What "Done" Looks Like

You can see the folders in the Project window, NavMesh in Scene view, and a basic capsule prefab exists. No runtime behavior yet.

---

## Phase 1 — Companion Data (ScriptableObject)

### Goal
Create a data-driven configuration asset for the companion's behavior parameters.

### What is Implemented
- `CompanionData.cs` ScriptableObject
- One data asset with default values

### What is Intentionally Deferred
- Multiple companion types
- Upgrades/progression

### Steps

#### Step 1.1 — Create CompanionData.cs

Create `Assets/Scripts/Companion/Data/CompanionData.cs`:

```csharp
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Configuration data for companion behavior.
/// All tunable parameters live here, not hardcoded in scripts.
/// </summary>
[CreateAssetMenu(fileName = "CompanionData", menuName = "StillOrbit/Companion/Companion Data")]
public class CompanionData : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string companionId = "default_companion";

    [BoxGroup("Identity")]
    [SerializeField] private string displayName = "Resource Mule";

    [BoxGroup("Movement")]
    [Tooltip("Movement speed when following or navigating")]
    [SerializeField] private float moveSpeed = 5f;

    [BoxGroup("Movement")]
    [Tooltip("How close the companion must get to consider arrival")]
    [SerializeField] private float arrivalDistance = 1.5f;

    [BoxGroup("Movement")]
    [Tooltip("Distance to maintain when following player")]
    [SerializeField] private float followDistance = 3f;

    [BoxGroup("Calling")]
    [Tooltip("Minimum spawn distance from player when called")]
    [SerializeField] private float minSpawnDistance = 5f;

    [BoxGroup("Calling")]
    [Tooltip("Maximum spawn distance from player when called")]
    [SerializeField] private float maxSpawnDistance = 10f;

    [BoxGroup("Calling")]
    [Tooltip("Prefer spawning behind player (outside field of view)")]
    [SerializeField] private bool preferSpawnBehindPlayer = true;

    [BoxGroup("Calling")]
    [Tooltip("Angle range behind player to try spawning (degrees)")]
    [SerializeField] private float behindPlayerAngleRange = 120f;

    [BoxGroup("Auto-Deposit")]
    [Tooltip("Seconds of no interaction before auto-deposit triggers")]
    [SerializeField] private float idleTimeBeforeAutoDeposit = 5f;

    [BoxGroup("Auto-Deposit")]
    [Tooltip("Maximum distance to search for deposit targets")]
    [SerializeField] private float depotSearchRadius = 50f;

    [BoxGroup("Inventory")]
    [Tooltip("Resource types the companion can carry (empty = all)")]
    [SerializeField] private List<ResourceType> acceptedResourceTypes = new List<ResourceType>();

    [BoxGroup("Inventory")]
    [Tooltip("If true, accepts all resource types regardless of list")]
    [SerializeField] private bool acceptAllResources = true;

    [BoxGroup("Interaction")]
    [Tooltip("Prompt shown when player can interact")]
    [SerializeField] private string interactionPrompt = "Deposit Resources";

    [BoxGroup("Interaction")]
    [Tooltip("Distance within which player can interact")]
    [SerializeField] private float interactionRange = 3f;

    // Public Accessors
    public string CompanionId => companionId;
    public string DisplayName => displayName;
    public float MoveSpeed => moveSpeed;
    public float ArrivalDistance => arrivalDistance;
    public float FollowDistance => followDistance;
    public float MinSpawnDistance => minSpawnDistance;
    public float MaxSpawnDistance => maxSpawnDistance;
    public bool PreferSpawnBehindPlayer => preferSpawnBehindPlayer;
    public float BehindPlayerAngleRange => behindPlayerAngleRange;
    public float IdleTimeBeforeAutoDeposit => idleTimeBeforeAutoDeposit;
    public float DepotSearchRadius => depotSearchRadius;
    public IReadOnlyList<ResourceType> AcceptedResourceTypes => acceptedResourceTypes;
    public bool AcceptAllResources => acceptAllResources;
    public string InteractionPrompt => interactionPrompt;
    public float InteractionRange => interactionRange;

    /// <summary>
    /// Check if the companion can carry a specific resource type.
    /// </summary>
    public bool CanAcceptResource(ResourceType resourceType)
    {
        if (acceptAllResources) return true;
        if (resourceType == ResourceType.None) return false;

        return acceptedResourceTypes.Contains(resourceType);
    }

#if UNITY_EDITOR
    [Button("Validate Data"), BoxGroup("Debug")]
    private void ValidateData()
    {
        bool valid = true;

        if (string.IsNullOrEmpty(companionId))
        {
            Debug.LogWarning("[CompanionData] Companion ID is empty");
            valid = false;
        }

        if (minSpawnDistance >= maxSpawnDistance)
        {
            Debug.LogWarning("[CompanionData] Min spawn distance should be less than max");
            valid = false;
        }

        if (idleTimeBeforeAutoDeposit <= 0)
        {
            Debug.LogWarning("[CompanionData] Idle time should be positive");
            valid = false;
        }

        if (valid)
        {
            Debug.Log("[CompanionData] Validation passed!");
        }
    }
#endif
}
```

#### Step 1.2 — Create the Data Asset

1. Right-click in `Assets/Data/Companion/`
2. Select **Create > StillOrbit > Companion > Companion Data**
3. Name it `DefaultCompanionData.asset`
4. Configure initial values (defaults are sensible)

#### Step 1.3 — Assign to Prefab (Later)

We'll wire this to the companion prefab in Phase 2. For now, just ensure the asset exists.

### ✅ Validation Checklist

- [ ] `CompanionData.cs` compiles without errors
- [ ] `DefaultCompanionData.asset` exists in `Assets/Data/Companion/`
- [ ] All public accessors work (test in Inspector)
- [ ] `CanAcceptResource()` returns expected values

### What "Done" Looks Like

You have a ScriptableObject asset that configures all companion behavior. Values can be tweaked in the Inspector without code changes.

---

## Phase 2 — Companion Core Controller

### Goal
Create the main MonoBehaviour that coordinates all companion subsystems. This phase implements only the skeleton — no state machine, no navigation yet.

### What is Implemented
- `CompanionController.cs` — Central coordinator
- Basic component references
- Spawn/despawn methods
- Integration with `CompanionData`

### What is Intentionally Deferred
- State machine
- Navigation
- Interaction handling

### Steps

#### Step 2.1 — Create CompanionController.cs

Create `Assets/Scripts/Companion/CompanionController.cs`:

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Central coordinator for companion behavior.
/// Manages state transitions and delegates to subsystems.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CompanionController : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [Required]
    [SerializeField] private CompanionData companionData;

    [BoxGroup("References")]
    [SerializeField] private Transform visualRoot;

    [BoxGroup("References")]
    [SerializeField] private Collider interactionCollider;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isActive = false;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Transform playerTransform;

    // Components (cached)
    private NavMeshAgent navAgent;

    // Events
    public event Action OnCompanionActivated;
    public event Action OnCompanionDeactivated;
    public event Action<Vector3> OnCompanionMoved;

    // Public Accessors
    public CompanionData Data => companionData;
    public bool IsActive => isActive;
    public Transform PlayerTransform => playerTransform;
    public NavMeshAgent NavAgent => navAgent;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
        {
            Debug.LogError("[CompanionController] NavMeshAgent component required!");
        }

        // Apply data settings to NavMeshAgent
        if (companionData != null && navAgent != null)
        {
            navAgent.speed = companionData.MoveSpeed;
            navAgent.stoppingDistance = companionData.ArrivalDistance;
        }
    }

    private void Start()
    {
        // Find player
        playerTransform = PlayerManager.Instance?.transform;

        if (playerTransform == null)
        {
            Debug.LogWarning("[CompanionController] PlayerManager not found. Will retry on activation.");
        }

        // Start inactive (can be changed based on game design)
        SetActive(false);
    }

    /// <summary>
    /// Activate the companion. Called when spawned or enabled.
    /// </summary>
    public void Activate()
    {
        if (isActive) return;

        // Retry finding player if needed
        if (playerTransform == null)
        {
            playerTransform = PlayerManager.Instance?.transform;
        }

        if (playerTransform == null)
        {
            Debug.LogError("[CompanionController] Cannot activate: No player found!");
            return;
        }

        isActive = true;

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
        }

        OnCompanionActivated?.Invoke();
        Debug.Log("[CompanionController] Companion activated");
    }

    /// <summary>
    /// Deactivate the companion. Hides but doesn't destroy.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return;

        isActive = false;

        // Stop movement
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = false;
        }

        OnCompanionDeactivated?.Invoke();
        Debug.Log("[CompanionController] Companion deactivated");
    }

    /// <summary>
    /// Teleport companion to a position (must be on NavMesh).
    /// </summary>
    public bool TeleportTo(Vector3 position)
    {
        // Validate position is on NavMesh
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[CompanionController] Cannot teleport: Position not on NavMesh");
            return false;
        }

        // Stop current navigation
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

        // Warp to position
        if (navAgent != null)
        {
            navAgent.Warp(hit.position);
        }
        else
        {
            transform.position = hit.position;
        }

        OnCompanionMoved?.Invoke(hit.position);
        Debug.Log($"[CompanionController] Teleported to {hit.position}");
        return true;
    }

    /// <summary>
    /// Set companion active state.
    /// </summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Get distance to player.
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    /// <summary>
    /// Check if companion is within interaction range of player.
    /// </summary>
    public bool IsWithinInteractionRange()
    {
        if (companionData == null) return false;
        return GetDistanceToPlayer() <= companionData.InteractionRange;
    }

#if UNITY_EDITOR
    [Button("Activate"), BoxGroup("Debug")]
    private void DebugActivate()
    {
        if (Application.isPlaying)
        {
            Activate();
        }
    }

    [Button("Deactivate"), BoxGroup("Debug")]
    private void DebugDeactivate()
    {
        if (Application.isPlaying)
        {
            Deactivate();
        }
    }

    [Button("Teleport to Player"), BoxGroup("Debug")]
    private void DebugTeleportToPlayer()
    {
        if (Application.isPlaying && playerTransform != null)
        {
            TeleportTo(playerTransform.position + playerTransform.forward * 3f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (companionData == null) return;

        // Draw interaction range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, companionData.InteractionRange);

        // Draw follow distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, companionData.FollowDistance);
    }
#endif
}
```

#### Step 2.2 — Update Companion Prefab

1. Open `Companion.prefab`
2. Add `CompanionController` component to root
3. Assign `DefaultCompanionData.asset` to the data field
4. Assign visual root (the Capsule child)
5. Assign interaction collider (the sphere trigger)

#### Step 2.3 — Test Basic Functionality

1. Place companion prefab in scene
2. Enter Play Mode
3. Use debug buttons in Inspector:
   - "Activate" — Companion should become visible
   - "Deactivate" — Companion should hide
   - "Teleport to Player" — Should move to player
4. Verify console logs show state changes

### ✅ Validation Checklist

- [ ] `CompanionController.cs` compiles without errors
- [ ] Prefab has component with data assigned
- [ ] Activate/Deactivate toggles visibility
- [ ] Teleport works and validates NavMesh position
- [ ] Distance calculations work correctly
- [ ] Gizmos draw in Scene view when selected

### What "Done" Looks Like

A companion object exists in the scene. You can activate/deactivate it and teleport it around. No autonomous behavior yet.

---

## Phase 3 — Companion Inventory & Resource Handling

### Goal
Give the companion the ability to carry bulk resources using the existing `IResourceHolder` interface.

### What is Implemented
- `CompanionInventory.cs` — Resource storage implementing `IResourceHolder`
- Integration with `ResourceInventory` class
- Resource transfer utilities

### What is Intentionally Deferred
- Capacity limits (infinite storage for now)
- Visual feedback for stored resources

### Steps

#### Step 3.1 — Create CompanionInventory.cs

Create `Assets/Scripts/Companion/CompanionInventory.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles resource storage for the companion.
/// Implements IResourceHolder for compatibility with existing systems.
/// </summary>
public class CompanionInventory : MonoBehaviour, IResourceHolder
{
    [BoxGroup("References")]
    [SerializeField] private CompanionController controller;

    [BoxGroup("Storage")]
    [ShowInInspector, ReadOnly]
    private ResourceInventory inventory = new ResourceInventory();

    // Events (from IResourceHolder)
    public event Action<ResourceType, int> OnResourcesChanged;

    // Additional events
    public event Action OnInventoryCleared;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CompanionController>();
        }

        // Subscribe to internal inventory changes
        inventory.OnResourceChanged += HandleResourceChanged;
    }

    private void OnDestroy()
    {
        inventory.OnResourceChanged -= HandleResourceChanged;
    }

    /// <summary>
    /// Get the amount of a specific resource.
    /// </summary>
    public int GetResourceAmount(ResourceType type)
    {
        return inventory.Get(type);
    }

    /// <summary>
    /// Add resources to the companion's inventory.
    /// Returns the amount actually added.
    /// </summary>
    public int AddResources(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        if (type == ResourceType.None) return 0;

        // Check if companion accepts this resource type
        if (controller != null && controller.Data != null)
        {
            if (!controller.Data.CanAcceptResource(type))
            {
                Debug.Log($"[CompanionInventory] Companion does not accept {type}");
                return 0;
            }
        }

        inventory.Add(type, amount);
        Debug.Log($"[CompanionInventory] Added {amount}x {type}. Total: {inventory.Get(type)}");

        return amount;
    }

    /// <summary>
    /// Remove resources from the companion's inventory.
    /// All-or-nothing operation.
    /// </summary>
    public bool RemoveResources(ResourceType type, int amount)
    {
        if (!HasResources(type, amount)) return false;

        bool success = inventory.TryRemove(type, amount);

        if (success)
        {
            Debug.Log($"[CompanionInventory] Removed {amount}x {type}. Remaining: {inventory.Get(type)}");
        }

        return success;
    }

    /// <summary>
    /// Check if the companion has at least this many resources.
    /// </summary>
    public bool HasResources(ResourceType type, int amount)
    {
        return inventory.Has(type, amount);
    }

    /// <summary>
    /// Check if the companion is carrying any resources.
    /// </summary>
    public bool HasAnyResources()
    {
        return inventory.GetTotalCount() > 0;
    }

    /// <summary>
    /// Get total count of all resources.
    /// </summary>
    public int GetTotalResourceCount()
    {
        return inventory.GetTotalCount();
    }

    /// <summary>
    /// Get all stored resources.
    /// </summary>
    public IEnumerable<KeyValuePair<ResourceType, int>> GetAllResources()
    {
        return inventory.GetAll();
    }

    /// <summary>
    /// Clear all resources from inventory.
    /// </summary>
    public void ClearInventory()
    {
        inventory.Clear();
        OnInventoryCleared?.Invoke();
        Debug.Log("[CompanionInventory] Inventory cleared");
    }

    /// <summary>
    /// Transfer all resources from a source (typically player) to companion.
    /// Returns total amount transferred.
    /// </summary>
    public int TransferAllFrom(IResourceHolder source)
    {
        if (source == null) return 0;

        int totalTransferred = 0;

        // We need to iterate over all resource types
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;

            int sourceAmount = source.GetResourceAmount(type);
            if (sourceAmount <= 0) continue;

            // Check if companion accepts this type
            if (controller != null && controller.Data != null)
            {
                if (!controller.Data.CanAcceptResource(type)) continue;
            }

            // Remove from source
            if (source.RemoveResources(type, sourceAmount))
            {
                // Add to companion
                AddResources(type, sourceAmount);
                totalTransferred += sourceAmount;
            }
        }

        if (totalTransferred > 0)
        {
            Debug.Log($"[CompanionInventory] Transferred {totalTransferred} total resources from source");
        }

        return totalTransferred;
    }

    /// <summary>
    /// Transfer all resources to a storage building.
    /// Returns total amount deposited.
    /// </summary>
    public int TransferAllTo(IResourceStorage storage)
    {
        if (storage == null) return 0;

        int totalDeposited = 0;

        // Snapshot current inventory to avoid modification during iteration
        var snapshot = new List<KeyValuePair<ResourceType, int>>(GetAllResources());

        foreach (var kvp in snapshot)
        {
            if (!storage.CanAcceptResource(kvp.Key)) continue;

            int toDeposit = kvp.Value;

            // Check storage capacity
            int capacity = storage.GetRemainingCapacity(kvp.Key);
            if (capacity < toDeposit && capacity != int.MaxValue)
            {
                toDeposit = capacity;
            }

            if (toDeposit <= 0) continue;

            // Deposit to storage
            int deposited = storage.TryDeposit(kvp.Key, toDeposit);

            if (deposited > 0)
            {
                // Remove from companion
                RemoveResources(kvp.Key, deposited);
                totalDeposited += deposited;
            }
        }

        if (totalDeposited > 0)
        {
            Debug.Log($"[CompanionInventory] Deposited {totalDeposited} total resources to storage");
        }

        return totalDeposited;
    }

    private void HandleResourceChanged(ResourceType type, int newAmount)
    {
        OnResourcesChanged?.Invoke(type, newAmount);
    }

#if UNITY_EDITOR
    [Button("Add Test Resources"), BoxGroup("Debug")]
    private void DebugAddResources()
    {
        AddResources(ResourceType.Wood, 10);
        AddResources(ResourceType.Stone, 5);
        AddResources(ResourceType.IronOre, 3);
    }

    [Button("Clear Inventory"), BoxGroup("Debug")]
    private void DebugClearInventory()
    {
        ClearInventory();
    }

    [Button("Log Contents"), BoxGroup("Debug")]
    private void DebugLogContents()
    {
        Debug.Log($"[CompanionInventory] Total: {GetTotalResourceCount()}");
        foreach (var kvp in GetAllResources())
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value}");
        }
    }
#endif
}
```

#### Step 3.2 — Update Companion Prefab

1. Open `Companion.prefab`
2. Add `CompanionInventory` component
3. Assign the `CompanionController` reference

#### Step 3.3 — Update CompanionController

Add inventory reference to `CompanionController.cs`:

```csharp
// Add to fields section:
[BoxGroup("References")]
[SerializeField] private CompanionInventory inventory;

// Add public accessor:
public CompanionInventory Inventory => inventory;

// Add to Awake() if not assigned:
if (inventory == null)
{
    inventory = GetComponent<CompanionInventory>();
}
```

#### Step 3.4 — Test Resource Handling

1. Enter Play Mode
2. Select companion in Hierarchy
3. Use debug buttons:
   - "Add Test Resources" — Should add wood, stone, iron
   - "Log Contents" — Should show inventory
   - "Clear Inventory" — Should remove everything
4. Verify console logs show operations

### ✅ Validation Checklist

- [ ] `CompanionInventory.cs` compiles without errors
- [ ] Implements `IResourceHolder` interface correctly
- [ ] Add/Remove operations work as expected
- [ ] Resource type filtering works (if configured in data)
- [ ] `TransferAllFrom()` moves resources from source
- [ ] `TransferAllTo()` moves resources to storage
- [ ] Events fire on resource changes

### What "Done" Looks Like

The companion can store resources. You can add, remove, and query resources. Transfer methods exist for moving resources between player ↔ companion ↔ storage.

---

## Phase 4 — Companion State Machine

### Goal
Implement a clean state machine to manage companion behavior modes.

### What is Implemented
- `CompanionState` enum
- State transition logic in `CompanionController`
- State entry/exit callbacks

### What is Intentionally Deferred
- Actual state behavior (navigation, etc.)
- Animation triggers

### Steps

#### Step 4.1 — Define State Enum

Add to `CompanionController.cs` or create a separate file `Assets/Scripts/Companion/CompanionState.cs`:

```csharp
/// <summary>
/// Possible states for the companion.
/// </summary>
public enum CompanionState
{
    /// <summary>
    /// Companion is inactive/hidden.
    /// </summary>
    Inactive,

    /// <summary>
    /// Companion is idle, not moving, waiting for commands.
    /// </summary>
    Idle,

    /// <summary>
    /// Companion is being summoned by player call.
    /// Spawning/teleporting near player.
    /// </summary>
    BeingCalled,

    /// <summary>
    /// Companion is actively following the player.
    /// Maintains follow distance.
    /// </summary>
    FollowingPlayer,

    /// <summary>
    /// Companion is navigating to a resource depot.
    /// </summary>
    MovingToDepot,

    /// <summary>
    /// Companion is depositing resources at a depot.
    /// </summary>
    Depositing,

    /// <summary>
    /// Companion is returning to player after depositing.
    /// </summary>
    ReturningToPlayer
}
```

#### Step 4.2 — Update CompanionController with State Machine

Add state machine logic to `CompanionController.cs`:

```csharp
// Add to fields:
[BoxGroup("State")]
[ShowInInspector, ReadOnly]
private CompanionState currentState = CompanionState.Inactive;

[BoxGroup("State")]
[ShowInInspector, ReadOnly]
private CompanionState previousState = CompanionState.Inactive;

// Events
public event Action<CompanionState, CompanionState> OnStateChanged;

// Public accessor
public CompanionState CurrentState => currentState;
public CompanionState PreviousState => previousState;

/// <summary>
/// Request a state change. Validates transition and invokes callbacks.
/// </summary>
public bool RequestStateChange(CompanionState newState)
{
    if (currentState == newState) return true;

    // Validate transition
    if (!IsValidTransition(currentState, newState))
    {
        Debug.LogWarning($"[CompanionController] Invalid state transition: {currentState} -> {newState}");
        return false;
    }

    // Exit current state
    OnStateExit(currentState);

    // Change state
    previousState = currentState;
    currentState = newState;

    // Enter new state
    OnStateEnter(newState);

    // Fire event
    OnStateChanged?.Invoke(previousState, currentState);
    Debug.Log($"[CompanionController] State: {previousState} -> {currentState}");

    return true;
}

/// <summary>
/// Force a state change without validation (use sparingly).
/// </summary>
public void ForceState(CompanionState newState)
{
    if (currentState == newState) return;

    OnStateExit(currentState);
    previousState = currentState;
    currentState = newState;
    OnStateEnter(newState);
    OnStateChanged?.Invoke(previousState, currentState);
    Debug.Log($"[CompanionController] State forced: {previousState} -> {currentState}");
}

private bool IsValidTransition(CompanionState from, CompanionState to)
{
    // Define valid transitions
    switch (from)
    {
        case CompanionState.Inactive:
            // Can only go to BeingCalled or Idle from inactive
            return to == CompanionState.BeingCalled || to == CompanionState.Idle;

        case CompanionState.Idle:
            // From idle, can be called, start following, or go to depot
            return to == CompanionState.BeingCalled
                || to == CompanionState.FollowingPlayer
                || to == CompanionState.MovingToDepot
                || to == CompanionState.Inactive;

        case CompanionState.BeingCalled:
            // After being called, go to following
            return to == CompanionState.FollowingPlayer
                || to == CompanionState.Idle
                || to == CompanionState.Inactive;

        case CompanionState.FollowingPlayer:
            // While following, can go idle, move to depot, or be called again
            return to == CompanionState.Idle
                || to == CompanionState.MovingToDepot
                || to == CompanionState.BeingCalled
                || to == CompanionState.Inactive;

        case CompanionState.MovingToDepot:
            // Moving to depot leads to depositing, or can be called back
            return to == CompanionState.Depositing
                || to == CompanionState.BeingCalled
                || to == CompanionState.FollowingPlayer
                || to == CompanionState.Inactive;

        case CompanionState.Depositing:
            // After depositing, return to player or follow
            return to == CompanionState.ReturningToPlayer
                || to == CompanionState.FollowingPlayer
                || to == CompanionState.BeingCalled
                || to == CompanionState.Inactive;

        case CompanionState.ReturningToPlayer:
            // After returning, follow or go idle
            return to == CompanionState.FollowingPlayer
                || to == CompanionState.Idle
                || to == CompanionState.BeingCalled
                || to == CompanionState.Inactive;

        default:
            return false;
    }
}

private void OnStateEnter(CompanionState state)
{
    switch (state)
    {
        case CompanionState.Inactive:
            // Handled by Deactivate()
            break;

        case CompanionState.Idle:
            // Stop movement
            StopNavigation();
            break;

        case CompanionState.BeingCalled:
            // Will be handled by spawn logic
            break;

        case CompanionState.FollowingPlayer:
            // Will start following in Update
            break;

        case CompanionState.MovingToDepot:
            // Will set destination in auto-deposit logic
            break;

        case CompanionState.Depositing:
            // Will handle in depositing logic
            StopNavigation();
            break;

        case CompanionState.ReturningToPlayer:
            // Will set destination to player
            break;
    }
}

private void OnStateExit(CompanionState state)
{
    // Cleanup when leaving a state
    switch (state)
    {
        case CompanionState.BeingCalled:
            // Nothing special
            break;

        case CompanionState.MovingToDepot:
            // Clear depot target if needed
            break;

        case CompanionState.Depositing:
            // Finalize deposit
            break;
    }
}

private void StopNavigation()
{
    if (navAgent != null && navAgent.isOnNavMesh)
    {
        navAgent.ResetPath();
        navAgent.velocity = Vector3.zero;
    }
}

// Modify Activate() to set initial state:
public void Activate()
{
    if (isActive) return;

    // ... existing activation code ...

    // Set initial state
    currentState = CompanionState.Idle;
    OnStateChanged?.Invoke(CompanionState.Inactive, CompanionState.Idle);
}

// Modify Deactivate() to set state:
public void Deactivate()
{
    if (!isActive) return;

    // ... existing deactivation code ...

    // Reset state
    previousState = currentState;
    currentState = CompanionState.Inactive;
    OnStateChanged?.Invoke(previousState, currentState);
}
```

#### Step 4.3 — Create State Transition Diagram

For reference, here's the valid state transition diagram:

```
                    ┌──────────────────────────────────────────────────┐
                    │                                                  │
                    ▼                                                  │
┌─────────────┐  [Call]  ┌──────────────┐  [Arrive]  ┌─────────────────┴──┐
│   INACTIVE  │─────────►│  BEING       │───────────►│   FOLLOWING        │
└─────────────┘          │  CALLED      │            │    PLAYER          │
      ▲                  └──────────────┘            └────────┬───────────┘
      │                                                       │
      │                                                       │ [Idle Timer]
      │                                                       ▼
      │                  ┌──────────────┐  [Arrive]  ┌────────────────────┐
      │                  │  RETURNING   │◄───────────│   MOVING TO        │
      │                  │  TO PLAYER   │            │    DEPOT           │
      │                  └───────┬──────┘            └────────────────────┘
      │                          │                            │
      │                          │ [Arrive]                   │ [Arrive]
      │                          ▼                            ▼
      │                  ┌──────────────┐            ┌────────────────────┐
      └──────────────────│    IDLE      │◄───────────│   DEPOSITING       │
                         └──────────────┘  [Done]    └────────────────────┘
```

#### Step 4.4 — Test State Transitions

1. Enter Play Mode
2. Select companion
3. Manually trigger state changes via debug buttons:
   - Add buttons for each state transition
4. Verify:
   - Valid transitions work
   - Invalid transitions are rejected with warning
   - Events fire correctly
   - Console logs show transitions

Add debug buttons to `CompanionController`:

```csharp
#if UNITY_EDITOR
[Button("To Idle"), BoxGroup("Debug/States")]
private void DebugToIdle() => RequestStateChange(CompanionState.Idle);

[Button("To Following"), BoxGroup("Debug/States")]
private void DebugToFollowing() => RequestStateChange(CompanionState.FollowingPlayer);

[Button("To MovingToDepot"), BoxGroup("Debug/States")]
private void DebugToMovingToDepot() => RequestStateChange(CompanionState.MovingToDepot);
#endif
```

### ✅ Validation Checklist

- [ ] `CompanionState` enum defined with all states
- [ ] `RequestStateChange()` validates transitions
- [ ] `ForceState()` works for edge cases
- [ ] State entry/exit callbacks fire
- [ ] `OnStateChanged` event fires with correct values
- [ ] Invalid transitions are logged and rejected
- [ ] Console shows state transition logs

### What "Done" Looks Like

The companion has a state machine. You can see the current state in Inspector. State transitions are validated. Events fire on changes. No actual behavior yet — states just change.

---

## Phase 5 — Navigation & Movement

### Goal
Implement NavMesh-based movement for the companion, including following player and navigating to destinations.

### What is Implemented
- `CompanionMovement.cs` — Movement handling
- Follow player behavior
- Move to destination behavior
- Arrival detection

### What is Intentionally Deferred
- Obstacle avoidance tuning
- Animation integration

### Steps

#### Step 5.1 — Create CompanionMovement.cs

Create `Assets/Scripts/Companion/CompanionMovement.cs`:

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles all movement and navigation for the companion.
/// Uses NavMeshAgent for pathfinding.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CompanionMovement : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private CompanionController controller;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Vector3 currentDestination;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool hasDestination = false;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private MovementMode currentMode = MovementMode.None;

    public enum MovementMode
    {
        None,
        FollowPlayer,
        MoveToDestination
    }

    // Components
    private NavMeshAgent navAgent;
    private CompanionData data;

    // Events
    public event Action OnDestinationReached;
    public event Action OnDestinationUnreachable;

    // Public accessors
    public bool HasDestination => hasDestination;
    public Vector3 CurrentDestination => currentDestination;
    public MovementMode CurrentMode => currentMode;
    public bool IsMoving => navAgent != null && navAgent.velocity.magnitude > 0.1f;
    public float RemainingDistance => navAgent != null ? navAgent.remainingDistance : float.MaxValue;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();

        if (controller == null)
        {
            controller = GetComponent<CompanionController>();
        }
    }

    private void Start()
    {
        if (controller != null)
        {
            data = controller.Data;
        }
    }

    private void Update()
    {
        if (controller == null || !controller.IsActive) return;

        switch (currentMode)
        {
            case MovementMode.FollowPlayer:
                UpdateFollowPlayer();
                break;

            case MovementMode.MoveToDestination:
                UpdateMoveToDestination();
                break;
        }
    }

    /// <summary>
    /// Start following the player, maintaining follow distance.
    /// </summary>
    public void StartFollowingPlayer()
    {
        currentMode = MovementMode.FollowPlayer;
        hasDestination = false;
        Debug.Log("[CompanionMovement] Started following player");
    }

    /// <summary>
    /// Move to a specific world position.
    /// </summary>
    public bool SetDestination(Vector3 destination)
    {
        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning("[CompanionMovement] Agent not on NavMesh");
            return false;
        }

        // Validate destination is reachable
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning("[CompanionMovement] Destination not on NavMesh");
            OnDestinationUnreachable?.Invoke();
            return false;
        }

        currentDestination = hit.position;
        hasDestination = true;
        currentMode = MovementMode.MoveToDestination;

        bool pathSet = navAgent.SetDestination(currentDestination);

        if (!pathSet)
        {
            Debug.LogWarning("[CompanionMovement] Failed to set destination path");
            OnDestinationUnreachable?.Invoke();
            return false;
        }

        Debug.Log($"[CompanionMovement] Moving to {currentDestination}");
        return true;
    }

    /// <summary>
    /// Stop all movement immediately.
    /// </summary>
    public void Stop()
    {
        currentMode = MovementMode.None;
        hasDestination = false;

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        Debug.Log("[CompanionMovement] Stopped");
    }

    /// <summary>
    /// Check if companion has arrived at current destination.
    /// </summary>
    public bool HasArrivedAtDestination()
    {
        if (!hasDestination) return false;
        if (navAgent == null) return false;

        // Check if path is complete and we're close enough
        if (!navAgent.pathPending)
        {
            float arrivalDist = data != null ? data.ArrivalDistance : 1.5f;

            if (navAgent.remainingDistance <= arrivalDist)
            {
                if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.01f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if companion is close enough to player.
    /// </summary>
    public bool IsNearPlayer(float threshold = -1f)
    {
        if (controller == null || controller.PlayerTransform == null) return false;

        if (threshold < 0)
        {
            threshold = data != null ? data.FollowDistance : 3f;
        }

        float distance = Vector3.Distance(transform.position, controller.PlayerTransform.position);
        return distance <= threshold;
    }

    private void UpdateFollowPlayer()
    {
        if (controller.PlayerTransform == null) return;

        float followDist = data != null ? data.FollowDistance : 3f;
        float currentDist = Vector3.Distance(transform.position, controller.PlayerTransform.position);

        // Only move if we're too far from player
        if (currentDist > followDist * 1.5f)
        {
            // Calculate position behind player
            Vector3 targetPos = controller.PlayerTransform.position -
                               controller.PlayerTransform.forward * followDist;

            // Validate and set destination
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                if (navAgent.isOnNavMesh)
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }
        else if (currentDist < followDist * 0.5f)
        {
            // Too close, stop
            if (navAgent.isOnNavMesh && navAgent.hasPath)
            {
                navAgent.ResetPath();
            }
        }
    }

    private void UpdateMoveToDestination()
    {
        if (!hasDestination) return;

        // Check for arrival
        if (HasArrivedAtDestination())
        {
            hasDestination = false;
            currentMode = MovementMode.None;
            OnDestinationReached?.Invoke();
            Debug.Log("[CompanionMovement] Arrived at destination");
        }

        // Check for path failure
        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            hasDestination = false;
            currentMode = MovementMode.None;
            OnDestinationUnreachable?.Invoke();
            Debug.LogWarning("[CompanionMovement] Path became invalid");
        }
    }

#if UNITY_EDITOR
    [Button("Follow Player"), BoxGroup("Debug")]
    private void DebugFollowPlayer()
    {
        if (Application.isPlaying)
        {
            StartFollowingPlayer();
        }
    }

    [Button("Stop"), BoxGroup("Debug")]
    private void DebugStop()
    {
        if (Application.isPlaying)
        {
            Stop();
        }
    }

    [Button("Move to Random"), BoxGroup("Debug")]
    private void DebugMoveToRandom()
    {
        if (Application.isPlaying)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * 10f;
            randomOffset.y = 0;
            SetDestination(transform.position + randomOffset);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, 0.5f);
        }

        if (controller != null && controller.PlayerTransform != null && data != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(controller.PlayerTransform.position, data.FollowDistance);
        }
    }
#endif
}
```

#### Step 5.2 — Update Companion Prefab

1. Open `Companion.prefab`
2. Add `CompanionMovement` component
3. Assign `CompanionController` reference

#### Step 5.3 — Update CompanionController

Add movement reference:

```csharp
// Add to fields:
[BoxGroup("References")]
[SerializeField] private CompanionMovement movement;

// Public accessor:
public CompanionMovement Movement => movement;

// Add to Awake():
if (movement == null)
{
    movement = GetComponent<CompanionMovement>();
}
```

#### Step 5.4 — Connect State Machine to Movement

Update `OnStateEnter()` in `CompanionController`:

```csharp
private void OnStateEnter(CompanionState state)
{
    switch (state)
    {
        case CompanionState.Inactive:
            movement?.Stop();
            break;

        case CompanionState.Idle:
            movement?.Stop();
            break;

        case CompanionState.BeingCalled:
            // Spawn logic will handle this
            break;

        case CompanionState.FollowingPlayer:
            movement?.StartFollowingPlayer();
            break;

        case CompanionState.MovingToDepot:
            // Destination set by auto-deposit logic
            break;

        case CompanionState.Depositing:
            movement?.Stop();
            break;

        case CompanionState.ReturningToPlayer:
            // Move back to player
            if (playerTransform != null)
            {
                movement?.SetDestination(playerTransform.position);
            }
            break;
    }
}
```

#### Step 5.5 — Test Navigation

1. Enter Play Mode
2. Activate companion
3. Test following:
   - Change to FollowingPlayer state
   - Walk around — companion should follow
   - Get far away — companion should catch up
   - Get close — companion should stop
4. Test destination:
   - Use "Move to Random" debug button
   - Companion should navigate to position
   - Gizmo should show destination line

### ✅ Validation Checklist

- [ ] `CompanionMovement.cs` compiles without errors
- [ ] Following player maintains appropriate distance
- [ ] `SetDestination()` validates NavMesh positions
- [ ] Arrival detection works correctly
- [ ] `OnDestinationReached` fires when arriving
- [ ] Path failure is detected and reported
- [ ] Stop() immediately halts movement
- [ ] Gizmos show destination and follow distance

### What "Done" Looks Like

The companion can follow the player and navigate to arbitrary points. It detects arrival and reports path failures. Movement integrates with state machine.

---

## Phase 6 — Player Interaction (Calling & Depositing)

### Goal
Implement the two main player interactions: calling the companion and depositing resources.

### What is Implemented
- `IInteractable` implementation on companion
- Call behavior with spawn position calculation
- Resource deposit from player to companion

### What is Intentionally Deferred
- UI for deposit confirmation
- Partial deposits

### Steps

#### Step 6.1 — Create CompanionCallHandler.cs

Create `Assets/Scripts/Companion/CompanionCallHandler.cs`:

```csharp
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles the "call companion" behavior.
/// Spawns companion near player, preferring behind/outside FOV.
/// </summary>
public class CompanionCallHandler : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private CompanionController controller;

    [BoxGroup("Settings")]
    [SerializeField] private int maxSpawnAttempts = 10;

    [BoxGroup("Settings")]
    [Tooltip("If true, teleports instantly. If false, companion navigates to player.")]
    [SerializeField] private bool teleportWhenCalled = true;

    private CompanionData data;

    private void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<CompanionController>();
        }

        if (controller != null)
        {
            data = controller.Data;
        }
    }

    /// <summary>
    /// Call the companion to the player.
    /// </summary>
    public void CallCompanion()
    {
        if (controller == null || controller.PlayerTransform == null)
        {
            Debug.LogWarning("[CompanionCallHandler] Cannot call: Missing controller or player");
            return;
        }

        // If already active and close, just switch to following
        if (controller.IsActive && controller.CurrentState != CompanionState.Inactive)
        {
            if (controller.GetDistanceToPlayer() < (data?.MaxSpawnDistance ?? 10f))
            {
                // Already nearby, just follow
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.Log("[CompanionCallHandler] Companion already nearby, now following");
                return;
            }
        }

        // Start call sequence
        controller.RequestStateChange(CompanionState.BeingCalled);

        if (teleportWhenCalled)
        {
            // Find spawn position and teleport
            Vector3 spawnPos = CalculateSpawnPosition();

            if (controller.TeleportTo(spawnPos))
            {
                controller.Activate();
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.Log($"[CompanionCallHandler] Companion teleported to {spawnPos}");
            }
            else
            {
                // Fallback: spawn at player position
                controller.TeleportTo(controller.PlayerTransform.position);
                controller.Activate();
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.LogWarning("[CompanionCallHandler] Fallback spawn at player position");
            }
        }
        else
        {
            // Companion navigates to player
            controller.Activate();
            if (controller.Movement != null)
            {
                controller.Movement.SetDestination(controller.PlayerTransform.position);
            }
            // Will transition to Following when arrives
        }
    }

    /// <summary>
    /// Calculate a spawn position near player, preferably behind/outside FOV.
    /// </summary>
    private Vector3 CalculateSpawnPosition()
    {
        Transform player = controller.PlayerTransform;

        float minDist = data?.MinSpawnDistance ?? 5f;
        float maxDist = data?.MaxSpawnDistance ?? 10f;
        float angleRange = data?.BehindPlayerAngleRange ?? 120f;
        bool preferBehind = data?.PreferSpawnBehindPlayer ?? true;

        // Try to find position behind player first
        if (preferBehind)
        {
            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                Vector3 candidate = GetBehindPlayerPosition(player, minDist, maxDist, angleRange);

                if (IsValidSpawnPosition(candidate))
                {
                    return candidate;
                }
            }
        }

        // Fallback: try any direction
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float distance = Random.Range(minDist, maxDist);
            float angle = Random.Range(0f, 360f);

            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
            Vector3 candidate = player.position + offset;

            if (IsValidSpawnPosition(candidate))
            {
                return candidate;
            }
        }

        // Last resort: directly behind player at min distance
        Vector3 fallback = player.position - player.forward * minDist;
        if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return player.position;
    }

    private Vector3 GetBehindPlayerPosition(Transform player, float minDist, float maxDist, float angleRange)
    {
        float distance = Random.Range(minDist, maxDist);

        // Angle offset from directly behind (0 = directly behind)
        float angleOffset = Random.Range(-angleRange / 2f, angleRange / 2f);

        // Direction behind player, rotated by offset
        Vector3 behindDir = Quaternion.Euler(0, angleOffset, 0) * -player.forward;

        return player.position + behindDir * distance;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if on NavMesh
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        // Check line of sight (optional - don't spawn through walls)
        if (controller.PlayerTransform != null)
        {
            Vector3 toPlayer = controller.PlayerTransform.position - hit.position;

            if (Physics.Raycast(hit.position + Vector3.up, toPlayer.normalized, toPlayer.magnitude - 1f))
            {
                // Something blocking - might still be okay, but prefer open paths
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    [Button("Call Companion"), BoxGroup("Debug")]
    private void DebugCallCompanion()
    {
        if (Application.isPlaying)
        {
            CallCompanion();
        }
    }

    [Button("Show Spawn Candidates"), BoxGroup("Debug")]
    private void DebugShowSpawnCandidates()
    {
        if (Application.isPlaying && controller?.PlayerTransform != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 pos = CalculateSpawnPosition();
                Debug.DrawLine(controller.PlayerTransform.position, pos, Color.yellow, 3f);
                Debug.DrawLine(pos, pos + Vector3.up * 2f, IsValidSpawnPosition(pos) ? Color.green : Color.red, 3f);
            }
        }
    }
#endif
}
```

#### Step 6.2 — Create CompanionInteraction.cs

Create `Assets/Scripts/Companion/CompanionInteraction.cs`:

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles player interaction with the companion.
/// Implements IInteractable to integrate with existing interaction system.
/// </summary>
public class CompanionInteraction : MonoBehaviour, IInteractable
{
    [BoxGroup("References")]
    [SerializeField] private CompanionController controller;

    [BoxGroup("References")]
    [SerializeField] private CompanionInventory inventory;

    // Events
    public event Action<int> OnResourcesDeposited;

    // IInteractable implementation
    public string InteractionPrompt
    {
        get
        {
            if (controller?.Data != null)
            {
                return controller.Data.InteractionPrompt;
            }
            return "Deposit Resources";
        }
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CompanionController>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<CompanionInventory>();
        }
    }

    /// <summary>
    /// Check if player can interact with companion.
    /// </summary>
    public bool CanInteract(GameObject interactor)
    {
        // Must be active
        if (controller == null || !controller.IsActive) return false;

        // Must be in a state that allows interaction
        if (controller.CurrentState == CompanionState.Inactive ||
            controller.CurrentState == CompanionState.MovingToDepot ||
            controller.CurrentState == CompanionState.Depositing)
        {
            return false;
        }

        // Check range
        if (!controller.IsWithinInteractionRange()) return false;

        // Check if player has resources to deposit
        var playerInventory = GetPlayerInventory(interactor);
        if (playerInventory == null || !HasAnyResources(playerInventory)) return false;

        return true;
    }

    /// <summary>
    /// Perform interaction - deposit player resources into companion.
    /// </summary>
    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            Debug.LogWarning("[CompanionInteraction] Cannot interact");
            return;
        }

        var playerInventory = GetPlayerInventory(interactor);
        if (playerInventory == null || inventory == null)
        {
            Debug.LogWarning("[CompanionInteraction] Missing inventory references");
            return;
        }

        // Transfer all resources from player to companion
        int transferred = inventory.TransferAllFrom(playerInventory);

        if (transferred > 0)
        {
            OnResourcesDeposited?.Invoke(transferred);
            Debug.Log($"[CompanionInteraction] Player deposited {transferred} resources");

            // Ensure companion is following after deposit
            if (controller.CurrentState != CompanionState.FollowingPlayer)
            {
                controller.RequestStateChange(CompanionState.FollowingPlayer);
            }
        }
        else
        {
            Debug.Log("[CompanionInteraction] No resources to deposit");
        }
    }

    private IResourceHolder GetPlayerInventory(GameObject interactor)
    {
        // Try to get from interactor
        var holder = interactor.GetComponent<IResourceHolder>();
        if (holder != null) return holder;

        // Try PlayerManager
        var playerInventory = PlayerManager.Instance?.ResourceInventory;
        return playerInventory;
    }

    private bool HasAnyResources(IResourceHolder holder)
    {
        // Check all resource types
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.None) continue;
            if (holder.GetResourceAmount(type) > 0) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    [Button("Simulate Deposit"), BoxGroup("Debug")]
    private void DebugSimulateDeposit()
    {
        if (Application.isPlaying)
        {
            var player = PlayerManager.Instance?.gameObject;
            if (player != null)
            {
                Interact(player);
            }
        }
    }
#endif
}
```

#### Step 6.3 — Create Player Input Handler for Calling

Add to your player input handling (or create new script):

```csharp
// In PlayerInputHandler.cs or new CompanionInputHandler.cs

private void Update()
{
    // ... existing input handling ...

    // Call companion
    if (Input.GetKeyDown(KeyCode.C))
    {
        CallCompanion();
    }
}

private void CallCompanion()
{
    // Find companion in scene
    var companion = FindObjectOfType<CompanionCallHandler>();

    if (companion != null)
    {
        companion.CallCompanion();
    }
    else
    {
        Debug.LogWarning("[Input] No companion found in scene");
    }
}
```

Alternatively, create a dedicated `CompanionManager` singleton that holds the reference.

#### Step 6.4 — Update Companion Prefab

1. Add `CompanionCallHandler` component
2. Add `CompanionInteraction` component
3. Wire up all references

#### Step 6.5 — Test Calling and Depositing

**Test Calling:**
1. Enter Play Mode
2. Press C (or use debug button)
3. Companion should spawn behind player
4. Companion should follow player
5. Try calling when far away — should teleport nearby

**Test Depositing:**
1. Give player some resources (debug)
2. Walk near companion
3. Interact (use your interaction key)
4. Resources should transfer to companion
5. Check companion inventory in Inspector

### ✅ Validation Checklist

- [ ] `CompanionCallHandler.cs` compiles and spawns companion
- [ ] Spawn position prefers behind player
- [ ] Spawn validates NavMesh
- [ ] `CompanionInteraction.cs` implements `IInteractable`
- [ ] `CanInteract()` checks state, range, and player resources
- [ ] `Interact()` transfers all player resources to companion
- [ ] Events fire on deposit
- [ ] State transitions to Following after deposit

### What "Done" Looks Like

Player can press C to call the companion. Companion appears behind player. Player can interact with companion to deposit all resources. Companion then follows player.

---

## Phase 7 — Auto-Deposit Logic & Timers

### Goal
Implement the idle timer and automatic deposit behavior — the core "mule" functionality.

### What is Implemented
- `CompanionAutoDeposit.cs` — Idle timer and deposit logic
- Find nearest depot using BuildingRegistry
- Navigate to depot and deposit resources

### What is Intentionally Deferred
- Multiple depot visits (single deposit then return)
- Priority selection between depots

### Steps

#### Step 7.1 — Create CompanionAutoDeposit.cs

Create `Assets/Scripts/Companion/CompanionAutoDeposit.cs`:

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles automatic resource depositing after idle timeout.
/// Finds nearest depot, navigates to it, deposits resources, then returns.
/// </summary>
public class CompanionAutoDeposit : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private CompanionController controller;

    [BoxGroup("References")]
    [SerializeField] private CompanionInventory inventory;

    [BoxGroup("References")]
    [SerializeField] private CompanionMovement movement;

    [BoxGroup("References")]
    [SerializeField] private CompanionInteraction interaction;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private float idleTimer = 0f;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isAutoDepositTriggered = false;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private IResourceStorage targetDepot;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Transform targetDepotTransform;

    private CompanionData data;

    // Events
    public event Action OnAutoDepositStarted;
    public event Action OnAutoDepositCompleted;
    public event Action OnNoDepotFound;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CompanionController>();
        if (inventory == null) inventory = GetComponent<CompanionInventory>();
        if (movement == null) movement = GetComponent<CompanionMovement>();
        if (interaction == null) interaction = GetComponent<CompanionInteraction>();
    }

    private void Start()
    {
        if (controller != null)
        {
            data = controller.Data;

            // Subscribe to events
            controller.OnStateChanged += HandleStateChanged;
        }

        if (interaction != null)
        {
            interaction.OnResourcesDeposited += HandleResourcesDeposited;
        }

        if (movement != null)
        {
            movement.OnDestinationReached += HandleDestinationReached;
        }
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnStateChanged -= HandleStateChanged;
        }

        if (interaction != null)
        {
            interaction.OnResourcesDeposited -= HandleResourcesDeposited;
        }

        if (movement != null)
        {
            movement.OnDestinationReached -= HandleDestinationReached;
        }
    }

    private void Update()
    {
        if (controller == null || !controller.IsActive) return;

        // Only tick idle timer in FollowingPlayer state
        if (controller.CurrentState == CompanionState.FollowingPlayer)
        {
            UpdateIdleTimer();
        }
    }

    private void UpdateIdleTimer()
    {
        // Only count down if we have resources to deposit
        if (inventory == null || !inventory.HasAnyResources())
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;

        float threshold = data?.IdleTimeBeforeAutoDeposit ?? 5f;

        if (idleTimer >= threshold && !isAutoDepositTriggered)
        {
            TriggerAutoDeposit();
        }
    }

    /// <summary>
    /// Reset the idle timer (called when player interacts).
    /// </summary>
    public void ResetIdleTimer()
    {
        idleTimer = 0f;
        isAutoDepositTriggered = false;
    }

    /// <summary>
    /// Start the auto-deposit sequence.
    /// </summary>
    public void TriggerAutoDeposit()
    {
        if (isAutoDepositTriggered) return;
        if (inventory == null || !inventory.HasAnyResources()) return;

        // Find nearest depot
        IResourceStorage depot = FindNearestDepot();

        if (depot == null)
        {
            Debug.LogWarning("[CompanionAutoDeposit] No depot found within range");
            OnNoDepotFound?.Invoke();
            ResetIdleTimer();
            return;
        }

        // Get depot transform
        targetDepot = depot;
        targetDepotTransform = (depot as MonoBehaviour)?.transform;

        if (targetDepotTransform == null)
        {
            Debug.LogError("[CompanionAutoDeposit] Depot has no transform");
            ResetIdleTimer();
            return;
        }

        isAutoDepositTriggered = true;
        OnAutoDepositStarted?.Invoke();

        // Change state and start moving
        controller.RequestStateChange(CompanionState.MovingToDepot);
        movement?.SetDestination(targetDepotTransform.position);

        Debug.Log($"[CompanionAutoDeposit] Moving to depot at {targetDepotTransform.position}");
    }

    /// <summary>
    /// Cancel auto-deposit and return to following.
    /// </summary>
    public void CancelAutoDeposit()
    {
        if (!isAutoDepositTriggered) return;

        isAutoDepositTriggered = false;
        targetDepot = null;
        targetDepotTransform = null;

        movement?.Stop();
        controller.RequestStateChange(CompanionState.FollowingPlayer);

        ResetIdleTimer();
        Debug.Log("[CompanionAutoDeposit] Auto-deposit cancelled");
    }

    private IResourceStorage FindNearestDepot()
    {
        float searchRadius = data?.DepotSearchRadius ?? 50f;

        // Use BuildingRegistry to find nearest storage
        var depot = BuildingRegistry.Instance?.FindNearest<IResourceStorage>(transform.position);

        if (depot == null) return null;

        // Check if within search radius
        var depotTransform = (depot as MonoBehaviour)?.transform;
        if (depotTransform == null) return null;

        float distance = Vector3.Distance(transform.position, depotTransform.position);
        if (distance > searchRadius) return null;

        return depot;
    }

    private void HandleStateChanged(CompanionState previous, CompanionState current)
    {
        // Reset timer when entering follow state
        if (current == CompanionState.FollowingPlayer)
        {
            ResetIdleTimer();
        }

        // Handle being called while depositing
        if (current == CompanionState.BeingCalled && isAutoDepositTriggered)
        {
            CancelAutoDeposit();
        }
    }

    private void HandleResourcesDeposited(int amount)
    {
        // Player deposited resources, reset timer
        ResetIdleTimer();
    }

    private void HandleDestinationReached()
    {
        // Check if we're in the right state
        if (controller.CurrentState == CompanionState.MovingToDepot)
        {
            PerformDeposit();
        }
        else if (controller.CurrentState == CompanionState.ReturningToPlayer)
        {
            FinishReturn();
        }
    }

    private void PerformDeposit()
    {
        if (targetDepot == null || inventory == null)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Cannot deposit: missing depot or inventory");
            ReturnToPlayer();
            return;
        }

        controller.RequestStateChange(CompanionState.Depositing);

        // Transfer resources
        int deposited = inventory.TransferAllTo(targetDepot);

        Debug.Log($"[CompanionAutoDeposit] Deposited {deposited} resources at depot");

        // Small delay then return (could add animation here)
        Invoke(nameof(ReturnToPlayer), 0.5f);
    }

    private void ReturnToPlayer()
    {
        targetDepot = null;
        targetDepotTransform = null;
        isAutoDepositTriggered = false;

        if (controller.PlayerTransform == null)
        {
            controller.RequestStateChange(CompanionState.Idle);
            return;
        }

        controller.RequestStateChange(CompanionState.ReturningToPlayer);
        movement?.SetDestination(controller.PlayerTransform.position);

        Debug.Log("[CompanionAutoDeposit] Returning to player");
    }

    private void FinishReturn()
    {
        controller.RequestStateChange(CompanionState.FollowingPlayer);
        OnAutoDepositCompleted?.Invoke();

        Debug.Log("[CompanionAutoDeposit] Auto-deposit complete, now following");
    }

#if UNITY_EDITOR
    [Button("Trigger Auto-Deposit"), BoxGroup("Debug")]
    private void DebugTriggerAutoDeposit()
    {
        if (Application.isPlaying)
        {
            TriggerAutoDeposit();
        }
    }

    [Button("Cancel Auto-Deposit"), BoxGroup("Debug")]
    private void DebugCancelAutoDeposit()
    {
        if (Application.isPlaying)
        {
            CancelAutoDeposit();
        }
    }

    [Button("Reset Idle Timer"), BoxGroup("Debug")]
    private void DebugResetTimer()
    {
        ResetIdleTimer();
    }

    [ShowInInspector, BoxGroup("Debug"), ReadOnly]
    private float TimeUntilAutoDeposit
    {
        get
        {
            if (data == null) return -1;
            return Mathf.Max(0, data.IdleTimeBeforeAutoDeposit - idleTimer);
        }
    }
#endif
}
```

#### Step 7.2 — Update Companion Prefab

1. Add `CompanionAutoDeposit` component
2. Wire up references

#### Step 7.3 — Test Auto-Deposit Flow

1. Place a ResourceDepot building in scene
2. Activate companion
3. Deposit resources into companion (from player)
4. Wait for idle timer to expire
5. Companion should:
   - Navigate to depot
   - Deposit resources
   - Return to player
   - Resume following

**Test edge cases:**
- Call companion while moving to depot — should cancel and return
- No depot in range — should log warning and reset
- Depot destroyed mid-path — should handle gracefully

### ✅ Validation Checklist

- [ ] `CompanionAutoDeposit.cs` compiles without errors
- [ ] Idle timer counts up when following with resources
- [ ] Timer resets on player deposit
- [ ] Auto-deposit triggers after timeout
- [ ] Finds nearest depot via BuildingRegistry
- [ ] Navigates to depot successfully
- [ ] Deposits resources at depot
- [ ] Returns to player after deposit
- [ ] Calling companion cancels auto-deposit
- [ ] Events fire at appropriate times

### What "Done" Looks Like

Complete auto-deposit loop: Player deposits resources → Companion waits → Companion walks to depot → Deposits → Returns to player → Follows again. Player can interrupt by calling.

---

## Phase 8 — Integration with BuildingRegistry & Resource Buildings

### Goal
Ensure robust integration with the building system. Handle edge cases like depot destruction.

### What is Implemented
- Registry event subscriptions
- Depot validation during navigation
- Graceful handling of destroyed depots

### What is Intentionally Deferred
- Depot prioritization (prefer closer, prefer specific types)
- Visual indicators for target depot

### Steps

#### Step 8.1 — Subscribe to Registry Events

Update `CompanionAutoDeposit.cs`:

```csharp
// Add to Start():
if (BuildingRegistry.Instance != null)
{
    BuildingRegistry.Instance.OnBuildingRemoved += HandleBuildingRemoved;
}

// Add to OnDestroy():
if (BuildingRegistry.Instance != null)
{
    BuildingRegistry.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
}

// Add handler:
private void HandleBuildingRemoved(Building building)
{
    // Check if our target depot was removed
    if (isAutoDepositTriggered && targetDepot != null)
    {
        if ((targetDepot as MonoBehaviour)?.gameObject == building.gameObject)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Target depot was destroyed!");

            // Try to find another depot
            IResourceStorage newDepot = FindNearestDepot();

            if (newDepot != null)
            {
                targetDepot = newDepot;
                targetDepotTransform = (newDepot as MonoBehaviour)?.transform;
                movement?.SetDestination(targetDepotTransform.position);
                Debug.Log("[CompanionAutoDeposit] Redirecting to new depot");
            }
            else
            {
                // No depot available, return to player
                CancelAutoDeposit();
            }
        }
    }
}
```

#### Step 8.2 — Add Depot Validation During Movement

Update the `Update()` method in `CompanionAutoDeposit.cs`:

```csharp
private void Update()
{
    if (controller == null || !controller.IsActive) return;

    // Check for state-specific updates
    switch (controller.CurrentState)
    {
        case CompanionState.FollowingPlayer:
            UpdateIdleTimer();
            break;

        case CompanionState.MovingToDepot:
            ValidateTargetDepot();
            break;
    }
}

private void ValidateTargetDepot()
{
    // Ensure depot still exists and is valid
    if (targetDepot == null || targetDepotTransform == null)
    {
        Debug.LogWarning("[CompanionAutoDeposit] Target depot became invalid");
        RetryOrCancel();
        return;
    }

    // Check if depot is still a valid storage
    var building = targetDepotTransform.GetComponent<Building>();
    if (building != null && !building.IsOperational)
    {
        Debug.LogWarning("[CompanionAutoDeposit] Target depot is not operational");
        RetryOrCancel();
    }
}

private void RetryOrCancel()
{
    // Try to find another depot
    IResourceStorage newDepot = FindNearestDepot();

    if (newDepot != null && newDepot != targetDepot)
    {
        targetDepot = newDepot;
        targetDepotTransform = (newDepot as MonoBehaviour)?.transform;
        movement?.SetDestination(targetDepotTransform.position);
        Debug.Log("[CompanionAutoDeposit] Found alternative depot");
    }
    else
    {
        CancelAutoDeposit();
    }
}
```

#### Step 8.3 — Test Building Integration

1. Place multiple ResourceDepots in scene
2. Destroy the target depot while companion is moving to it
3. Companion should redirect to another depot
4. If no depots remain, should cancel and return

**Test scenarios:**
- Single depot destroyed → Cancel auto-deposit
- Multiple depots, nearest destroyed → Redirect to next nearest
- Depot made non-operational → Redirect or cancel

### ✅ Validation Checklist

- [ ] Subscribes to `BuildingRegistry.OnBuildingRemoved`
- [ ] Detects when target depot is destroyed
- [ ] Finds alternative depot if available
- [ ] Cancels gracefully if no depot available
- [ ] Validates depot during navigation
- [ ] Handles non-operational buildings

### What "Done" Looks Like

Companion robustly handles depot changes. Destroying the target depot doesn't cause errors — companion either finds another depot or returns to player.

---

## Phase 9 — Validation, Debugging Tools & Common Failure Cases

### Goal
Add comprehensive debugging tools and handle edge cases.

### What is Implemented
- `CompanionDebugPanel.cs` — Runtime debug UI
- Comprehensive logging
- Common failure case handling

### What is Intentionally Deferred
- Production-ready error recovery
- Analytics

### Steps

#### Step 9.1 — Create CompanionDebugPanel.cs

Create `Assets/Scripts/Companion/CompanionDebugPanel.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Runtime debug panel for companion system.
/// Shows state, inventory, and provides test controls.
/// </summary>
public class CompanionDebugPanel : MonoBehaviour
{
    [SerializeField] private CompanionController controller;
    [SerializeField] private CompanionInventory inventory;
    [SerializeField] private CompanionAutoDeposit autoDeposit;
    [SerializeField] private CompanionCallHandler callHandler;

    [SerializeField] private bool showDebugWindow = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private Rect windowRect = new Rect(10, 10, 300, 400);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showDebugWindow = !showDebugWindow;
        }
    }

    private void OnGUI()
    {
        if (!showDebugWindow) return;
        if (controller == null) return;

        windowRect = GUI.Window(0, windowRect, DrawWindow, "Companion Debug");
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginVertical();

        // State info
        GUILayout.Label($"State: {controller.CurrentState}");
        GUILayout.Label($"Active: {controller.IsActive}");
        GUILayout.Label($"Distance to Player: {controller.GetDistanceToPlayer():F1}m");

        GUILayout.Space(10);

        // Inventory info
        if (inventory != null)
        {
            GUILayout.Label("--- Inventory ---");
            GUILayout.Label($"Total Resources: {inventory.GetTotalResourceCount()}");

            foreach (var kvp in inventory.GetAllResources())
            {
                GUILayout.Label($"  {kvp.Key}: {kvp.Value}");
            }
        }

        GUILayout.Space(10);

        // Controls
        GUILayout.Label("--- Controls ---");

        if (GUILayout.Button("Call Companion"))
        {
            callHandler?.CallCompanion();
        }

        if (GUILayout.Button("Add Test Resources"))
        {
            inventory?.AddResources(ResourceType.Wood, 10);
            inventory?.AddResources(ResourceType.Stone, 5);
        }

        if (GUILayout.Button("Trigger Auto-Deposit"))
        {
            autoDeposit?.TriggerAutoDeposit();
        }

        if (GUILayout.Button("Cancel Auto-Deposit"))
        {
            autoDeposit?.CancelAutoDeposit();
        }

        if (controller.IsActive && GUILayout.Button("Deactivate"))
        {
            controller.Deactivate();
        }
        else if (!controller.IsActive && GUILayout.Button("Activate"))
        {
            controller.Activate();
        }

        GUILayout.Space(10);

        // Depot info
        GUILayout.Label("--- Depots ---");
        var depots = BuildingRegistry.Instance?.GetAll<IResourceStorage>();
        GUILayout.Label($"Found: {depots?.Count ?? 0}");

        GUILayout.EndVertical();

        GUI.DragWindow();
    }
}
```

#### Step 9.2 — Add Debug Panel to Scene

1. Create empty GameObject `CompanionDebugPanel`
2. Add `CompanionDebugPanel` component
3. Assign companion references
4. Set toggle key (F3 default)

#### Step 9.3 — Document Common Failure Cases

Create a reference table for common issues:

| Symptom | Cause | Solution |
|---------|-------|----------|
| Companion doesn't spawn | No valid NavMesh position | Ensure NavMesh is baked |
| Companion stuck | NavMesh holes or obstacles | Check NavMesh coverage |
| No depot found | No `IResourceStorage` buildings | Place ResourceDepot |
| Depot not detected | Missing `Building` component | Verify depot setup |
| Resources not transferred | Different `ResourceType` enum | Check enum compatibility |
| State machine stuck | Invalid transition | Check `IsValidTransition()` |

#### Step 9.4 — Add Comprehensive Logging

Ensure all major operations log with clear prefixes:

```
[CompanionController] ...
[CompanionInventory] ...
[CompanionMovement] ...
[CompanionCallHandler] ...
[CompanionAutoDeposit] ...
[CompanionInteraction] ...
```

### ✅ Validation Checklist

- [ ] Debug panel shows real-time state
- [ ] All buttons work in debug panel
- [ ] F3 toggles panel visibility
- [ ] Common failure cases are documented
- [ ] All scripts have consistent log prefixes
- [ ] Errors are logged with clear context

### What "Done" Looks Like

A developer can press F3 to see companion state, inventory, and controls. Logs clearly identify which system is logging. Common issues have documented solutions.

---

## Phase 10 — Cleanup, Extension Hooks & Future-Proofing

### Goal
Polish the implementation, add extension points, and prepare for future features.

### What is Implemented
- Code documentation
- Extension events
- Performance considerations
- Input System integration notes

### Steps

#### Step 10.1 — Add Extension Events

Ensure all components expose useful events:

```csharp
// CompanionController
public event Action OnCompanionActivated;
public event Action OnCompanionDeactivated;
public event Action<CompanionState, CompanionState> OnStateChanged;
public event Action<Vector3> OnCompanionMoved;

// CompanionInventory
public event Action<ResourceType, int> OnResourcesChanged;
public event Action OnInventoryCleared;

// CompanionAutoDeposit
public event Action OnAutoDepositStarted;
public event Action OnAutoDepositCompleted;
public event Action OnNoDepotFound;

// CompanionInteraction
public event Action<int> OnResourcesDeposited;
```

#### Step 10.2 — Document Extension Points

Create clear extension points for future features:

```csharp
// Example: Custom depot selection
// Override FindNearestDepot() or create IDepotSelector interface

// Example: Capacity limits
// Add CanAddResources(type, amount) check in CompanionInventory

// Example: Multiple companions
// Create CompanionManager singleton that tracks all companions

// Example: Companion abilities
// Create ICompanionAbility interface with Execute() method
```

#### Step 10.3 — Input System Integration Notes

If using Unity Input System, replace all `Input.GetKeyDown()` calls:

```csharp
// In PlayerInputHandler or dedicated handler:

private PlayerControls controls;

private void Awake()
{
    controls = new PlayerControls();
}

private void OnEnable()
{
    controls.Player.CallCompanion.performed += OnCallCompanion;
    controls.Enable();
}

private void OnDisable()
{
    controls.Player.CallCompanion.performed -= OnCallCompanion;
    controls.Disable();
}

private void OnCallCompanion(InputAction.CallbackContext context)
{
    // Call companion
}
```

#### Step 10.4 — Performance Considerations

- Cache `BuildingRegistry.Instance` reference
- Use squared distance comparisons where possible
- Consider pooling for future multi-companion support
- NavMeshAgent is efficient — no optimization needed

#### Step 10.5 — Final Cleanup

1. Remove any `TODO` comments that are now complete
2. Ensure all public methods have XML documentation
3. Verify all debug buttons only work in Play Mode
4. Remove any unused variables or methods

### ✅ Validation Checklist

- [ ] All events are documented and exposed
- [ ] Extension points are identified
- [ ] Input System migration path is clear
- [ ] No unused code remains
- [ ] All public methods documented
- [ ] Debug tools work correctly
- [ ] No console warnings in normal operation

### What "Done" Looks Like

Clean, documented, extensible code. Events allow UI integration. Future features have clear integration points. Ready for production use.

---

## Common Mistakes & Anti-Patterns

### 1. Direct FindObjectOfType for Depots

**Wrong:**
```csharp
var depots = FindObjectsOfType<ResourceDepot>();
var nearest = depots.OrderBy(d => Vector3.Distance(...)).FirstOrDefault();
```

**Why it's bad:** Slow, doesn't use interface abstraction, breaks polymorphism.

**Correct:** Use `BuildingRegistry.FindNearest<IResourceStorage>()`.

---

### 2. Hardcoding Resource Types

**Wrong:**
```csharp
if (resourceType == ResourceType.Wood || resourceType == ResourceType.Stone)
{
    // Accept
}
```

**Why it's bad:** Not extensible, requires code changes for new resources.

**Correct:** Use `CompanionData.CanAcceptResource()` with configurable list.

---

### 3. State Changes Without Validation

**Wrong:**
```csharp
currentState = CompanionState.Depositing; // Direct assignment
```

**Why it's bad:** Bypasses transition validation, doesn't fire events.

**Correct:** Use `RequestStateChange()` or `ForceState()` (sparingly).

---

### 4. Ignoring NavMesh Validation

**Wrong:**
```csharp
transform.position = targetPosition; // Direct teleport
```

**Why it's bad:** Can place companion in invalid position, breaking navigation.

**Correct:** Use `NavMesh.SamplePosition()` then `NavMeshAgent.Warp()`.

---

### 5. Ticking Timer in Wrong States

**Wrong:**
```csharp
void Update()
{
    idleTimer += Time.deltaTime; // Always ticking
}
```

**Why it's bad:** Timer should only run when following with resources.

**Correct:** Check state and inventory before incrementing timer.

---

### 6. Not Handling Destroyed References

**Wrong:**
```csharp
void PerformDeposit()
{
    targetDepot.TryDeposit(...); // No null check
}
```

**Why it's bad:** Depot might be destroyed between navigation start and arrival.

**Correct:** Validate references at each step, subscribe to destruction events.

---

### 7. Blocking on Resource Transfer

**Wrong:**
```csharp
while (inventory.HasAnyResources())
{
    DepositOne();
    Thread.Sleep(100); // NEVER DO THIS
}
```

**Why it's bad:** Blocks main thread, freezes game.

**Correct:** Transfer all at once, or use coroutine with yield.

---

## Quick Reference

### File Locations

```
Assets/Scripts/Companion/
├── CompanionController.cs       🆕 Phase 2
├── CompanionState.cs            🆕 Phase 4 (or inline)
├── CompanionInventory.cs        🆕 Phase 3
├── CompanionMovement.cs         🆕 Phase 5
├── CompanionCallHandler.cs      🆕 Phase 6
├── CompanionInteraction.cs      🆕 Phase 6
├── CompanionAutoDeposit.cs      🆕 Phase 7
├── CompanionDebugPanel.cs       🆕 Phase 9
└── Data/
    └── CompanionData.cs         🆕 Phase 1

Assets/Data/Companion/
└── DefaultCompanionData.asset   🆕 Phase 1

Assets/Prefabs/Companion/
└── Companion.prefab             🆕 Phase 0
```

### Key Input Bindings

| Action | Key | Effect |
|--------|-----|--------|
| Call Companion | C | Spawn/summon companion near player |
| Interact | E (or existing) | Deposit resources into companion |
| Debug Panel | F3 | Toggle debug window |

### State Transitions Summary

| From | To | Trigger |
|------|----|----|
| Inactive | BeingCalled | Player calls |
| BeingCalled | FollowingPlayer | Spawn complete |
| FollowingPlayer | MovingToDepot | Idle timer expires |
| MovingToDepot | Depositing | Arrival at depot |
| Depositing | ReturningToPlayer | Deposit complete |
| ReturningToPlayer | FollowingPlayer | Arrival at player |
| Any | BeingCalled | Player calls (interrupt) |
| Any | Inactive | Deactivate |

### Component Dependencies

```
CompanionController (root)
├── NavMeshAgent (required)
├── CompanionInventory
│   └── ResourceInventory (internal)
├── CompanionMovement
├── CompanionCallHandler
├── CompanionInteraction (IInteractable)
└── CompanionAutoDeposit
    └── Uses: BuildingRegistry
```

### Integration Points

| System | How Companion Integrates |
|--------|-------------------------|
| BuildingRegistry | `FindNearest<IResourceStorage>()` |
| PlayerResourceInventory | `IResourceHolder.TransferAllFrom()` |
| IResourceStorage | `TryDeposit()` at depot |
| IInteractable | Player interaction system |
| NavMesh | Movement and spawn validation |

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| Initial | 1.0 | Complete implementation guide |

---

*This document is authoritative for companion system implementation. Refer to `BuildingSystem_IterativeImplementation.md` for building system integration details.*
