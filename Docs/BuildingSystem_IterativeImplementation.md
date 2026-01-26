# Building System — Iterative Implementation Guide

> **StillOrbit** — Step-by-step implementation playbook for the Building Placement System

---

## Document Purpose

This document answers:

> "What exactly do I build first, then second, then third — and how do I test each step — until the building system is complete and stable?"

This is **not** a theory document. It is an **execution playbook**.

**Authoritative reference:** `CompanionAndBuildingSystemsGuide.md`

---

## Table of Contents

1. [Phase 0 — Prerequisites & Project Setup](#phase-0--prerequisites--project-setup)
2. [Phase 1 — Core Building Data & Interfaces (Verify)](#phase-1--core-building-data--interfaces-verify)
3. [Phase 2 — BuildingDatabase ScriptableObject](#phase-2--buildingdatabase-scriptableobject)
4. [Phase 3 — BuildModeController Skeleton](#phase-3--buildmodecontroller-skeleton)
5. [Phase 4 — Basic Build Menu UI](#phase-4--basic-build-menu-ui)
6. [Phase 5 — BuildingGhostController (Preview)](#phase-5--buildinghostcontroller-preview)
7. [Phase 6 — Placement Validation & Collision Rules](#phase-6--placement-validation--collision-rules)
8. [Phase 7 — BuildPlacementCamera](#phase-7--buildplacementcamera)
9. [Phase 8 — Resource Cost Checking & Consumption](#phase-8--resource-cost-checking--consumption)
10. [Phase 9 — Final Placement & Registration](#phase-9--final-placement--registration)
11. [Phase 10 — Integration Hooks (Companion & Resources)](#phase-10--integration-hooks-companion--resources)
12. [Phase 11 — Refactoring, Cleanup, and Hardening](#phase-11--refactoring-cleanup-and-hardening)
13. [Common Mistakes & Anti-Patterns](#common-mistakes--anti-patterns)

---

## Existing Code Reference

Before starting, confirm these files exist in your project (they are already implemented):

| File | Purpose | Location |
|------|---------|----------|
| `Building.cs` | Base building MonoBehaviour | `Assets/Scripts/Buildings/Core/` |
| `BuildingData.cs` | ScriptableObject configuration | `Assets/Scripts/Buildings/Core/` |
| `BuildingRegistry.cs` | Runtime building discovery | `Assets/Scripts/Buildings/Core/` |
| `IBuilding.cs` | Building interface | `Assets/Scripts/Buildings/Interfaces/` |
| `IResourceStorage.cs` | Storage interface | `Assets/Scripts/Buildings/Interfaces/` |
| `ResourceCost` | Struct in `BuildingData.cs` | (inline) |
| `PlayerManager.cs` | Player singleton | `Assets/Scripts/Player/` |
| `PlayerInputHandler.cs` | Input handling | `Assets/Scripts/Player/` |
| `PlayerResourceInventory.cs` | Player resources | `Assets/Scripts/Resources/` |

---

## Phase 0 — Prerequisites & Project Setup

### Goal
Ensure the project has the correct folder structure, layers, and dependencies before writing any new code.

### What is Implemented
- Folder structure for new placement scripts
- Required Unity layers for validation
- Input action map updates (placeholder)

### What is Intentionally Deferred
- Actual placement scripts
- UI prefabs

### Steps

#### Step 0.1 — Create Folder Structure

Create the following folders if they don't exist:

```
Assets/Scripts/Buildings/Placement/
Assets/Scripts/UI/Building/
Assets/Data/Buildings/
Assets/Prefabs/Buildings/
Assets/Materials/Building/
```

#### Step 0.2 — Define Layers

In **Edit > Project Settings > Tags and Layers**, ensure these layers exist:

| Layer Name | Purpose |
|------------|---------|
| `Ground` | Terrain and surfaces where buildings can be placed |
| `Building` | All placed buildings (for collision detection) |
| `Obstacle` | Objects that block building placement |
| `BuildingGhost` | Preview ghost (to exclude from raycasts) |

**Note:** If `Ground` is already used for something else, you may reuse it. The key is having a layer for valid placement surfaces.

#### Step 0.3 — Create Placeholder Materials

Create two materials in `Assets/Materials/Building/`:

1. **`ValidPlacement.mat`**
   - Shader: `Standard` or `Universal Render Pipeline/Lit`
   - Rendering Mode: `Transparent`
   - Color: Green with ~50% alpha (`#00FF0080`)

2. **`InvalidPlacement.mat`**
   - Same settings
   - Color: Red with ~50% alpha (`#FF000080`)

These will be replaced with better shaders later. Simple is fine for now.

#### Step 0.4 — Reserve Input Actions (Placeholder)

Open your `PlayerControls.inputactions` asset and add these actions to the `Player` action map:

| Action Name | Type | Binding |
|-------------|------|---------|
| `ToggleBuildMode` | Button | `B` |
| `RotateBuilding` | Button | `R` |
| `ConfirmPlacement` | Button | `Mouse/leftButton` |
| `CancelPlacement` | Button | `Escape` |

**Regenerate the C# class** after saving.

> **Assumption:** The project uses Unity's Input System package. If using the legacy system, you'll handle these in `Update()` with `Input.GetKeyDown()`.

### ✅ Validation Checklist

- [ ] Folder structure exists
- [ ] Layers are defined in Project Settings
- [ ] Placeholder materials created
- [ ] Input actions added (or noted for legacy input)

### What "Done" Looks Like

You can see the folders in the Project window, layers in Project Settings, and materials in the Materials folder. No runtime behavior yet.

---

## Phase 1 — Core Building Data & Interfaces (Verify)

### Goal
Confirm the existing building infrastructure is complete and matches the architecture document.

### What is Implemented
- Verification that `BuildingData` has `ConstructionCosts`
- Verification that `Building` auto-registers with `BuildingRegistry`

### What is Intentionally Deferred
- Any modifications (only verify)

### Steps

#### Step 1.1 — Verify BuildingData.cs

Open `Assets/Scripts/Buildings/Core/BuildingData.cs` and confirm:

```csharp
[BoxGroup("Construction")]
[SerializeField] private List<ResourceCost> constructionCosts = new List<ResourceCost>();

public IReadOnlyList<ResourceCost> ConstructionCosts => constructionCosts;
```

The `ResourceCost` struct should exist with:

```csharp
[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    [Min(1)]
    public int amount = 1;
}
```

✅ This already exists in the codebase.

#### Step 1.2 — Verify Building Auto-Registration

Open `Assets/Scripts/Buildings/Core/Building.cs` and confirm:

- `Start()` calls `BuildingRegistry.Instance.Register(this);`
- `OnDestroy()` calls `BuildingRegistry.Instance.Unregister(this);`

✅ This already exists in the codebase.

#### Step 1.3 — Verify BuildingRegistry Query Methods

Open `Assets/Scripts/Buildings/Core/BuildingRegistry.cs` and confirm these methods exist:

- `GetAll<T>()` — Returns all buildings implementing interface `T`
- `FindNearest<T>(Vector3 position)` — Returns nearest building with capability
- `FindWithinRadius<T>(Vector3 position, float radius)` — Returns all within radius

✅ These already exist in the codebase.

### ✅ Validation Checklist

- [ ] `BuildingData` has `ConstructionCosts` property
- [ ] `Building` registers/unregisters automatically
- [ ] `BuildingRegistry` has query methods

### What "Done" Looks Like

All existing code is confirmed to match the architecture. No changes needed.

---

## Phase 2 — BuildingDatabase ScriptableObject

### Goal
Create a central registry of all available building types that can be constructed.

### What is Implemented
- `BuildingDatabase.cs` ScriptableObject
- One database asset with at least one building entry

### What is Intentionally Deferred
- Filtering by category
- Unlocking/progression system

### Steps

#### Step 2.1 — Create BuildingDatabase.cs

Create `Assets/Scripts/Buildings/Placement/BuildingDatabase.cs`:

```csharp
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Central registry of all building types available for construction.
/// Used by the build menu to display options.
/// </summary>
[CreateAssetMenu(fileName = "BuildingDatabase", menuName = "StillOrbit/Buildings/Building Database")]
public class BuildingDatabase : ScriptableObject
{
    [BoxGroup("Buildings")]
    [ListDrawerSettings(ShowFoldout = true)]
    [SerializeField]
    private List<BuildingData> buildings = new List<BuildingData>();

    /// <summary>
    /// All buildings available for construction.
    /// </summary>
    public IReadOnlyList<BuildingData> AllBuildings => buildings;

    /// <summary>
    /// Get a building by its ID.
    /// </summary>
    public BuildingData GetById(string buildingId)
    {
        foreach (var building in buildings)
        {
            if (building != null && building.BuildingId == buildingId)
            {
                return building;
            }
        }
        return null;
    }

    /// <summary>
    /// Get the count of available buildings.
    /// </summary>
    public int Count => buildings.Count;

#if UNITY_EDITOR
    [Button("Validate Entries"), BoxGroup("Debug")]
    private void ValidateEntries()
    {
        int nullCount = 0;
        int missingPrefab = 0;

        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] == null)
            {
                Debug.LogWarning($"[BuildingDatabase] Null entry at index {i}");
                nullCount++;
            }
            else if (buildings[i].BuildingPrefab == null)
            {
                Debug.LogWarning($"[BuildingDatabase] '{buildings[i].BuildingName}' has no prefab assigned");
                missingPrefab++;
            }
        }

        if (nullCount == 0 && missingPrefab == 0)
        {
            Debug.Log($"[BuildingDatabase] All {buildings.Count} entries valid!");
        }
    }
#endif
}
```

#### Step 2.2 — Create the Database Asset

1. Right-click in `Assets/Data/Buildings/`
2. Select **Create > StillOrbit > Buildings > Building Database**
3. Name it `BuildingDatabase.asset`

#### Step 2.3 — Add Existing Building Data

If you have an existing `ResourceDepotData.asset` or similar:

1. Select `BuildingDatabase.asset`
2. In the Inspector, add entries to the Buildings list
3. Drag your existing BuildingData assets into the slots

**If no BuildingData assets exist yet**, create a test one:

1. Right-click in `Assets/Data/Buildings/`
2. Select **Create > StillOrbit > Buildings > Building Data**
3. Name it `Test Building.asset`
4. Set a name, description, and assign a placeholder prefab (a cube is fine)
5. Add it to the database

### ✅ Validation Checklist

- [ ] `BuildingDatabase.cs` compiles without errors
- [ ] `BuildingDatabase.asset` exists in `Assets/Data/Buildings/`
- [ ] At least one BuildingData entry is in the database
- [ ] Clicking "Validate Entries" button shows no errors

### What "Done" Looks Like

You have a ScriptableObject asset that lists all buildable structures. The database is ready for the UI to read from.

---

## Phase 3 — BuildModeController Skeleton

### Goal
Create the central coordinator that manages build mode state transitions. This phase implements only the state machine — no UI, ghost, or camera yet.

### What is Implemented
- `BuildModeController.cs` with state enum and transitions
- Input handling for toggling build mode
- Player locomotion disabled during build mode

### What is Intentionally Deferred
- Menu UI
- Ghost preview
- Camera transitions
- Actual placement

### Steps

#### Step 3.1 — Create BuildModeController.cs

Create `Assets/Scripts/Buildings/Placement/BuildModeController.cs`:

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Central coordinator for the building placement system.
/// Manages mode transitions and delegates to subsystems.
/// </summary>
public class BuildModeController : MonoBehaviour
{
    public enum BuildModeState
    {
        Inactive,   // Normal gameplay
        MenuOpen,   // Build menu visible, selecting building
        Placing     // Holding building ghost, positioning
    }

    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildingDatabase buildingDatabase;

    [BoxGroup("References")]
    [SerializeField] private PlayerManager playerManager;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private BuildModeState currentState = BuildModeState.Inactive;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private BuildingData selectedBuilding;

    // Public accessors
    public bool IsInBuildMode => currentState != BuildModeState.Inactive;
    public BuildModeState CurrentState => currentState;
    public BuildingData SelectedBuilding => selectedBuilding;
    public BuildingDatabase Database => buildingDatabase;

    // Events
    public event Action<BuildModeState> OnStateChanged;
    public event Action<BuildingData> OnBuildingSelected;
    public event Action<Building> OnBuildingPlaced;

    private void Awake()
    {
        if (playerManager == null)
        {
            playerManager = PlayerManager.Instance;
        }
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Toggle build mode with B key (temporary - will be replaced by Input System)
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildMode();
        }

        // Cancel/back with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == BuildModeState.Placing)
            {
                // Go back to menu
                EnterMenuState();
            }
            else if (currentState == BuildModeState.MenuOpen)
            {
                // Exit build mode entirely
                ExitBuildMode();
            }
        }
    }

    /// <summary>
    /// Toggle between normal gameplay and build mode.
    /// </summary>
    public void ToggleBuildMode()
    {
        if (currentState == BuildModeState.Inactive)
        {
            EnterBuildMode();
        }
        else
        {
            ExitBuildMode();
        }
    }

    /// <summary>
    /// Enter build mode and show the build menu.
    /// </summary>
    public void EnterBuildMode()
    {
        if (currentState != BuildModeState.Inactive)
        {
            Debug.LogWarning("[BuildModeController] Already in build mode");
            return;
        }

        EnterMenuState();
        Debug.Log("[BuildModeController] Entered build mode");
    }

    /// <summary>
    /// Exit build mode completely and return to normal gameplay.
    /// </summary>
    public void ExitBuildMode()
    {
        if (currentState == BuildModeState.Inactive) return;

        SetPlayerControlsEnabled(true);
        selectedBuilding = null;

        ChangeState(BuildModeState.Inactive);
        Debug.Log("[BuildModeController] Exited build mode");
    }

    /// <summary>
    /// Select a building to place. Transitions to Placing state.
    /// </summary>
    public void SelectBuilding(BuildingData building)
    {
        if (building == null)
        {
            Debug.LogWarning("[BuildModeController] Cannot select null building");
            return;
        }

        selectedBuilding = building;
        OnBuildingSelected?.Invoke(building);

        ChangeState(BuildModeState.Placing);
        Debug.Log($"[BuildModeController] Selected building: {building.BuildingName}");
    }

    /// <summary>
    /// Cancel current placement and return to menu.
    /// </summary>
    public void CancelPlacement()
    {
        if (currentState != BuildModeState.Placing) return;

        EnterMenuState();
        Debug.Log("[BuildModeController] Cancelled placement");
    }

    private void EnterMenuState()
    {
        SetPlayerControlsEnabled(false);
        selectedBuilding = null;

        ChangeState(BuildModeState.MenuOpen);
    }

    private void ChangeState(BuildModeState newState)
    {
        if (currentState == newState) return;

        BuildModeState previousState = currentState;
        currentState = newState;

        OnStateChanged?.Invoke(newState);
        Debug.Log($"[BuildModeController] State: {previousState} -> {newState}");
    }

    private void SetPlayerControlsEnabled(bool enabled)
    {
        if (playerManager == null) return;

        // Disable locomotion during build mode
        if (playerManager.LocomotionController != null)
        {
            playerManager.LocomotionController.enabled = enabled;
        }

        // Future: disable other controls (combat, interaction) as needed
    }

#if UNITY_EDITOR
    [Button("Toggle Build Mode"), BoxGroup("Debug")]
    private void DebugToggle()
    {
        if (Application.isPlaying)
        {
            ToggleBuildMode();
        }
    }

    [Button("Log State"), BoxGroup("Debug")]
    private void DebugLogState()
    {
        Debug.Log($"[BuildModeController] Current state: {currentState}, Selected: {selectedBuilding?.BuildingName ?? "None"}");
    }
#endif
}
```

#### Step 3.2 — Add BuildModeController to Scene

1. Create a new empty GameObject named `BuildModeController`
2. Add the `BuildModeController` component
3. Assign the `BuildingDatabase` asset reference
4. Assign `PlayerManager` reference (or leave null to use singleton)

#### Step 3.3 — Test State Transitions

1. Enter Play Mode
2. Press `B` — should log "Entered build mode"
3. Press `Escape` — should log "Exited build mode"
4. Press `B` again — should enter build mode
5. Observe player controls: locomotion should be disabled while in build mode

### ✅ Validation Checklist

- [ ] `BuildModeController.cs` compiles without errors
- [ ] Controller is in scene with references assigned
- [ ] Pressing B toggles build mode on/off
- [ ] Player locomotion is disabled during build mode
- [ ] Escape exits build mode from MenuOpen state
- [ ] Console logs show state transitions

### What "Done" Looks Like

You can press B to enter build mode, see the state change in logs, observe player movement stops, and press Escape to exit. No visual UI yet.

---

## Phase 4 — Basic Build Menu UI

### Goal
Create a minimal UI that displays available buildings and allows selection.

### What is Implemented
- `BuildMenuUI.cs` — Panel that lists buildings
- `BuildingSlotUI.cs` — Individual building button
- Basic prefabs for menu layout

### What is Intentionally Deferred
- Affordability checking (grayed out if can't afford)
- Pretty styling
- Tooltips
- Categories/filtering

### Steps

#### Step 4.1 — Create BuildingSlotUI.cs

Create `Assets/Scripts/UI/Building/BuildingSlotUI.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI element representing a single building in the build menu.
/// </summary>
public class BuildingSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;

    [Header("Colors")]
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private BuildingData buildingData;
    private Action<BuildingData> onClickCallback;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    /// <summary>
    /// Initialize the slot with building data.
    /// </summary>
    public void Setup(BuildingData data, Action<BuildingData> onClick)
    {
        buildingData = data;
        onClickCallback = onClick;

        if (iconImage != null && data.Icon != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (nameText != null)
        {
            nameText.text = data.BuildingName;
        }
    }

    /// <summary>
    /// Update visual state based on whether player can afford this building.
    /// </summary>
    public void SetAffordable(bool canAfford)
    {
        if (button != null)
        {
            button.interactable = canAfford;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = canAfford ? affordableColor : unaffordableColor;
        }

        if (nameText != null)
        {
            nameText.color = canAfford ? Color.white : Color.gray;
        }
    }

    private void HandleClick()
    {
        if (buildingData != null)
        {
            onClickCallback?.Invoke(buildingData);
        }
    }
}
```

#### Step 4.2 — Create BuildMenuUI.cs

Create `Assets/Scripts/UI/Building/BuildMenuUI.cs`:

```csharp
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// UI panel displaying available buildings for construction.
/// </summary>
public class BuildMenuUI : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private Transform slotContainer;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private BuildingSlotUI slotPrefab;

    [BoxGroup("References")]
    [SerializeField] private BuildModeController buildModeController;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private List<BuildingSlotUI> activeSlots = new List<BuildingSlotUI>();

    public event Action<BuildingData> OnBuildingSelected;

    private void Start()
    {
        // Find controller if not assigned
        if (buildModeController == null)
        {
            buildModeController = FindObjectOfType<BuildModeController>();
        }

        // Subscribe to state changes
        if (buildModeController != null)
        {
            buildModeController.OnStateChanged += HandleStateChanged;
        }

        // Start hidden
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (buildModeController != null)
        {
            buildModeController.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(BuildModeController.BuildModeState newState)
    {
        if (newState == BuildModeController.BuildModeState.MenuOpen)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// Show the build menu and populate with available buildings.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshSlots();
    }

    /// <summary>
    /// Hide the build menu.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RefreshSlots()
    {
        // Clear existing slots
        foreach (var slot in activeSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        activeSlots.Clear();

        // Get database from controller
        if (buildModeController == null || buildModeController.Database == null)
        {
            Debug.LogWarning("[BuildMenuUI] No building database available");
            return;
        }

        // Create slot for each building
        foreach (var building in buildModeController.Database.AllBuildings)
        {
            if (building == null) continue;

            BuildingSlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.Setup(building, HandleSlotClicked);
            slot.SetAffordable(true); // TODO: Check actual affordability

            activeSlots.Add(slot);
        }

        Debug.Log($"[BuildMenuUI] Populated {activeSlots.Count} building slots");
    }

    private void HandleSlotClicked(BuildingData building)
    {
        Debug.Log($"[BuildMenuUI] Slot clicked: {building.BuildingName}");

        OnBuildingSelected?.Invoke(building);

        // Tell controller to enter placement mode
        if (buildModeController != null)
        {
            buildModeController.SelectBuilding(building);
        }
    }
}
```

#### Step 4.3 — Create UI Prefabs

**Create BuildingSlotUI Prefab:**

1. In a Canvas, create UI > Button (TextMeshPro)
2. Structure:
   ```
   BuildingSlot (Button)
   ├── Background (Image)
   ├── Icon (Image)
   └── Name (TextMeshProUGUI)
   ```
3. Set size to ~100x120
4. Add `BuildingSlotUI` component
5. Wire up the references (iconImage, nameText, button, backgroundImage)
6. Save as prefab in `Assets/Prefabs/UI/Building/BuildingSlotUI.prefab`

**Create BuildMenuUI Panel:**

1. In Canvas, create UI > Panel named "BuildMenuPanel"
2. Add a child with Horizontal Layout Group or Grid Layout Group named "SlotContainer"
3. Configure layout (cell size, spacing)
4. Add `BuildMenuUI` component to panel
5. Assign `slotContainer` and `slotPrefab`
6. Assign `buildModeController` reference

#### Step 4.4 — Wire Up Controller to UI

In your `BuildModeController`, the UI should now auto-show/hide based on state changes. No additional wiring needed if `BuildMenuUI` finds the controller.

#### Step 4.5 — Test the Menu

1. Enter Play Mode
2. Press `B` — Build menu should appear
3. Click a building slot — should log selection and hide menu
4. Press `B` again, then `Escape` — menu should close

### ✅ Validation Checklist

- [ ] `BuildingSlotUI.cs` and `BuildMenuUI.cs` compile
- [ ] Prefabs created with correct structure
- [ ] UI references wired in Inspector
- [ ] Pressing B shows menu with building list
- [ ] Clicking a slot logs selection
- [ ] Menu hides when entering Placing state
- [ ] Menu hides when pressing Escape

### What "Done" Looks Like

A functional (ugly) menu appears when pressing B, shows all buildings from the database, and clicking one transitions to Placing state.

---

## Phase 5 — BuildingGhostController (Preview)

### Goal
Create the placement preview (ghost) that follows the mouse cursor.

### What is Implemented
- `BuildingGhostController.cs` — Manages ghost instantiation and positioning
- Ghost follows mouse raycast position
- Ghost rotates with R key

### What is Intentionally Deferred
- Valid/invalid material switching (just position for now)
- Collision validation

### Steps

#### Step 5.1 — Create BuildingGhostController.cs

Create `Assets/Scripts/Buildings/Placement/BuildingGhostController.cs`:

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages the building placement preview (ghost).
/// Instantiates a transparent copy of the building prefab that follows the cursor.
/// </summary>
public class BuildingGhostController : MonoBehaviour
{
    [BoxGroup("Settings")]
    [SerializeField] private Material ghostMaterial;

    [BoxGroup("Settings")]
    [SerializeField] private float rotationStep = 45f;

    [BoxGroup("Raycast")]
    [SerializeField] private LayerMask groundLayerMask;

    [BoxGroup("Raycast")]
    [SerializeField] private float maxRaycastDistance = 100f;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private GameObject currentGhost;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private BuildingData currentBuildingData;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private float currentRotationY = 0f;

    private Camera mainCamera;
    private Material[] originalMaterials;

    public bool HasGhost => currentGhost != null;
    public Vector3 GhostPosition => currentGhost != null ? currentGhost.transform.position : Vector3.zero;
    public Quaternion GhostRotation => Quaternion.Euler(0f, currentRotationY, 0f);
    public BuildingData CurrentBuildingData => currentBuildingData;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (currentGhost == null) return;

        UpdateGhostPosition();
        HandleRotationInput();
    }

    /// <summary>
    /// Show a ghost for the specified building.
    /// </summary>
    public void ShowGhost(BuildingData building)
    {
        if (building == null || building.BuildingPrefab == null)
        {
            Debug.LogWarning("[BuildingGhostController] Cannot show ghost: null building or prefab");
            return;
        }

        ClearGhost();

        currentBuildingData = building;
        currentRotationY = 0f;

        // Instantiate the prefab
        currentGhost = Instantiate(building.BuildingPrefab);
        currentGhost.name = $"Ghost_{building.BuildingName}";

        // Disable functional components
        DisableGhostComponents(currentGhost);

        // Apply ghost material to all renderers
        ApplyGhostMaterial(currentGhost);

        // Set layer to avoid self-raycasting
        SetLayerRecursive(currentGhost, LayerMask.NameToLayer("BuildingGhost"));

        Debug.Log($"[BuildingGhostController] Showing ghost for: {building.BuildingName}");
    }

    /// <summary>
    /// Remove the current ghost.
    /// </summary>
    public void ClearGhost()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }

        currentBuildingData = null;
        currentRotationY = 0f;
    }

    /// <summary>
    /// Rotate the ghost by one step.
    /// </summary>
    public void RotateGhost()
    {
        currentRotationY = (currentRotationY + rotationStep) % 360f;

        if (currentGhost != null)
        {
            currentGhost.transform.rotation = GhostRotation;
        }

        Debug.Log($"[BuildingGhostController] Rotated to: {currentRotationY}°");
    }

    private void UpdateGhostPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayerMask))
        {
            currentGhost.transform.position = hit.point;
            currentGhost.transform.rotation = GhostRotation;
        }
    }

    private void HandleRotationInput()
    {
        // Temporary: R key to rotate (will be replaced by Input System)
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateGhost();
        }
    }

    private void DisableGhostComponents(GameObject ghost)
    {
        // Disable Building component
        foreach (var building in ghost.GetComponentsInChildren<Building>(true))
        {
            building.enabled = false;
        }

        // Disable colliders (keep trigger colliders for visualization if any)
        foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.isTrigger)
            {
                collider.enabled = false;
            }
        }

        // Disable NavMeshObstacle
        foreach (var obstacle in ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
        {
            obstacle.enabled = false;
        }

        // Disable any scripts that shouldn't run on ghost
        foreach (var monoBehaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // Keep only essential components
            if (!(monoBehaviour is Transform))
            {
                // Disable most scripts, but keep renderers working
                if (!(monoBehaviour is Renderer))
                {
                    monoBehaviour.enabled = false;
                }
            }
        }
    }

    private void ApplyGhostMaterial(GameObject ghost)
    {
        if (ghostMaterial == null) return;

        foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = ghostMaterial;
            }
            renderer.materials = newMaterials;
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

#if UNITY_EDITOR
    [Button("Clear Ghost"), BoxGroup("Debug")]
    private void DebugClearGhost()
    {
        if (Application.isPlaying)
        {
            ClearGhost();
        }
    }
#endif
}
```

#### Step 5.2 — Update BuildModeController to Use Ghost

Add ghost controller integration to `BuildModeController.cs`:

```csharp
// Add to fields:
[BoxGroup("References")]
[SerializeField] private BuildingGhostController ghostController;

// Modify SelectBuilding():
public void SelectBuilding(BuildingData building)
{
    if (building == null)
    {
        Debug.LogWarning("[BuildModeController] Cannot select null building");
        return;
    }

    selectedBuilding = building;
    OnBuildingSelected?.Invoke(building);

    // Show ghost
    if (ghostController != null)
    {
        ghostController.ShowGhost(building);
    }

    ChangeState(BuildModeState.Placing);
    Debug.Log($"[BuildModeController] Selected building: {building.BuildingName}");
}

// Modify CancelPlacement():
public void CancelPlacement()
{
    if (currentState != BuildModeState.Placing) return;

    // Clear ghost
    if (ghostController != null)
    {
        ghostController.ClearGhost();
    }

    EnterMenuState();
    Debug.Log("[BuildModeController] Cancelled placement");
}

// Modify ExitBuildMode():
public void ExitBuildMode()
{
    if (currentState == BuildModeState.Inactive) return;

    // Clear ghost
    if (ghostController != null)
    {
        ghostController.ClearGhost();
    }

    SetPlayerControlsEnabled(true);
    selectedBuilding = null;

    ChangeState(BuildModeState.Inactive);
    Debug.Log("[BuildModeController] Exited build mode");
}
```

#### Step 5.3 — Add Ghost Controller to Scene

1. Create empty GameObject `BuildingGhostController`
2. Add `BuildingGhostController` component
3. Assign `ValidPlacement.mat` as the ghost material (for now)
4. Set `groundLayerMask` to include your Ground layer
5. In `BuildModeController`, assign the ghost controller reference

#### Step 5.4 — Test Ghost Preview

1. Enter Play Mode
2. Press `B` to open menu
3. Click a building
4. A transparent ghost should appear and follow mouse
5. Press `R` — ghost should rotate 45°
6. Press `Escape` — ghost should disappear

### ✅ Validation Checklist

- [ ] `BuildingGhostController.cs` compiles
- [ ] Ghost controller is in scene with references assigned
- [ ] Selecting a building shows ghost at cursor
- [ ] Ghost follows mouse (raycast against ground)
- [ ] Pressing R rotates ghost
- [ ] Cancelling or exiting clears ghost
- [ ] Ghost has transparent material applied
- [ ] Ghost components are disabled (no collisions, no logic)

### What "Done" Looks Like

You can see a transparent preview of the building following your mouse, rotating with R. The ghost has no gameplay effect.

---

## Phase 6 — Placement Validation & Collision Rules

### Goal
Add validation to determine if the current ghost position is valid for placement.

### What is Implemented
- `BuildPlacementValidator.cs` — Validates placement positions
- Visual feedback (valid/invalid materials)
- Ground, slope, and obstacle checks

### What is Intentionally Deferred
- NavMesh requirements (optional)
- Building-specific placement rules

### Steps

#### Step 6.1 — Create BuildPlacementValidator.cs

Create `Assets/Scripts/Buildings/Placement/BuildPlacementValidator.cs`:

```csharp
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Validates whether a building can be placed at a given position.
/// </summary>
public class BuildPlacementValidator : MonoBehaviour
{
    [BoxGroup("Ground Check")]
    [SerializeField] private LayerMask groundLayers;

    [BoxGroup("Ground Check")]
    [SerializeField] private float groundCheckDistance = 10f;

    [BoxGroup("Slope")]
    [SerializeField] private float maxSlopeAngle = 30f;

    [BoxGroup("Obstacles")]
    [SerializeField] private LayerMask obstacleLayers;

    [BoxGroup("Obstacles")]
    [SerializeField] private float boundsMargin = 0.9f; // Slightly shrink bounds

    [BoxGroup("NavMesh")]
    [SerializeField] private bool requireNavMesh = false;

    [BoxGroup("NavMesh")]
    [SerializeField] private float navMeshSampleDistance = 2f;

    /// <summary>
    /// Detailed result of placement validation.
    /// </summary>
    public struct ValidationResult
    {
        public bool IsValid;
        public string FailureReason;

        public static ValidationResult Valid() => new ValidationResult { IsValid = true };
        public static ValidationResult Invalid(string reason) => new ValidationResult { IsValid = false, FailureReason = reason };
    }

    /// <summary>
    /// Check if a building can be placed at the given position and rotation.
    /// </summary>
    public bool IsValidPlacement(Vector3 position, Quaternion rotation, BuildingData building)
    {
        return Validate(position, rotation, building).IsValid;
    }

    /// <summary>
    /// Validate placement with detailed result.
    /// </summary>
    public ValidationResult Validate(Vector3 position, Quaternion rotation, BuildingData building)
    {
        if (building == null || building.BuildingPrefab == null)
        {
            return ValidationResult.Invalid("No building data");
        }

        // 1. Check ground exists
        if (!IsOnGround(position))
        {
            return ValidationResult.Invalid("No ground detected");
        }

        // 2. Check slope
        if (!IsSlopeAcceptable(position, out float slopeAngle))
        {
            return ValidationResult.Invalid($"Slope too steep: {slopeAngle:F1}°");
        }

        // 3. Check obstacle collision
        Bounds bounds = CalculatePrefabBounds(building.BuildingPrefab);
        if (HasObstacleCollision(position, rotation, bounds))
        {
            return ValidationResult.Invalid("Blocked by obstacle");
        }

        // 4. Check NavMesh (optional)
        if (requireNavMesh && !IsOnNavMesh(position))
        {
            return ValidationResult.Invalid("Not on NavMesh");
        }

        return ValidationResult.Valid();
    }

    private bool IsOnGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 5f;
        return Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayers);
    }

    private bool IsSlopeAcceptable(Vector3 position, out float angle)
    {
        angle = 0f;
        Vector3 rayStart = position + Vector3.up * 2f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, groundLayers))
        {
            angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle <= maxSlopeAngle;
        }

        return false;
    }

    private bool HasObstacleCollision(Vector3 position, Quaternion rotation, Bounds bounds)
    {
        Vector3 center = position + rotation * bounds.center;
        Vector3 halfExtents = bounds.extents * boundsMargin;

        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, obstacleLayers);

        // Debug visualization
        #if UNITY_EDITOR
        Color debugColor = overlaps.Length > 0 ? Color.red : Color.green;
        DebugDrawBox(center, halfExtents, rotation, debugColor, 0.1f);
        #endif

        return overlaps.Length > 0;
    }

    private bool IsOnNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out _, navMeshSampleDistance, NavMesh.AllAreas);
    }

    private Bounds CalculatePrefabBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // Fallback: 1x1x1 bounds
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        // Make bounds relative to prefab origin
        bounds.center -= prefab.transform.position;

        return bounds;
    }

#if UNITY_EDITOR
    private void DebugDrawBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, Color color, float duration)
    {
        Vector3[] corners = new Vector3[8];

        corners[0] = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[1] = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[2] = center + rotation * new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[3] = center + rotation * new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[4] = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[5] = center + rotation * new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[6] = center + rotation * new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
        corners[7] = center + rotation * new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);

        // Bottom
        Debug.DrawLine(corners[0], corners[1], color, duration);
        Debug.DrawLine(corners[1], corners[2], color, duration);
        Debug.DrawLine(corners[2], corners[3], color, duration);
        Debug.DrawLine(corners[3], corners[0], color, duration);

        // Top
        Debug.DrawLine(corners[4], corners[5], color, duration);
        Debug.DrawLine(corners[5], corners[6], color, duration);
        Debug.DrawLine(corners[6], corners[7], color, duration);
        Debug.DrawLine(corners[7], corners[4], color, duration);

        // Verticals
        Debug.DrawLine(corners[0], corners[4], color, duration);
        Debug.DrawLine(corners[1], corners[5], color, duration);
        Debug.DrawLine(corners[2], corners[6], color, duration);
        Debug.DrawLine(corners[3], corners[7], color, duration);
    }
#endif
}
```

#### Step 6.2 — Update BuildingGhostController for Validation

Modify `BuildingGhostController.cs` to use the validator and update materials:

```csharp
// Add fields:
[BoxGroup("Validation")]
[SerializeField] private BuildPlacementValidator validator;

[BoxGroup("Materials")]
[SerializeField] private Material validPlacementMaterial;

[BoxGroup("Materials")]
[SerializeField] private Material invalidPlacementMaterial;

[BoxGroup("State")]
[ShowInInspector, ReadOnly]
private bool isCurrentPositionValid = false;

public bool IsValidPlacement => isCurrentPositionValid;

// Modify UpdateGhostPosition():
private void UpdateGhostPosition()
{
    if (mainCamera == null)
    {
        mainCamera = Camera.main;
        if (mainCamera == null) return;
    }

    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

    if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayerMask))
    {
        currentGhost.transform.position = hit.point;
        currentGhost.transform.rotation = GhostRotation;

        // Validate placement
        if (validator != null && currentBuildingData != null)
        {
            bool wasValid = isCurrentPositionValid;
            isCurrentPositionValid = validator.IsValidPlacement(hit.point, GhostRotation, currentBuildingData);

            // Update material if validity changed
            if (wasValid != isCurrentPositionValid)
            {
                UpdateGhostMaterial();
            }
        }
        else
        {
            isCurrentPositionValid = true; // No validator = always valid
        }
    }
}

private void UpdateGhostMaterial()
{
    Material targetMaterial = isCurrentPositionValid ? validPlacementMaterial : invalidPlacementMaterial;

    if (targetMaterial == null) return;

    foreach (var renderer in currentGhost.GetComponentsInChildren<Renderer>(true))
    {
        Material[] newMaterials = new Material[renderer.materials.Length];
        for (int i = 0; i < newMaterials.Length; i++)
        {
            newMaterials[i] = targetMaterial;
        }
        renderer.materials = newMaterials;
    }
}

// Modify ShowGhost() to set initial material:
public void ShowGhost(BuildingData building)
{
    // ... existing code ...

    // Initial material (assume invalid until validated)
    isCurrentPositionValid = false;
    UpdateGhostMaterial();

    Debug.Log($"[BuildingGhostController] Showing ghost for: {building.BuildingName}");
}
```

#### Step 6.3 — Add Validator to Scene

1. Create empty GameObject `BuildPlacementValidator`
2. Add `BuildPlacementValidator` component
3. Configure layers:
   - Ground Layers: Include `Ground`
   - Obstacle Layers: Include `Building`, `Obstacle`
4. Set `maxSlopeAngle` (e.g., 30)
5. In `BuildingGhostController`, assign the validator reference
6. Assign both valid and invalid materials

#### Step 6.4 — Test Validation

1. Enter Play Mode
2. Press `B`, select a building
3. Move ghost over flat ground — should be green
4. Move ghost over steep slope — should turn red
5. Move ghost over existing obstacles — should turn red
6. Ghost should switch materials as you move

### ✅ Validation Checklist

- [ ] `BuildPlacementValidator.cs` compiles
- [ ] Validator is in scene with layers configured
- [ ] Ghost turns green on valid positions
- [ ] Ghost turns red on invalid positions (slopes, obstacles)
- [ ] Debug box visualization appears in Scene view
- [ ] `IsValidPlacement` property reflects current state

### What "Done" Looks Like

Ghost visually indicates whether the current position is valid. Red = can't build here, green = can build here.

---

## Phase 7 — BuildPlacementCamera

### Goal
Implement a top-down camera view for easier building placement.

### What is Implemented
- `BuildPlacementCamera.cs` — Handles camera transitions
- Top-down view during placement
- Pan and zoom controls

### What is Intentionally Deferred
- Smooth boundaries/limits
- Edge panning

### Steps

#### Step 7.1 — Create BuildPlacementCamera.cs

Create `Assets/Scripts/Buildings/Placement/BuildPlacementCamera.cs`:

```csharp
using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages camera transitions between FPS view and top-down building placement view.
/// </summary>
public class BuildPlacementCamera : MonoBehaviour
{
    [BoxGroup("Settings")]
    [SerializeField] private float placementHeight = 20f;

    [BoxGroup("Settings")]
    [SerializeField] private float placementAngle = 60f; // Degrees from horizontal (90 = straight down)

    [BoxGroup("Settings")]
    [SerializeField] private float transitionDuration = 0.3f;

    [BoxGroup("Controls")]
    [SerializeField] private float panSpeed = 20f;

    [BoxGroup("Controls")]
    [SerializeField] private float zoomSpeed = 10f;

    [BoxGroup("Controls")]
    [SerializeField] private Vector2 heightRange = new Vector2(10f, 50f);

    [BoxGroup("References")]
    [SerializeField] private Camera targetCamera;

    [BoxGroup("References")]
    [SerializeField] private PlayerCameraController playerCameraController;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isInPlacementMode = false;

    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float currentHeight;
    private Coroutine transitionCoroutine;

    public bool IsInPlacementMode => isInPlacementMode;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (!isInPlacementMode) return;

        HandlePanning();
        HandleZoom();
    }

    /// <summary>
    /// Transition to placement view centered on a position.
    /// </summary>
    public void EnterPlacementMode(Vector3 centerPosition)
    {
        if (isInPlacementMode) return;

        // Save current camera state
        savedPosition = targetCamera.transform.position;
        savedRotation = targetCamera.transform.rotation;

        // Disable player camera control
        if (playerCameraController != null)
        {
            playerCameraController.enabled = false;
        }

        // Calculate target position and rotation
        currentHeight = placementHeight;
        Vector3 targetPosition = centerPosition + Vector3.up * currentHeight;
        Quaternion targetRotation = Quaternion.Euler(placementAngle, 0f, 0f);

        // Transition
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        transitionCoroutine = StartCoroutine(TransitionCamera(targetPosition, targetRotation));

        isInPlacementMode = true;
        Debug.Log("[BuildPlacementCamera] Entered placement mode");
    }

    /// <summary>
    /// Return to normal FPS camera.
    /// </summary>
    public void ExitPlacementMode()
    {
        if (!isInPlacementMode) return;

        // Transition back
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        transitionCoroutine = StartCoroutine(TransitionCamera(savedPosition, savedRotation, () =>
        {
            // Re-enable player camera
            if (playerCameraController != null)
            {
                playerCameraController.enabled = true;
            }
        }));

        isInPlacementMode = false;
        Debug.Log("[BuildPlacementCamera] Exited placement mode");
    }

    /// <summary>
    /// Get world position from screen point (for ghost positioning).
    /// </summary>
    public bool TryGetGroundPosition(Vector2 screenPosition, LayerMask groundLayer, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (targetCamera == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            worldPosition = hit.point;
            return true;
        }

        return false;
    }

    private void HandlePanning()
    {
        // WASD or arrow key panning
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            Vector3 movement = new Vector3(horizontal, 0f, vertical) * panSpeed * Time.deltaTime;
            targetCamera.transform.position += movement;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentHeight -= scroll * zoomSpeed;
            currentHeight = Mathf.Clamp(currentHeight, heightRange.x, heightRange.y);

            Vector3 pos = targetCamera.transform.position;
            pos.y = currentHeight;
            targetCamera.transform.position = pos;
        }
    }

    private IEnumerator TransitionCamera(Vector3 targetPos, Quaternion targetRot, Action onComplete = null)
    {
        Vector3 startPos = targetCamera.transform.position;
        Quaternion startRot = targetCamera.transform.rotation;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            // Smoothstep interpolation
            t = t * t * (3f - 2f * t);

            targetCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            targetCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        targetCamera.transform.position = targetPos;
        targetCamera.transform.rotation = targetRot;

        onComplete?.Invoke();
    }

#if UNITY_EDITOR
    [Button("Toggle Placement Mode"), BoxGroup("Debug")]
    private void DebugToggle()
    {
        if (!Application.isPlaying) return;

        if (isInPlacementMode)
        {
            ExitPlacementMode();
        }
        else
        {
            EnterPlacementMode(Vector3.zero);
        }
    }
#endif
}
```

#### Step 7.2 — Integrate Camera with BuildModeController

Update `BuildModeController.cs`:

```csharp
// Add field:
[BoxGroup("References")]
[SerializeField] private BuildPlacementCamera placementCamera;

// Modify ChangeState() or add state enter/exit methods:
private void ChangeState(BuildModeState newState)
{
    if (currentState == newState) return;

    BuildModeState previousState = currentState;
    currentState = newState;

    // Handle camera transitions
    if (placementCamera != null)
    {
        if (newState == BuildModeState.Placing && previousState != BuildModeState.Placing)
        {
            // Enter top-down camera
            Vector3 playerPos = playerManager != null ? playerManager.transform.position : Vector3.zero;
            placementCamera.EnterPlacementMode(playerPos);
        }
        else if (newState != BuildModeState.Placing && previousState == BuildModeState.Placing)
        {
            // Exit top-down camera
            placementCamera.ExitPlacementMode();
        }
    }

    OnStateChanged?.Invoke(newState);
    Debug.Log($"[BuildModeController] State: {previousState} -> {newState}");
}
```

#### Step 7.3 — Add Camera Controller to Scene

1. Create empty GameObject `BuildPlacementCamera`
2. Add `BuildPlacementCamera` component
3. Assign `Camera.main` as target camera
4. Assign `PlayerCameraController` reference
5. Configure height, angle, and speed settings
6. In `BuildModeController`, assign the camera reference

#### Step 7.4 — Test Camera Transitions

1. Enter Play Mode
2. Press `B`, select a building
3. Camera should transition to top-down view
4. Use WASD to pan the camera
5. Use scroll wheel to zoom in/out
6. Press `Escape` — camera should return to FPS view

### ✅ Validation Checklist

- [ ] `BuildPlacementCamera.cs` compiles
- [ ] Camera transitions smoothly to top-down on Placing state
- [ ] Camera transitions back on exit/cancel
- [ ] WASD pans the camera
- [ ] Scroll wheel zooms in/out
- [ ] Player camera is disabled during placement mode
- [ ] Player camera re-enables on exit

### What "Done" Looks Like

Selecting a building triggers a smooth camera transition to a top-down view. You can pan/zoom to find a placement spot. Exiting returns to normal FPS camera.

---

## Phase 8 — Resource Cost Checking & Consumption

### Goal
Add resource cost validation before placement and consumption on successful placement.

### What is Implemented
- Affordability check in UI (grayed out slots)
- Resource deduction on successful placement
- Integration with `PlayerResourceInventory`

### What is Intentionally Deferred
- Partial placement (if some resources available)
- Resource refund on cancelled construction

### Steps

#### Step 8.1 — Add Affordability Helper

Add this method to `BuildModeController.cs`:

```csharp
/// <summary>
/// Check if the player can afford a building.
/// </summary>
public bool CanAffordBuilding(BuildingData building)
{
    if (building == null) return false;

    PlayerResourceInventory playerResources = GetPlayerResources();
    if (playerResources == null) return true; // No resources to check

    foreach (var cost in building.ConstructionCosts)
    {
        if (!playerResources.HasResources(cost.resourceType, cost.amount))
        {
            return false;
        }
    }

    return true;
}

/// <summary>
/// Attempt to deduct building costs from player resources.
/// </summary>
private bool TryDeductBuildingCosts(BuildingData building)
{
    if (building == null) return false;

    PlayerResourceInventory playerResources = GetPlayerResources();
    if (playerResources == null) return true; // No resources = free building (debug mode)

    // First verify we can afford everything
    if (!CanAffordBuilding(building))
    {
        Debug.LogWarning($"[BuildModeController] Cannot afford {building.BuildingName}");
        return false;
    }

    // Deduct all costs
    foreach (var cost in building.ConstructionCosts)
    {
        playerResources.RemoveResources(cost.resourceType, cost.amount);
        Debug.Log($"[BuildModeController] Deducted {cost.amount}x {cost.resourceType}");
    }

    return true;
}

private PlayerResourceInventory GetPlayerResources()
{
    if (playerManager != null)
    {
        return playerManager.ResourceInventory;
    }

    return PlayerManager.Instance?.ResourceInventory;
}
```

#### Step 8.2 — Update BuildMenuUI for Affordability

Modify `BuildMenuUI.cs` `RefreshSlots()`:

```csharp
private void RefreshSlots()
{
    // Clear existing slots
    foreach (var slot in activeSlots)
    {
        if (slot != null)
        {
            Destroy(slot.gameObject);
        }
    }
    activeSlots.Clear();

    if (buildModeController == null || buildModeController.Database == null)
    {
        Debug.LogWarning("[BuildMenuUI] No building database available");
        return;
    }

    // Create slot for each building
    foreach (var building in buildModeController.Database.AllBuildings)
    {
        if (building == null) continue;

        BuildingSlotUI slot = Instantiate(slotPrefab, slotContainer);

        // Check affordability using controller's method
        bool canAfford = buildModeController.CanAffordBuilding(building);

        slot.Setup(building, HandleSlotClicked);
        slot.SetAffordable(canAfford);

        activeSlots.Add(slot);
    }

    Debug.Log($"[BuildMenuUI] Populated {activeSlots.Count} building slots");
}
```

#### Step 8.3 — Update Slot Click to Check Affordability

Modify `BuildMenuUI.cs`:

```csharp
private void HandleSlotClicked(BuildingData building)
{
    // Double-check affordability
    if (!buildModeController.CanAffordBuilding(building))
    {
        Debug.LogWarning($"[BuildMenuUI] Cannot afford: {building.BuildingName}");
        // TODO: Play error sound
        return;
    }

    Debug.Log($"[BuildMenuUI] Slot clicked: {building.BuildingName}");
    OnBuildingSelected?.Invoke(building);

    if (buildModeController != null)
    {
        buildModeController.SelectBuilding(building);
    }
}
```

#### Step 8.4 — Test Affordability

1. Use the debug button in `PlayerResourceInventory` to add/clear resources
2. Enter Play Mode
3. Press `B` — some buildings should be grayed out if you can't afford them
4. Add resources, re-open menu — previously grayed buildings should be clickable
5. Clear resources, try clicking — should be prevented

### ✅ Validation Checklist

- [ ] Buildings with unmet costs are grayed out
- [ ] Cannot select buildings you can't afford
- [ ] `CanAffordBuilding()` correctly checks all costs
- [ ] Resource amounts in Inspector match expected behavior

### What "Done" Looks Like

The build menu shows affordability. Gray = can't afford. Clicking gray slots does nothing.

---

## Phase 9 — Final Placement & Registration

### Goal
Complete the placement flow: confirm placement, instantiate building, deduct resources.

### What is Implemented
- Left-click to confirm placement (when valid)
- Building instantiation at ghost position
- Resource deduction
- Ghost cleanup
- Return to gameplay

### What is Intentionally Deferred
- Placement sound/effects
- Construction animation
- Continuous placement mode

### Steps

#### Step 9.1 — Add Placement Confirmation to BuildModeController

Add to `BuildModeController.cs`:

```csharp
private void Update()
{
    HandleInput();

    // Handle placement confirmation when in Placing state
    if (currentState == BuildModeState.Placing)
    {
        HandlePlacementInput();
    }
}

private void HandlePlacementInput()
{
    // Left-click to confirm
    if (Input.GetMouseButtonDown(0))
    {
        TryConfirmPlacement();
    }

    // R to rotate (handled by ghost controller, but we can also handle here)
    // Already handled in BuildingGhostController
}

/// <summary>
/// Attempt to place the building at the current ghost position.
/// </summary>
public void TryConfirmPlacement()
{
    if (currentState != BuildModeState.Placing)
    {
        Debug.LogWarning("[BuildModeController] Not in placing state");
        return;
    }

    if (ghostController == null || !ghostController.HasGhost)
    {
        Debug.LogWarning("[BuildModeController] No ghost to place");
        return;
    }

    // Check validity
    if (!ghostController.IsValidPlacement)
    {
        Debug.Log("[BuildModeController] Invalid placement position");
        // TODO: Play error sound
        return;
    }

    // Check and deduct resources
    if (!TryDeductBuildingCosts(selectedBuilding))
    {
        Debug.Log("[BuildModeController] Cannot afford building");
        // TODO: Play error sound
        return;
    }

    // Instantiate the building
    Building newBuilding = InstantiateBuilding(
        selectedBuilding,
        ghostController.GhostPosition,
        ghostController.GhostRotation
    );

    if (newBuilding != null)
    {
        OnBuildingPlaced?.Invoke(newBuilding);
        Debug.Log($"[BuildModeController] Placed: {selectedBuilding.BuildingName} at {ghostController.GhostPosition}");
    }

    // Clean up and exit
    ExitBuildMode();
}

private Building InstantiateBuilding(BuildingData buildingData, Vector3 position, Quaternion rotation)
{
    if (buildingData == null || buildingData.BuildingPrefab == null)
    {
        Debug.LogError("[BuildModeController] Cannot instantiate: null building data or prefab");
        return null;
    }

    GameObject instance = Instantiate(buildingData.BuildingPrefab, position, rotation);
    instance.name = buildingData.BuildingName;

    // Ensure correct layer
    SetLayerRecursive(instance, LayerMask.NameToLayer("Building"));

    // Get and return the Building component
    Building building = instance.GetComponent<Building>();

    if (building == null)
    {
        Debug.LogWarning($"[BuildModeController] Placed object has no Building component: {buildingData.BuildingName}");
    }

    return building;
}

private void SetLayerRecursive(GameObject obj, int layer)
{
    obj.layer = layer;
    foreach (Transform child in obj.transform)
    {
        SetLayerRecursive(child.gameObject, layer);
    }
}
```

#### Step 9.2 — Test Full Placement Flow

1. Give yourself resources via debug button
2. Enter Play Mode
3. Press `B`, select an affordable building
4. Move ghost to valid position (green)
5. Left-click — building should be placed
6. Check console: resources deducted, building registered
7. Try clicking on invalid position — should do nothing
8. Verify building exists in scene and in `BuildingRegistry`

### ✅ Validation Checklist

- [ ] Left-click on valid position places building
- [ ] Left-click on invalid position does nothing
- [ ] Resources are deducted after placement
- [ ] Building appears at correct position and rotation
- [ ] Building is registered in `BuildingRegistry`
- [ ] Ghost is cleared after placement
- [ ] Camera returns to FPS view
- [ ] Player controls are re-enabled

### What "Done" Looks Like

Complete end-to-end placement: open menu, select building, position ghost, click to place, building appears, resources gone, back to gameplay.

---

## Phase 10 — Integration Hooks (Companion & Resources)

### Goal
Define and document integration points for other systems without deep implementation.

### What is Implemented
- Event subscriptions for external systems
- Clear interface contracts
- Example integration code (commented)

### What is Intentionally Deferred
- Full companion integration (separate system)
- UI resource display during placement

### Steps

#### Step 10.1 — Document Integration Events

The `BuildModeController` exposes these events:

```csharp
// Already defined:
public event Action<BuildModeState> OnStateChanged;
public event Action<BuildingData> OnBuildingSelected;
public event Action<Building> OnBuildingPlaced;
```

#### Step 10.2 — Example: Companion Deposit Target

When a `ResourceDepot` is placed, it becomes a valid deposit target. No code changes needed if:

1. `ResourceDepot` implements `IResourceStorage`
2. `ResourceDepot` inherits from `Building` (auto-registers)
3. Companion queries `BuildingRegistry.FindNearest<IResourceStorage>()`

This integration is **automatic** due to the registry pattern.

#### Step 10.3 — Example: Resource UI During Placement

To show resource costs during placement (future enhancement):

```csharp
// In a hypothetical BuildPlacementUI.cs:

private void OnEnable()
{
    buildModeController.OnBuildingSelected += HandleBuildingSelected;
}

private void HandleBuildingSelected(BuildingData building)
{
    // Display costs
    foreach (var cost in building.ConstructionCosts)
    {
        // Show: cost.resourceType, cost.amount, player's current amount
    }
}
```

#### Step 10.4 — Interface Contracts

For future systems integrating with buildings:

| Interface | Purpose | Query Method |
|-----------|---------|--------------|
| `IResourceStorage` | Buildings that store resources | `BuildingRegistry.FindNearest<IResourceStorage>()` |
| `IBuilding` | Any building | `BuildingRegistry.GetAllBuildings()` |
| `IInteractable` | Player-interactable objects | (Not registry-based, uses colliders/triggers) |

### ✅ Validation Checklist

- [ ] Events are documented and exposed
- [ ] Registry queries work for new building types
- [ ] Integration patterns are understood

### What "Done" Looks Like

You understand how to hook into the building system from other systems. The contracts are clear.

---

## Phase 11 — Refactoring, Cleanup, and Hardening

### Goal
Polish the implementation, add safety checks, and improve code quality.

### What is Implemented
- Null checks throughout
- Input System integration (replacing direct Input calls)
- Component caching
- Code documentation

### Steps

#### Step 11.1 — Replace Legacy Input with Input System

Update `BuildModeController.cs` to use Unity Input System:

```csharp
// In PlayerInputHandler.cs, add:
public bool ToggleBuildModeInput { get; private set; }
public bool ConfirmPlacementInput { get; private set; }
public bool CancelInput { get; private set; }
public bool RotateBuildingInput { get; private set; }

// Wire these to the input actions in OnEnable/OnDisable
```

Then update `BuildModeController` to read from `PlayerInputHandler` instead of `Input.GetKeyDown()`.

#### Step 11.2 — Add Comprehensive Null Checks

Review all scripts for potential null references:

- `BuildModeController`: Check `playerManager`, `ghostController`, `placementCamera`
- `BuildingGhostController`: Check `mainCamera`, `currentGhost`, `validator`
- `BuildMenuUI`: Check `buildModeController`, `slotPrefab`

#### Step 11.3 — Add XML Documentation

Ensure all public methods have summary comments:

```csharp
/// <summary>
/// Attempt to place the building at the current ghost position.
/// </summary>
/// <returns>True if placement succeeded, false otherwise.</returns>
public bool TryConfirmPlacement() { ... }
```

#### Step 11.4 — Performance Considerations

- Cache `Camera.main` (already done in ghost controller)
- Use squared distance comparisons where possible
- Consider object pooling for frequent ghost instantiation (future)

#### Step 11.5 — Final Validation

Run through the complete flow multiple times:

1. Open menu with various resource amounts
2. Place buildings in valid positions
3. Attempt invalid placements
4. Cancel mid-placement
5. Exit and re-enter build mode
6. Verify no console errors

### ✅ Validation Checklist

- [ ] No console errors during normal use
- [ ] No null reference exceptions
- [ ] Input System fully integrated
- [ ] All public methods documented
- [ ] Code follows project conventions

### What "Done" Looks Like

Clean, documented, robust code. No warnings, no errors. Ready for production use.

---

## Common Mistakes & Anti-Patterns

### 1. Placement Logic Inside Building Prefabs

**Wrong:**
```csharp
// In Building.cs
void Update() {
    if (isBeingPlaced) {
        FollowMouse();
        CheckValidity();
    }
}
```

**Why it's bad:** Building prefabs should be pure runtime entities. Placement is a separate concern.

**Correct:** Keep placement logic in `BuildingGhostController` and `BuildModeController`.

---

### 2. Buildings Managing Global Resources

**Wrong:**
```csharp
// In ResourceDepot.cs
void OnBuilt() {
    GameManager.Instance.GlobalResources -= constructionCost;
}
```

**Why it's bad:** Buildings shouldn't know about or modify global state. This creates tight coupling and makes testing difficult.

**Correct:** `BuildModeController` handles resource deduction before instantiation.

---

### 3. UI Driving Game Logic

**Wrong:**
```csharp
// In BuildMenuUI.cs
void OnSlotClicked(BuildingData building) {
    Instantiate(building.BuildingPrefab, somePosition);
    playerResources.Deduct(building.Costs);
}
```

**Why it's bad:** UI should only translate user intent to commands. Actual logic belongs in controllers.

**Correct:** UI calls `buildModeController.SelectBuilding(building)`. Controller handles the rest.

---

### 4. Overusing Singletons

**Wrong:**
```csharp
public class BuildingGhostController : MonoBehaviour {
    public static BuildingGhostController Instance;
    void Awake() { Instance = this; }
}
```

**Why it's bad:** Creates hidden dependencies, makes testing difficult, and can cause order-of-initialization bugs.

**Correct:** Use explicit references (serialized fields) or service locator patterns for truly global services.

---

### 5. Hardcoding Building References

**Wrong:**
```csharp
if (building.name == "Resource Depot") {
    // Special logic
}
```

**Why it's bad:** Brittle, breaks when renamed, doesn't scale.

**Correct:** Use interfaces (`IResourceStorage`), building IDs, or component presence checks.

---

### 6. Skipping Validation

**Wrong:**
```csharp
void PlaceBuilding() {
    Instantiate(prefab, ghostPosition); // Hope it's valid!
}
```

**Why it's bad:** Players can place buildings in invalid locations, causing gameplay issues.

**Correct:** Always validate via `BuildPlacementValidator` before instantiation.

---

### 7. Not Using the Registry

**Wrong:**
```csharp
// Finding all depots
ResourceDepot[] depots = FindObjectsOfType<ResourceDepot>();
```

**Why it's bad:** Slow (`FindObjectsOfType` is O(n) scene objects), doesn't use interface abstraction.

**Correct:** Use `BuildingRegistry.GetAll<IResourceStorage>()`.

---

## Quick Reference

### File Locations

```
Assets/Scripts/Buildings/
├── Core/
│   ├── Building.cs           ✅ Exists
│   ├── BuildingData.cs       ✅ Exists
│   └── BuildingRegistry.cs   ✅ Exists
├── Interfaces/
│   ├── IBuilding.cs          ✅ Exists
│   └── IResourceStorage.cs   ✅ Exists
└── Placement/
    ├── BuildModeController.cs      🆕 Created in Phase 3
    ├── BuildingGhostController.cs  🆕 Created in Phase 5
    ├── BuildPlacementValidator.cs  🆕 Created in Phase 6
    ├── BuildPlacementCamera.cs     🆕 Created in Phase 7
    └── BuildingDatabase.cs         🆕 Created in Phase 2

Assets/Scripts/UI/Building/
├── BuildMenuUI.cs            🆕 Created in Phase 4
└── BuildingSlotUI.cs         🆕 Created in Phase 4
```

### Key Input Bindings

| Action | Key | State |
|--------|-----|-------|
| Toggle Build Mode | B | Any → MenuOpen/Inactive |
| Select Building | LMB on slot | MenuOpen → Placing |
| Rotate Building | R | Placing |
| Confirm Placement | LMB | Placing (valid) |
| Cancel | Escape | Any → Previous/Inactive |

### State Transitions

```
Inactive ──[B]──► MenuOpen ──[Select]──► Placing ──[LMB Valid]──► Inactive
    ▲                │                       │
    │                │                       │
    └──[B/Esc]───────┴───────────[Esc]───────┘
```

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| Initial | 1.0 | Complete implementation guide |

---

*This document is authoritative for building system implementation. Refer to `CompanionAndBuildingSystemsGuide.md` for architectural decisions.*
