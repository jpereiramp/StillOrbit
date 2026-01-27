# StillOrbit: Inventory & Weapon System Implementation Guide

**Version:** 1.0
**Target:** Unity 6 / New Input System / Single-player
**Approach:** Iterative, additive implementation preserving existing systems

---

## Table of Contents

1. [Phase 0 — Codebase Review & Assumptions](#phase-0--codebase-review--assumptions)
2. [Phase 1 — Inventory UI Foundations](#phase-1--inventory-ui-foundations)
3. [Phase 2 — Inventory Panel & Item Grid](#phase-2--inventory-panel--item-grid)
4. [Phase 3 — Quick Slot Data Model](#phase-3--quick-slot-data-model)
5. [Phase 4 — Quick Slot HUD](#phase-4--quick-slot-hud)
6. [Phase 5 — Input & Item Swapping](#phase-5--input--item-swapping)
7. [Phase 6 — Weapon System Review & Gaps](#phase-6--weapon-system-review--gaps)
8. [Phase 7 — Unified Weapon Lifecycle](#phase-7--unified-weapon-lifecycle)
9. [Phase 8 — Melee Weapon Polish](#phase-8--melee-weapon-polish)
10. [Phase 9 — Ranged Weapon (Raycast) Implementation](#phase-9--ranged-weapon-raycast-implementation)
11. [Phase 10 — Ammo, Reloading, Fire Rate](#phase-10--ammo-reloading-fire-rate)
12. [Phase 11 — VFX & SFX Integration](#phase-11--vfx--sfx-integration)
13. [Phase 12 — Integration Testing Scenarios](#phase-12--integration-testing-scenarios)
14. [Phase 13 — Common Pitfalls & Debugging Tips](#phase-13--common-pitfalls--debugging-tips)
15. [Phase 14 — Extension Hooks](#phase-14--extension-hooks)

---

## Phase 0 — Codebase Review & Assumptions

### Goal

Document what exists, confirm assumptions, and establish the contract for all subsequent phases.

### What Already Exists

#### Inventory Systems

| System | Location | Purpose |
|--------|----------|---------|
| `PlayerInventory` | `Assets/Scripts/Inventory/PlayerInventory.cs` | 20-slot item storage with stacking |
| `InventorySlot` | `Assets/Scripts/Inventory/InventorySlot.cs` | Single slot: `ItemData` + `Quantity` |
| `ResourceInventory` | `Assets/Scripts/Resources/ResourceInventory.cs` | Bulk resource storage (wood, ore, etc.) |
| `PlayerResourceInventory` | `Assets/Scripts/Resources/PlayerResourceInventory.cs` | MonoBehaviour wrapper for player |

**Key Events:**
- `PlayerInventory.OnInventoryChanged(int slotIndex)` — Fires when any slot changes
- `PlayerResourceInventory.OnResourcesChanged(ResourceType, int)` — Fires when resources change

#### Item Data Hierarchy

```
ItemData (ScriptableObject)
├── WeaponData (attackRate, range, fleshDamage, woodDamage, rockDamage)
├── ToolData (rate, range, woodDamage, rockDamage, fleshDamage)
└── ConsumableData (healthRestore, hungerRestore, thirstRestore)
```

**Base `ItemData` Properties:**
- `ItemName`, `ItemId`, `Description`, `Icon`
- `CanPickup`, `CanEquip`, `MaxStackSize`
- `WorldPrefab`, `HeldPrefab`

#### Weapon & Combat Systems

| Component | Location | Purpose |
|-----------|----------|---------|
| `WeaponData` | `Assets/Scripts/Items/Data/WeaponData.cs` | Weapon stats as ScriptableObject |
| `MeleeWeapon` | `Assets/Scripts/Items/MeleeWeapon.cs` | IUsable attack behavior |
| `WeaponHitbox` | `Assets/Scripts/Combat/WeaponHitbox.cs` | Trigger-based hit detection |
| `PlayerCombatManager` | `Assets/Scripts/Player/PlayerCombatManager.cs` | Animation triggers, hit registration |
| `DamageType` | `Assets/Scripts/Combat/DamageType.cs` | Generic, Wood, Rock, Flesh |

**Melee Attack Flow:**
```
PrimaryActionInput → MeleeWeapon.Use() → PlayerCombatManager.PerformMeleeAttack()
    → Animator trigger "MeleeAttack"
    → Animation event enables WeaponHitbox
    → OnTriggerEnter → RegisterHit → IDamageable.TakeDamage()
    → Animation event disables WeaponHitbox
```

#### Equipment System

| Component | Location | Purpose |
|-----------|----------|---------|
| `PlayerEquipmentController` | `Assets/Scripts/Player/PlayerEquipmentController.cs` | Equip/unequip, single-slot hands |
| `IUsable` | `Assets/Scripts/Items/IUsable.cs` | Interface: `CanUse`, `Use()` → `UseResult` |
| `HeldItemBehaviour` | `Assets/Scripts/Items/HeldItemBehaviour.cs` | Per-item hold offset/rotation |

#### Input System

| Asset/Component | Location | Purpose |
|-----------------|----------|---------|
| `PlayerControls.inputactions` | `Assets/Settings/PlayerControls.inputactions` | Input action definitions |
| `PlayerInputHandler` | `Assets/Scripts/Player/PlayerInputHandler.cs` | Input state tracking |

**Existing Actions:** Move, Look, PrimaryAction, SecondaryAction, Interact, Drop, Jump, Sprint, Crouch, ToggleBuildMode, RotateBuilding, CallCompanion

#### UI System

| Component | Location | Purpose |
|-----------|----------|---------|
| `UIManager` | `Assets/Scripts/UI/Core/UIManager.cs` | Singleton, panel discovery, Show/Hide/Toggle |
| `UIPanel` | `Assets/Scripts/UI/Core/UIPanel.cs` | Abstract base with CanvasGroup visibility |
| `HealthPanel` | `Assets/Scripts/UI/Panels/HealthPanel.cs` | Health bar display |
| `InteractionPromptPanel` | `Assets/Scripts/UI/Panels/InteractionPromptPanel.cs` | "Press E to..." prompts |

### Confirmed Assumptions

1. **Items ≠ Resources** — `PlayerInventory` holds `ItemData` references; `ResourceInventory` holds `ResourceType` counts. These are separate systems and will remain so.

2. **Single equipped item** — Only one item can be held in hands at a time via `PlayerEquipmentController`.

3. **ScriptableObject-based items** — All item definitions are assets, runtime only stores references.

4. **Animation-driven melee** — Hitbox activation is tied to animation events, not code timers.

5. **No inventory UI exists** — The `OnInventoryChanged` event exists but has no subscribers.

6. **No quick slots exist** — There is no concept of hotbar or quick access slots.

7. **No ranged weapons exist** — Only melee weapons (`MeleeWeapon` + `WeaponHitbox`) are implemented.

### Architecture Principles

These principles guide all implementation decisions:

1. **Additive, not replacement** — New systems extend existing ones; no rewrites.
2. **Event-driven UI** — UI subscribes to data events; never duplicates data.
3. **Interface-based extension** — New behaviors implement existing interfaces (e.g., `IUsable`).
4. **Component composition** — Small, focused components over monolithic scripts.
5. **Data-driven configuration** — ScriptableObjects for balancing, prefabs for visuals.

### What Will NOT Change

- `PlayerInventory` internal implementation
- `ResourceInventory` separation from items
- `WeaponData` existing fields
- `MeleeWeapon` / `WeaponHitbox` melee attack flow
- `PlayerEquipmentController` core equip/unequip logic
- `UIManager` / `UIPanel` base architecture

---

## Phase 1 — Inventory UI Foundations

### Goal

Create the input action for toggling inventory and establish the basic panel infrastructure.

### What Already Exists

- `UIManager` with `ShowPanel<T>()`, `HidePanel<T>()`, `TogglePanel<T>()`
- `UIPanel` base class with CanvasGroup-based visibility
- `PlayerInputHandler` pattern for input state
- `PlayerControls.inputactions` with existing actions

### What Will Be Added

1. New input action: `ToggleInventory` (key: **I**)
2. Input handler extension for the new action
3. Controller to bridge input → UI toggle

### What Is NOT Being Changed

- `UIManager` internals
- `UIPanel` base class
- Existing input actions
- `PlayerInputHandler` structure (only adding new field)

### Implementation Steps

#### Step 1.1: Add ToggleInventory Input Action

Open `Assets/Settings/PlayerControls.inputactions` in the Input Actions editor.

1. In the **Player** action map, click **+** to add a new action
2. Name: `ToggleInventory`
3. Action Type: `Button`
4. Add binding: **Keyboard/I**
5. (Optional) Add gamepad binding: **Select** or **Back** button
6. Save the asset

#### Step 1.2: Extend PlayerInputHandler

Add the following to `PlayerInputHandler.cs`:

```csharp
// Add field with other input state fields
public bool ToggleInventoryPressed { get; private set; }

// Add in OnEnable, alongside other subscriptions
playerControls.Player.ToggleInventory.performed += OnToggleInventory;

// Add in OnDisable, alongside other unsubscriptions
playerControls.Player.ToggleInventory.performed -= OnToggleInventory;

// Add callback method
private void OnToggleInventory(InputAction.CallbackContext context)
{
    ToggleInventoryPressed = true;
}

// Add in LateUpdate or create one if it doesn't exist
private void LateUpdate()
{
    // Reset one-shot inputs at end of frame
    ToggleInventoryPressed = false;
}
```

> **Note:** If `LateUpdate` already exists for resetting other one-shot inputs, add `ToggleInventoryPressed = false;` there.

#### Step 1.3: Create InventoryUIController

Create new file: `Assets/Scripts/UI/Inventory/InventoryUIController.cs`

```csharp
using UnityEngine;

namespace StillOrbit.UI.Inventory
{
    /// <summary>
    /// Bridges input to inventory panel visibility.
    /// Attach to a persistent GameObject (e.g., Player or UI root).
    /// </summary>
    public class InventoryUIController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler inputHandler;

        private void Update()
        {
            if (inputHandler.ToggleInventoryPressed)
            {
                HandleToggleInventory();
            }
        }

        private void HandleToggleInventory()
        {
            // Panel doesn't exist yet - we'll add this in Phase 2
            // UIManager.Instance.TogglePanel<InventoryPanel>();
            Debug.Log("[InventoryUIController] Toggle inventory input received");
        }
    }
}
```

### Validation Checklist

- [ ] `PlayerControls.inputactions` contains `ToggleInventory` action in Player map
- [ ] Pressing **I** logs message to console
- [ ] No compile errors
- [ ] Existing input actions still function

### What "Done" Looks Like

Pressing **I** triggers a debug log message. The infrastructure is ready to toggle an inventory panel that doesn't exist yet.

---

## Phase 2 — Inventory Panel & Item Grid

### Goal

Create a toggleable inventory panel that displays items from `PlayerInventory` in a grid layout.

### What Already Exists

- `PlayerInventory` with `OnInventoryChanged(int slotIndex)` event
- `PlayerInventory.GetSlot(int index)` returns `InventorySlot`
- `InventorySlot` has `ItemData` and `Quantity`
- `ItemData.Icon` provides the sprite for display
- `UIPanel` base class for panel visibility

### What Will Be Added

1. `InventoryPanel` — UI panel showing item grid
2. `InventorySlotUI` — Individual slot visual component
3. Grid layout prefab structure
4. Event subscription to `OnInventoryChanged`

### What Is NOT Being Changed

- `PlayerInventory` implementation
- `InventorySlot` structure
- `ItemData` fields

### Implementation Steps

#### Step 2.1: Create InventorySlotUI Component

Create: `Assets/Scripts/UI/Inventory/InventorySlotUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StillOrbit.UI.Inventory
{
    /// <summary>
    /// Visual representation of a single inventory slot.
    /// Does NOT store item data - only displays it.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject filledState;

        [Header("Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.3f);

        private int slotIndex;

        public int SlotIndex => slotIndex;

        public void Initialize(int index)
        {
            slotIndex = index;
            SetEmpty();
        }

        public void UpdateDisplay(ItemData itemData, int quantity)
        {
            if (itemData == null || quantity <= 0)
            {
                SetEmpty();
                return;
            }

            SetFilled(itemData, quantity);
        }

        private void SetEmpty()
        {
            if (emptyState != null) emptyState.SetActive(true);
            if (filledState != null) filledState.SetActive(false);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = emptyColor;
                iconImage.enabled = false;
            }

            if (quantityText != null)
            {
                quantityText.text = "";
                quantityText.enabled = false;
            }
        }

        private void SetFilled(ItemData itemData, int quantity)
        {
            if (emptyState != null) emptyState.SetActive(false);
            if (filledState != null) filledState.SetActive(true);

            if (iconImage != null)
            {
                iconImage.sprite = itemData.Icon;
                iconImage.color = normalColor;
                iconImage.enabled = true;
            }

            if (quantityText != null)
            {
                // Only show quantity if stackable and more than 1
                if (quantity > 1)
                {
                    quantityText.text = quantity.ToString();
                    quantityText.enabled = true;
                }
                else
                {
                    quantityText.text = "";
                    quantityText.enabled = false;
                }
            }
        }
    }
}
```

#### Step 2.2: Create InventoryPanel Component

Create: `Assets/Scripts/UI/Inventory/InventoryPanel.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace StillOrbit.UI.Inventory
{
    /// <summary>
    /// Main inventory panel that displays all items from PlayerInventory.
    /// Subscribes to inventory events and updates display accordingly.
    /// </summary>
    public class InventoryPanel : UIPanel
    {
        [Header("Inventory References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private InventorySlotUI slotPrefab;

        private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
        private bool isInitialized = false;

        private void Start()
        {
            InitializeSlots();
        }

        private void OnEnable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged += OnSlotChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= OnSlotChanged;
            }
        }

        private void InitializeSlots()
        {
            if (isInitialized) return;
            if (playerInventory == null || slotPrefab == null || slotContainer == null)
            {
                Debug.LogError("[InventoryPanel] Missing required references");
                return;
            }

            // Clear any existing slots
            foreach (Transform child in slotContainer)
            {
                Destroy(child.gameObject);
            }
            slotUIs.Clear();

            // Create slot UIs for each inventory slot
            int slotCount = playerInventory.SlotCount;
            for (int i = 0; i < slotCount; i++)
            {
                InventorySlotUI slotUI = Instantiate(slotPrefab, slotContainer);
                slotUI.Initialize(i);
                slotUIs.Add(slotUI);
            }

            // Initial refresh
            RefreshAllSlots();
            isInitialized = true;
        }

        private void OnSlotChanged(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < slotUIs.Count)
            {
                RefreshSlot(slotIndex);
            }
        }

        private void RefreshSlot(int index)
        {
            InventorySlot slot = playerInventory.GetSlot(index);
            slotUIs[index].UpdateDisplay(slot.ItemData, slot.Quantity);
        }

        private void RefreshAllSlots()
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                RefreshSlot(i);
            }
        }

        protected override void OnShow()
        {
            base.OnShow();
            // Refresh when panel opens in case data changed while hidden
            if (isInitialized)
            {
                RefreshAllSlots();
            }
        }
    }
}
```

#### Step 2.3: Add SlotCount Property to PlayerInventory

Add to `PlayerInventory.cs` if not present:

```csharp
public int SlotCount => slots.Count;
```

#### Step 2.4: Create UI Prefab Structure

Create the following hierarchy in a new Canvas or existing UI Canvas:

```
InventoryPanel (GameObject)
├── CanvasGroup (component)
├── InventoryPanel.cs (component)
├── Background (Image - semi-transparent dark)
├── Header
│   └── TitleText (TextMeshProUGUI - "Inventory")
└── SlotContainer (GridLayoutGroup)
    └── [Slots will be instantiated here]
```

**GridLayoutGroup Settings on SlotContainer:**
- Cell Size: 80 x 80 (adjust to taste)
- Spacing: 8 x 8
- Start Corner: Upper Left
- Start Axis: Horizontal
- Child Alignment: Upper Left
- Constraint: Fixed Column Count → 5 (for 5 columns)

**Create InventorySlotUI Prefab:**

```
InventorySlot (GameObject)
├── InventorySlotUI.cs (component)
├── Background (Image - slot background)
├── EmptyState (GameObject)
│   └── EmptyIcon (Image - faded icon placeholder)
├── FilledState (GameObject)
│   ├── IconImage (Image - item icon reference)
│   └── QuantityText (TextMeshProUGUI - bottom-right corner)
```

#### Step 2.5: Wire Up InventoryUIController

Update `InventoryUIController.cs`:

```csharp
private void HandleToggleInventory()
{
    UIManager.Instance.TogglePanel<InventoryPanel>();
}
```

#### Step 2.6: Configure Panel References

1. Assign `PlayerInventory` reference to `InventoryPanel`
2. Assign `SlotContainer` transform
3. Assign `InventorySlotUI` prefab
4. Ensure panel starts hidden (alpha = 0 on CanvasGroup)

### Validation Checklist

- [ ] Pressing **I** opens inventory panel
- [ ] Pressing **I** again closes it
- [ ] Panel shows correct number of slots (20 by default)
- [ ] Empty slots display empty state
- [ ] Picking up an item updates the corresponding slot
- [ ] Stackable items show quantity when > 1
- [ ] Non-stackable items don't show quantity
- [ ] Dropping an item updates the slot to empty

### What "Done" Looks Like

A functional inventory grid that:
- Toggles with **I** key
- Displays current `PlayerInventory` contents
- Updates in real-time when inventory changes
- Shows item icons and stack quantities
- Is read-only (no interaction yet)

---

## Phase 3 — Quick Slot Data Model

### Goal

Create a data layer for 5 quick slots that reference items from `PlayerInventory` without duplicating storage.

### What Already Exists

- `PlayerInventory` stores actual item data
- `PlayerEquipmentController` handles single equipped item
- Items have `ItemData.CanEquip` flag

### What Will Be Added

1. `QuickSlotManager` — Manages 5 quick slot assignments
2. Events for quick slot changes
3. Active slot tracking

### What Is NOT Being Changed

- `PlayerInventory` — Quick slots reference inventory slots, not copy items
- `PlayerEquipmentController` — Still handles actual equip/unequip

### Design Decision: Reference vs. Copy

Quick slots will store **inventory slot indices** (or null), not item copies. This ensures:
- No duplicate item data
- Automatic sync when inventory changes
- Items can only be in quick slots if they exist in inventory

### Implementation Steps

#### Step 3.1: Create QuickSlotManager

Create: `Assets/Scripts/Inventory/QuickSlotManager.cs`

```csharp
using System;
using UnityEngine;

namespace StillOrbit.Inventory
{
    /// <summary>
    /// Manages 5 quick slots that reference inventory slots.
    /// Quick slots are a VIEW into the inventory, not separate storage.
    /// </summary>
    public class QuickSlotManager : MonoBehaviour
    {
        public const int QUICK_SLOT_COUNT = 5;

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipmentController equipmentController;

        // Each element is the inventory slot index, or -1 if empty
        private int[] quickSlotAssignments = new int[QUICK_SLOT_COUNT];

        // Currently selected quick slot (0-4), -1 if none
        private int activeSlotIndex = -1;

        /// <summary>
        /// Fired when a quick slot assignment changes.
        /// Parameters: (slotIndex, itemData or null)
        /// </summary>
        public event Action<int, ItemData> OnQuickSlotChanged;

        /// <summary>
        /// Fired when the active quick slot changes.
        /// Parameters: (previousIndex, newIndex)
        /// </summary>
        public event Action<int, int> OnActiveSlotChanged;

        public int ActiveSlotIndex => activeSlotIndex;

        private void Awake()
        {
            // Initialize all slots to empty
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            {
                quickSlotAssignments[i] = -1;
            }
        }

        private void OnEnable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged += OnInventorySlotChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= OnInventorySlotChanged;
            }
        }

        /// <summary>
        /// Get the item data for a quick slot (null if empty or invalid).
        /// </summary>
        public ItemData GetQuickSlotItem(int quickSlotIndex)
        {
            if (!IsValidQuickSlotIndex(quickSlotIndex)) return null;

            int inventoryIndex = quickSlotAssignments[quickSlotIndex];
            if (inventoryIndex < 0) return null;

            InventorySlot slot = playerInventory.GetSlot(inventoryIndex);
            return slot?.ItemData;
        }

        /// <summary>
        /// Get the inventory slot index assigned to a quick slot (-1 if none).
        /// </summary>
        public int GetInventorySlotIndex(int quickSlotIndex)
        {
            if (!IsValidQuickSlotIndex(quickSlotIndex)) return -1;
            return quickSlotAssignments[quickSlotIndex];
        }

        /// <summary>
        /// Assign an inventory slot to a quick slot.
        /// Only items with CanEquip = true can be assigned.
        /// </summary>
        public bool AssignToQuickSlot(int quickSlotIndex, int inventorySlotIndex)
        {
            if (!IsValidQuickSlotIndex(quickSlotIndex)) return false;

            // Validate inventory slot
            if (inventorySlotIndex >= 0)
            {
                InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);
                if (slot == null || slot.IsEmpty) return false;
                if (!slot.ItemData.CanEquip) return false;

                // Remove from any other quick slot first
                RemoveFromAllQuickSlots(inventorySlotIndex);
            }

            quickSlotAssignments[quickSlotIndex] = inventorySlotIndex;

            ItemData item = inventorySlotIndex >= 0
                ? playerInventory.GetSlot(inventorySlotIndex)?.ItemData
                : null;
            OnQuickSlotChanged?.Invoke(quickSlotIndex, item);

            return true;
        }

        /// <summary>
        /// Clear a quick slot assignment.
        /// </summary>
        public void ClearQuickSlot(int quickSlotIndex)
        {
            AssignToQuickSlot(quickSlotIndex, -1);
        }

        /// <summary>
        /// Select a quick slot as active and equip its item.
        /// </summary>
        public void SelectQuickSlot(int quickSlotIndex)
        {
            if (!IsValidQuickSlotIndex(quickSlotIndex)) return;

            int previousIndex = activeSlotIndex;

            // If selecting the same slot, deselect (unequip)
            if (activeSlotIndex == quickSlotIndex)
            {
                activeSlotIndex = -1;
                equipmentController.UnequipItem(true);
                OnActiveSlotChanged?.Invoke(previousIndex, -1);
                return;
            }

            // Select new slot
            activeSlotIndex = quickSlotIndex;

            // Equip the item in this slot
            ItemData item = GetQuickSlotItem(quickSlotIndex);
            if (item != null && item.CanEquip)
            {
                equipmentController.UnequipItem(true);
                equipmentController.EquipItem(item);
            }
            else
            {
                // Empty slot selected - unequip current
                equipmentController.UnequipItem(true);
            }

            OnActiveSlotChanged?.Invoke(previousIndex, activeSlotIndex);
        }

        /// <summary>
        /// Find which quick slot (if any) contains the given inventory slot.
        /// Returns -1 if not found.
        /// </summary>
        public int FindQuickSlotForInventorySlot(int inventorySlotIndex)
        {
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            {
                if (quickSlotAssignments[i] == inventorySlotIndex)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Automatically assign a newly picked up equippable item to the first empty quick slot.
        /// </summary>
        public bool TryAutoAssign(int inventorySlotIndex)
        {
            InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);
            if (slot == null || slot.IsEmpty || !slot.ItemData.CanEquip)
                return false;

            // Find first empty quick slot
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            {
                if (quickSlotAssignments[i] < 0)
                {
                    return AssignToQuickSlot(i, inventorySlotIndex);
                }
            }

            return false; // No empty slots
        }

        private void RemoveFromAllQuickSlots(int inventorySlotIndex)
        {
            for (int i = 0; i < QUICK_SLOT_COUNT; i++)
            {
                if (quickSlotAssignments[i] == inventorySlotIndex)
                {
                    quickSlotAssignments[i] = -1;
                    OnQuickSlotChanged?.Invoke(i, null);
                }
            }
        }

        private void OnInventorySlotChanged(int inventorySlotIndex)
        {
            // Check if this inventory slot is assigned to any quick slot
            int quickSlot = FindQuickSlotForInventorySlot(inventorySlotIndex);
            if (quickSlot < 0) return;

            InventorySlot slot = playerInventory.GetSlot(inventorySlotIndex);

            // If slot is now empty, clear the quick slot assignment
            if (slot == null || slot.IsEmpty)
            {
                quickSlotAssignments[quickSlot] = -1;
                OnQuickSlotChanged?.Invoke(quickSlot, null);

                // If this was the active slot, deselect
                if (activeSlotIndex == quickSlot)
                {
                    int prev = activeSlotIndex;
                    activeSlotIndex = -1;
                    OnActiveSlotChanged?.Invoke(prev, -1);
                }
            }
            else
            {
                // Item still exists, just notify of potential change
                OnQuickSlotChanged?.Invoke(quickSlot, slot.ItemData);
            }
        }

        private bool IsValidQuickSlotIndex(int index)
        {
            return index >= 0 && index < QUICK_SLOT_COUNT;
        }
    }
}
```

### Validation Checklist

- [ ] `QuickSlotManager` compiles without errors
- [ ] Can assign inventory slots to quick slots via code
- [ ] Quick slots reject items where `CanEquip = false`
- [ ] Removing an item from inventory clears the quick slot
- [ ] `OnQuickSlotChanged` fires when assignments change
- [ ] `OnActiveSlotChanged` fires when selection changes

### What "Done" Looks Like

A working data model where:
- 5 quick slots can reference inventory items
- Only equippable items can be assigned
- Inventory changes propagate to quick slots
- Selecting a quick slot equips/unequips items
- Events enable UI to react

---

## Phase 4 — Quick Slot HUD

### Goal

Create an always-visible HUD showing the 5 quick slots with selection highlight.

### What Already Exists

- `QuickSlotManager` with events
- `UIPanel` pattern (though HUD won't use hide/show)
- `InventorySlotUI` pattern for slot display

### What Will Be Added

1. `QuickSlotHUD` — Always-visible HUD component
2. `QuickSlotUI` — Individual quick slot visual
3. Selection highlight indicator

### Implementation Steps

#### Step 4.1: Create QuickSlotUI Component

Create: `Assets/Scripts/UI/Inventory/QuickSlotUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StillOrbit.UI.Inventory
{
    /// <summary>
    /// Visual representation of a single quick slot in the HUD.
    /// </summary>
    public class QuickSlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image slotBackground;
        [SerializeField] private GameObject selectionHighlight;
        [SerializeField] private TextMeshProUGUI hotkeyText;

        [Header("Colors")]
        [SerializeField] private Color normalBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color selectedBackgroundColor = new Color(0.4f, 0.6f, 0.8f, 0.9f);
        [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color filledIconColor = Color.white;

        private int slotIndex;
        private bool isSelected;

        public int SlotIndex => slotIndex;

        public void Initialize(int index)
        {
            slotIndex = index;

            // Display hotkey number (1-5)
            if (hotkeyText != null)
            {
                hotkeyText.text = (index + 1).ToString();
            }

            SetEmpty();
            SetSelected(false);
        }

        public void UpdateDisplay(ItemData itemData)
        {
            if (itemData == null)
            {
                SetEmpty();
            }
            else
            {
                SetFilled(itemData);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;

            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(selected);
            }

            if (slotBackground != null)
            {
                slotBackground.color = selected ? selectedBackgroundColor : normalBackgroundColor;
            }
        }

        private void SetEmpty()
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = emptyIconColor;
                iconImage.enabled = false;
            }
        }

        private void SetFilled(ItemData itemData)
        {
            if (iconImage != null)
            {
                iconImage.sprite = itemData.Icon;
                iconImage.color = filledIconColor;
                iconImage.enabled = true;
            }
        }
    }
}
```

#### Step 4.2: Create QuickSlotHUD Component

Create: `Assets/Scripts/UI/Inventory/QuickSlotHUD.cs`

```csharp
using UnityEngine;
using StillOrbit.Inventory;

namespace StillOrbit.UI.Inventory
{
    /// <summary>
    /// Always-visible HUD showing quick slots.
    /// Does not extend UIPanel as it should never be hidden.
    /// </summary>
    public class QuickSlotHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private QuickSlotUI slotPrefab;

        private QuickSlotUI[] slotUIs;

        private void Start()
        {
            InitializeSlots();
        }

        private void OnEnable()
        {
            if (quickSlotManager != null)
            {
                quickSlotManager.OnQuickSlotChanged += OnQuickSlotChanged;
                quickSlotManager.OnActiveSlotChanged += OnActiveSlotChanged;
            }
        }

        private void OnDisable()
        {
            if (quickSlotManager != null)
            {
                quickSlotManager.OnQuickSlotChanged -= OnQuickSlotChanged;
                quickSlotManager.OnActiveSlotChanged -= OnActiveSlotChanged;
            }
        }

        private void InitializeSlots()
        {
            if (slotPrefab == null || slotContainer == null || quickSlotManager == null)
            {
                Debug.LogError("[QuickSlotHUD] Missing required references");
                return;
            }

            // Clear existing
            foreach (Transform child in slotContainer)
            {
                Destroy(child.gameObject);
            }

            // Create slots
            slotUIs = new QuickSlotUI[QuickSlotManager.QUICK_SLOT_COUNT];
            for (int i = 0; i < QuickSlotManager.QUICK_SLOT_COUNT; i++)
            {
                QuickSlotUI slotUI = Instantiate(slotPrefab, slotContainer);
                slotUI.Initialize(i);
                slotUIs[i] = slotUI;
            }

            // Initial refresh
            RefreshAllSlots();
            UpdateSelectionHighlight(-1, quickSlotManager.ActiveSlotIndex);
        }

        private void OnQuickSlotChanged(int slotIndex, ItemData itemData)
        {
            if (slotUIs != null && slotIndex >= 0 && slotIndex < slotUIs.Length)
            {
                slotUIs[slotIndex].UpdateDisplay(itemData);
            }
        }

        private void OnActiveSlotChanged(int previousIndex, int newIndex)
        {
            UpdateSelectionHighlight(previousIndex, newIndex);
        }

        private void UpdateSelectionHighlight(int previousIndex, int newIndex)
        {
            if (slotUIs == null) return;

            if (previousIndex >= 0 && previousIndex < slotUIs.Length)
            {
                slotUIs[previousIndex].SetSelected(false);
            }

            if (newIndex >= 0 && newIndex < slotUIs.Length)
            {
                slotUIs[newIndex].SetSelected(true);
            }
        }

        private void RefreshAllSlots()
        {
            for (int i = 0; i < slotUIs.Length; i++)
            {
                ItemData item = quickSlotManager.GetQuickSlotItem(i);
                slotUIs[i].UpdateDisplay(item);
            }
        }
    }
}
```

#### Step 4.3: Create UI Prefab Structure

Create in Canvas (should always be visible, typically at bottom-center):

```
QuickSlotHUD (GameObject)
├── QuickSlotHUD.cs (component)
├── Background (Image - optional subtle background)
└── SlotContainer (HorizontalLayoutGroup)
    └── [Quick slots instantiated here]
```

**HorizontalLayoutGroup Settings on SlotContainer:**
- Spacing: 8
- Child Alignment: Middle Center
- Child Force Expand: Width = false, Height = false
- Control Child Size: Width = false, Height = false

**Create QuickSlotUI Prefab:**

```
QuickSlot (GameObject, 70x70)
├── QuickSlotUI.cs (component)
├── SlotBackground (Image - rounded rectangle)
├── IconImage (Image - item icon, centered)
├── SelectionHighlight (Image - border/glow, starts inactive)
└── HotkeyText (TextMeshProUGUI - corner number "1"-"5")
```

### Validation Checklist

- [ ] HUD displays 5 quick slots at screen bottom
- [ ] Slots show numbers 1-5
- [ ] Empty slots appear dimmed
- [ ] Assigning an item shows its icon
- [ ] Selecting a slot highlights it
- [ ] Deselecting removes highlight

### What "Done" Looks Like

An always-visible quick slot bar at the bottom of the screen showing 5 slots with hotkey numbers, item icons when assigned, and selection highlighting.

---

## Phase 5 — Input & Item Swapping

### Goal

Connect number key inputs (1-5) to quick slot selection, enabling seamless item swapping.

### What Already Exists

- `QuickSlotManager.SelectQuickSlot(int)` handles equip/unequip
- `PlayerInputHandler` pattern for input handling
- `PlayerControls.inputactions` for input definitions

### What Will Be Added

1. Five new input actions: `QuickSlot1` through `QuickSlot5`
2. Input handler extensions
3. Controller to bridge input → `QuickSlotManager`

### Implementation Steps

#### Step 5.1: Add Quick Slot Input Actions

Open `Assets/Settings/PlayerControls.inputactions`:

1. In the **Player** action map, add 5 new actions:

| Action Name | Type | Keyboard Binding |
|-------------|------|------------------|
| `QuickSlot1` | Button | **1** (Keyboard) |
| `QuickSlot2` | Button | **2** (Keyboard) |
| `QuickSlot3` | Button | **3** (Keyboard) |
| `QuickSlot4` | Button | **4** (Keyboard) |
| `QuickSlot5` | Button | **5** (Keyboard) |

2. (Optional) Add gamepad bindings: D-Pad directions or shoulder + face buttons
3. Save the asset

#### Step 5.2: Extend PlayerInputHandler

Add to `PlayerInputHandler.cs`:

```csharp
// Add fields
public int QuickSlotPressed { get; private set; } = -1; // -1 = none, 0-4 = slot index

// In OnEnable, add:
playerControls.Player.QuickSlot1.performed += ctx => OnQuickSlotPressed(0);
playerControls.Player.QuickSlot2.performed += ctx => OnQuickSlotPressed(1);
playerControls.Player.QuickSlot3.performed += ctx => OnQuickSlotPressed(2);
playerControls.Player.QuickSlot4.performed += ctx => OnQuickSlotPressed(3);
playerControls.Player.QuickSlot5.performed += ctx => OnQuickSlotPressed(4);

// Add callback
private void OnQuickSlotPressed(int slotIndex)
{
    QuickSlotPressed = slotIndex;
}

// In LateUpdate, add:
QuickSlotPressed = -1;
```

#### Step 5.3: Create QuickSlotInputController

Create: `Assets/Scripts/Inventory/QuickSlotInputController.cs`

```csharp
using UnityEngine;

namespace StillOrbit.Inventory
{
    /// <summary>
    /// Bridges quick slot input to QuickSlotManager.
    /// </summary>
    public class QuickSlotInputController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private QuickSlotManager quickSlotManager;

        private void Update()
        {
            if (inputHandler.QuickSlotPressed >= 0)
            {
                quickSlotManager.SelectQuickSlot(inputHandler.QuickSlotPressed);
            }
        }
    }
}
```

#### Step 5.4: Handle Equip on Pickup Integration

Modify `PlayerInteractionController` pickup logic to auto-assign to quick slots:

In `PlayerInteractionController.cs`, after successfully adding item to inventory:

```csharp
// After: inventory.TryAddItem(itemData, quantity)
// Add:
int inventoryIndex = inventory.FindItem(itemData);
if (inventoryIndex >= 0)
{
    quickSlotManager.TryAutoAssign(inventoryIndex);
}
```

Add the reference:
```csharp
[SerializeField] private QuickSlotManager quickSlotManager;
```

### Validation Checklist

- [ ] Pressing **1** selects quick slot 1 (if assigned)
- [ ] Pressing **2-5** selects corresponding slots
- [ ] Selection equips the item in that slot
- [ ] Pressing the same number again deselects (unequips)
- [ ] Pressing a different number swaps items smoothly
- [ ] Picking up an equippable item auto-assigns to first empty quick slot
- [ ] Quick slot HUD updates selection highlight

### What "Done" Looks Like

Players can:
1. Pick up weapons/tools that auto-assign to quick slots
2. Press 1-5 to instantly switch between equipped items
3. See visual feedback in the HUD
4. Press the active slot key to put away the item

---

## Phase 6 — Weapon System Review & Gaps

### Goal

Identify what exists in the weapon system and what needs to be added for ranged weapon support.

### What Already Exists (Melee)

#### WeaponData (`Assets/Scripts/Items/Data/WeaponData.cs`)

```csharp
public class WeaponData : ItemData
{
    [SerializeField] private float attackRate;  // Attacks per second
    [SerializeField] private float range;
    [SerializeField] private float fleshDamage;
    [SerializeField] private float woodDamage;
    [SerializeField] private float rockDamage;

    public float GetDamage(DamageType targetType);
}
```

#### MeleeWeapon (`Assets/Scripts/Items/MeleeWeapon.cs`)

- Implements `IUsable`
- Handles attack cooldown via `attackRate`
- Plays swing SFX
- Fires `onAttack` and `onHit` UnityEvents
- Delegates to `PlayerCombatManager.PerformMeleeAttack()`

#### WeaponHitbox (`Assets/Scripts/Combat/WeaponHitbox.cs`)

- Trigger-based collision detection
- Enabled/disabled via animation events
- Reports hits to `PlayerCombatManager.RegisterHit()`
- Layer mask filtering
- Self-collision prevention

#### PlayerCombatManager

- `PerformMeleeAttack(MeleeWeapon)` — Triggers animation
- `RegisterHit()` — Processes damage application
- Animation event callbacks for hitbox control

### Gap Analysis for Ranged Weapons

| Aspect | Melee (Exists) | Ranged (Needed) |
|--------|----------------|-----------------|
| **Damage Source** | Hitbox trigger | Raycast |
| **Timing** | Animation-driven | Input-driven (can fire without animation) |
| **Ammo** | N/A | Clip + reserve |
| **Reload** | N/A | Reload time + animation |
| **Fire Rate** | AttackRate (cooldown) | FireRate (similar concept) |
| **Effects** | Swing SFX, onHit | Muzzle flash, projectile/beam, impact VFX |
| **State** | Simple cooldown | Firing, Reloading, Ready, Empty |

### Architectural Decision: Extend WeaponData or Create RangedWeaponData?

**Option A: Extend WeaponData with optional ranged fields**
- Pro: Single data type, simpler hierarchy
- Con: Melee weapons carry unused ranged fields

**Option B: Create RangedWeaponData extending WeaponData**
- Pro: Clean separation, no unused fields
- Con: Deeper hierarchy

**Decision: Option B (RangedWeaponData)**

Rationale:
- Ranged weapons have significant additional configuration (ammo, reload, fire mode)
- Keeps melee data clean
- Follows existing pattern (`WeaponData` extends `ItemData`)
- `WeaponData` becomes shared base for combat items

### What Will Be Created

1. `RangedWeaponData` — ScriptableObject extending `WeaponData`
2. `RangedWeapon` — MonoBehaviour implementing `IUsable` (parallel to `MeleeWeapon`)
3. `IWeapon` — Interface for shared weapon behaviors (optional, for future polymorphism)

### What Is NOT Being Changed

- `WeaponData` existing fields
- `MeleeWeapon` implementation
- `WeaponHitbox` system
- `PlayerCombatManager` melee flow

---

## Phase 7 — Unified Weapon Lifecycle

### Goal

Ensure melee and ranged weapons share consistent equip/unequip/use lifecycle patterns.

### What Already Exists

- `IUsable` interface: `CanUse`, `Use(GameObject user)` → `UseResult`
- `PlayerEquipmentController.TryUseEquippedItem()` calls `IUsable.Use()`
- `MeleeWeapon` implements `IUsable`

### What Will Be Added

1. `IWeapon` interface for weapon-specific behaviors (optional)
2. Documentation of lifecycle contract

### Design: Weapon Lifecycle Contract

All weapons (melee and ranged) must follow this lifecycle:

```
┌─────────────┐
│   Unequipped │ ← Prefab in inventory, not instantiated
└──────┬───────┘
       │ PlayerEquipmentController.EquipItem()
       ▼
┌─────────────┐
│   Equipped   │ ← HeldPrefab instantiated, IUsable cached
└──────┬───────┘
       │ PrimaryActionInput → TryUseEquippedItem()
       ▼
┌─────────────┐
│    Use()     │ ← IUsable.Use() called
│              │   - Check CanUse (cooldown, ammo, etc.)
│              │   - Perform action (attack/fire)
│              │   - Return UseResult
└──────┬───────┘
       │
       ▼
┌─────────────┐
│  UseResult   │
│  - Success   │ → Remains equipped, ready for next use
│  - Consumed  │ → Auto-unequipped, removed from inventory
│  - Failed    │ → No action taken, remains equipped
└─────────────┘
```

### IWeapon Interface (Optional Enhancement)

Create: `Assets/Scripts/Combat/IWeapon.cs`

```csharp
namespace StillOrbit.Combat
{
    /// <summary>
    /// Optional interface for weapon-specific behaviors beyond IUsable.
    /// Allows combat systems to query weapon capabilities.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>
        /// The weapon's data definition.
        /// </summary>
        WeaponData WeaponData { get; }

        /// <summary>
        /// Get damage for a specific target type.
        /// </summary>
        float GetDamage(DamageType targetType);

        /// <summary>
        /// Get effective range.
        /// </summary>
        float GetRange();

        /// <summary>
        /// Called when the weapon successfully hits a target.
        /// </summary>
        void NotifyHit();
    }
}
```

Update `MeleeWeapon` to implement `IWeapon`:

```csharp
public class MeleeWeapon : MonoBehaviour, IUsable, IWeapon
{
    // ... existing implementation ...

    public WeaponData WeaponData => itemData as WeaponData;
}
```

### Validation Checklist

- [ ] `IWeapon` interface created (if implementing)
- [ ] `MeleeWeapon` implements `IWeapon` (if implementing)
- [ ] Existing melee weapon functionality unchanged
- [ ] Lifecycle documentation clear

### What "Done" Looks Like

Clear contract for how weapons integrate with the equipment system, ready for ranged weapon implementation.

---

## Phase 8 — Melee Weapon Polish

### Goal

Ensure existing melee system is complete before adding ranged. Identify and fix any gaps.

### Current State Review

The melee system is functional with:
- Animation-driven hitbox activation
- Cooldown via `attackRate`
- Damage type system
- UnityEvents for effects

### Potential Improvements (Optional)

These are **optional polish items**, not required for ranged implementation:

#### 8.1: Attack Queuing

Currently, if the player presses attack during cooldown, nothing happens. Consider queuing:

```csharp
// In MeleeWeapon
private bool attackQueued = false;

public void Update()
{
    if (attackQueued && CanUse)
    {
        attackQueued = false;
        // Trigger attack
    }
}

public UseResult Use(GameObject user)
{
    if (!CanUse)
    {
        attackQueued = true;
        return UseResult.Failed;
    }
    // ... rest of attack logic
}
```

**Decision:** Skip for now. Players can spam attack input.

#### 8.2: Attack Combos

Chain attacks with different animations. Would require:
- Combo state tracking
- Multiple animation triggers
- Timing windows

**Decision:** Out of scope. Document as extension hook.

### What Is NOT Being Changed

- Core melee attack flow
- Animation event system
- Hit detection

### Validation Checklist

- [ ] Melee attacks work correctly
- [ ] Cooldown is enforced
- [ ] Damage applies to `IDamageable` targets
- [ ] SFX plays on swing
- [ ] VFX plays on hit (via `HitEffectReceiver`)

### What "Done" Looks Like

Confirmation that melee system is production-ready. No blockers for ranged implementation.

---

## Phase 9 — Ranged Weapon (Raycast) Implementation

### Goal

Create the core ranged weapon component using raycast-based hit detection.

### What Will Be Added

1. `RangedWeaponData` — ScriptableObject for ranged configuration
2. `RangedWeapon` — MonoBehaviour implementing `IUsable`, `IWeapon`
3. Raycast-based hit detection
4. Fire cooldown

### Implementation Steps

#### Step 9.1: Create RangedWeaponData

Create: `Assets/Scripts/Items/Data/RangedWeaponData.cs`

```csharp
using UnityEngine;

namespace StillOrbit.Items.Data
{
    /// <summary>
    /// Data definition for ranged weapons (guns, lasers, etc.).
    /// Extends WeaponData with ranged-specific properties.
    /// </summary>
    [CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "StillOrbit/Items/Ranged Weapon Data")]
    public class RangedWeaponData : WeaponData
    {
        [Header("Ranged Properties")]
        [SerializeField, Min(0.01f)] private float fireRate = 5f;  // Shots per second
        [SerializeField, Min(1)] private int clipSize = 30;
        [SerializeField, Min(0f)] private float reloadTime = 2f;
        [SerializeField, Min(0f)] private float maxRange = 100f;

        [Header("Ammo")]
        [SerializeField] private bool useAmmo = true;
        [SerializeField] private ItemData ammoType;  // Null = infinite ammo

        [Header("Spread")]
        [SerializeField, Range(0f, 45f)] private float spreadAngle = 0f;  // Degrees
        [SerializeField, Min(1)] private int pelletsPerShot = 1;  // For shotguns

        // Public accessors
        public float FireRate => fireRate;
        public int ClipSize => clipSize;
        public float ReloadTime => reloadTime;
        public float MaxRange => maxRange;
        public bool UseAmmo => useAmmo;
        public ItemData AmmoType => ammoType;
        public float SpreadAngle => spreadAngle;
        public int PelletsPerShot => pelletsPerShot;

        /// <summary>
        /// Time between shots in seconds.
        /// </summary>
        public float FireInterval => 1f / fireRate;
    }
}
```

#### Step 9.2: Create RangedWeapon Component

Create: `Assets/Scripts/Items/RangedWeapon.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.Events;
using StillOrbit.Combat;

namespace StillOrbit.Items
{
    /// <summary>
    /// Ranged weapon behavior using raycast hit detection.
    /// Attach to HeldPrefab of ranged weapons.
    /// </summary>
    [RequireComponent(typeof(Item))]
    public class RangedWeapon : MonoBehaviour, IUsable, IWeapon
    {
        [Header("Data Reference")]
        [SerializeField] private RangedWeaponData weaponData;

        [Header("Fire Point")]
        [SerializeField] private Transform firePoint;  // Where raycast originates

        [Header("Audio")]
        [SerializeField] private AudioClip fireSFX;
        [SerializeField] private AudioClip emptySFX;
        [SerializeField] private AudioClip reloadSFX;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onFire;
        [SerializeField] private UnityEvent onHit;
        [SerializeField] private UnityEvent onReloadStart;
        [SerializeField] private UnityEvent onReloadComplete;
        [SerializeField] private UnityEvent onEmpty;

        [Header("Layers")]
        [SerializeField] private LayerMask hitLayers = ~0;

        // Runtime state
        private int currentAmmo;
        private float lastFireTime;
        private bool isReloading;
        private float reloadStartTime;

        private Transform ownerTransform;

        // Events for external systems (UI, etc.)
        public event Action<int, int> OnAmmoChanged;  // (current, max)
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;

        // IUsable implementation
        public bool CanUse => !isReloading && HasAmmo && CooldownElapsed;

        // IWeapon implementation
        public WeaponData WeaponData => weaponData;

        private bool HasAmmo => !weaponData.UseAmmo || currentAmmo > 0;
        private bool CooldownElapsed => Time.time >= lastFireTime + weaponData.FireInterval;

        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => weaponData.ClipSize;
        public bool IsReloading => isReloading;

        private void Awake()
        {
            currentAmmo = weaponData != null ? weaponData.ClipSize : 0;

            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        private void Update()
        {
            // Handle reload completion
            if (isReloading && Time.time >= reloadStartTime + weaponData.ReloadTime)
            {
                CompleteReload();
            }
        }

        public UseResult Use(GameObject user)
        {
            if (weaponData == null)
            {
                Debug.LogError("[RangedWeapon] No weapon data assigned");
                return UseResult.Failed;
            }

            ownerTransform = user.transform;

            // Handle reloading
            if (isReloading)
            {
                return UseResult.Failed;
            }

            // Check ammo
            if (weaponData.UseAmmo && currentAmmo <= 0)
            {
                PlaySound(emptySFX);
                onEmpty?.Invoke();
                return UseResult.Failed;
            }

            // Check cooldown
            if (!CooldownElapsed)
            {
                return UseResult.Failed;
            }

            // Fire!
            Fire();
            return UseResult.Success;
        }

        private void Fire()
        {
            lastFireTime = Time.time;

            // Consume ammo
            if (weaponData.UseAmmo)
            {
                currentAmmo--;
                OnAmmoChanged?.Invoke(currentAmmo, weaponData.ClipSize);
            }

            // Fire event and SFX
            PlaySound(fireSFX);
            onFire?.Invoke();

            // Perform raycast(s)
            for (int i = 0; i < weaponData.PelletsPerShot; i++)
            {
                PerformRaycast();
            }
        }

        private void PerformRaycast()
        {
            Vector3 origin = firePoint.position;
            Vector3 direction = GetFireDirection();

            if (Physics.Raycast(origin, direction, out RaycastHit hit, weaponData.MaxRange, hitLayers))
            {
                ProcessHit(hit);
            }

            // Debug visualization
            Debug.DrawRay(origin, direction * weaponData.MaxRange, Color.red, 0.1f);
        }

        private Vector3 GetFireDirection()
        {
            Vector3 baseDirection = firePoint.forward;

            if (weaponData.SpreadAngle <= 0f)
            {
                return baseDirection;
            }

            // Apply random spread within cone
            float spreadRad = weaponData.SpreadAngle * Mathf.Deg2Rad;
            float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float randomRadius = UnityEngine.Random.Range(0f, Mathf.Tan(spreadRad));

            Vector3 perpendicular = Vector3.Cross(baseDirection, Vector3.up).normalized;
            if (perpendicular.sqrMagnitude < 0.01f)
            {
                perpendicular = Vector3.Cross(baseDirection, Vector3.right).normalized;
            }
            Vector3 perpendicular2 = Vector3.Cross(baseDirection, perpendicular).normalized;

            Vector3 offset = perpendicular * Mathf.Cos(randomAngle) * randomRadius +
                           perpendicular2 * Mathf.Sin(randomAngle) * randomRadius;

            return (baseDirection + offset).normalized;
        }

        private void ProcessHit(RaycastHit hit)
        {
            // Find IDamageable on hit object
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = hit.collider.GetComponentInParent<IDamageable>();
            }

            if (damageable != null)
            {
                // Determine damage type from target if possible
                DamageType damageType = DamageType.Flesh;  // Default for ranged

                // Check if target specifies a damage type
                var damageReceiver = hit.collider.GetComponent<IDamageTypeProvider>();
                if (damageReceiver != null)
                {
                    damageType = damageReceiver.DamageType;
                }

                float damage = weaponData.GetDamage(damageType);
                damageable.TakeDamage(damage, damageType, ownerTransform.gameObject);

                NotifyHit();
            }

            // Always try to play hit effects
            var hitEffects = hit.collider.GetComponent<HitEffectReceiver>();
            if (hitEffects == null)
            {
                hitEffects = hit.collider.GetComponentInParent<HitEffectReceiver>();
            }
            hitEffects?.PlayHitEffect(hit.point, hit.normal);
        }

        public void NotifyHit()
        {
            onHit?.Invoke();
        }

        public float GetDamage(DamageType targetType)
        {
            return weaponData != null ? weaponData.GetDamage(targetType) : 0f;
        }

        public float GetRange()
        {
            return weaponData != null ? weaponData.MaxRange : 0f;
        }

        /// <summary>
        /// Start reloading. Can be called externally (e.g., by reload input).
        /// </summary>
        public void StartReload()
        {
            if (isReloading) return;
            if (currentAmmo >= weaponData.ClipSize) return;  // Already full

            isReloading = true;
            reloadStartTime = Time.time;

            PlaySound(reloadSFX);
            onReloadStart?.Invoke();
            OnReloadStarted?.Invoke();
        }

        private void CompleteReload()
        {
            isReloading = false;
            currentAmmo = weaponData.ClipSize;

            onReloadComplete?.Invoke();
            OnReloadCompleted?.Invoke();
            OnAmmoChanged?.Invoke(currentAmmo, weaponData.ClipSize);
        }

        /// <summary>
        /// Force reload cancellation (e.g., when unequipped).
        /// </summary>
        public void CancelReload()
        {
            isReloading = false;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, firePoint.position, sfxVolume);
            }
        }

        private void OnDisable()
        {
            // Cancel reload when unequipped
            CancelReload();
        }
    }
}
```

#### Step 9.3: Create IDamageTypeProvider Interface (Optional)

Create: `Assets/Scripts/Combat/IDamageTypeProvider.cs`

```csharp
namespace StillOrbit.Combat
{
    /// <summary>
    /// Interface for objects that specify what damage type should be used against them.
    /// </summary>
    public interface IDamageTypeProvider
    {
        DamageType DamageType { get; }
    }
}
```

This allows targets (trees, rocks, creatures) to specify their vulnerability type.

### Validation Checklist

- [ ] `RangedWeaponData` compiles, shows in Create menu
- [ ] `RangedWeapon` compiles
- [ ] Can create ranged weapon asset
- [ ] Can attach `RangedWeapon` to held prefab
- [ ] Pressing primary action fires raycast (see debug line)
- [ ] Fire rate limits fire speed
- [ ] Hitting `IDamageable` applies damage

### What "Done" Looks Like

A functional raycast weapon that:
- Fires on primary action input
- Respects fire rate cooldown
- Hits targets via raycast
- Applies damage to `IDamageable` objects
- Has placeholder SFX hooks

---

## Phase 10 — Ammo, Reloading, Fire Rate

### Goal

Complete the ammo and reload systems with proper input integration.

### What Already Exists

- `RangedWeapon` has `currentAmmo`, `StartReload()`, `CompleteReload()`
- `RangedWeaponData` defines `clipSize`, `reloadTime`, `useAmmo`

### What Will Be Added

1. Reload input action and handling
2. Ammo UI display
3. Auto-reload when empty (optional)

### Implementation Steps

#### Step 10.1: Add Reload Input Action

Open `Assets/Settings/PlayerControls.inputactions`:

1. Add action: `Reload`
2. Type: Button
3. Binding: **R** (Keyboard)
4. Save

#### Step 10.2: Extend PlayerInputHandler

Add to `PlayerInputHandler.cs`:

```csharp
public bool ReloadPressed { get; private set; }

// In OnEnable:
playerControls.Player.Reload.performed += OnReload;

// In OnDisable:
playerControls.Player.Reload.performed -= OnReload;

// Callback:
private void OnReload(InputAction.CallbackContext context)
{
    ReloadPressed = true;
}

// In LateUpdate:
ReloadPressed = false;
```

#### Step 10.3: Handle Reload Input

Update `PlayerInteractionController.cs` or create dedicated `WeaponInputController.cs`:

```csharp
// In Update:
if (inputHandler.ReloadPressed)
{
    HandleReload();
}

private void HandleReload()
{
    if (equipmentController.EquippedObject == null) return;

    var rangedWeapon = equipmentController.EquippedObject.GetComponent<RangedWeapon>();
    if (rangedWeapon != null)
    {
        rangedWeapon.StartReload();
    }
}
```

#### Step 10.4: Create Ammo Display UI

Create: `Assets/Scripts/UI/Combat/AmmoDisplay.cs`

```csharp
using UnityEngine;
using TMPro;

namespace StillOrbit.UI.Combat
{
    /// <summary>
    /// Displays current ammo count for equipped ranged weapon.
    /// </summary>
    public class AmmoDisplay : MonoBehaviour
    {
        [SerializeField] private PlayerEquipmentController equipmentController;
        [SerializeField] private TextMeshProUGUI ammoText;
        [SerializeField] private GameObject displayRoot;

        private RangedWeapon currentWeapon;

        private void Update()
        {
            UpdateWeaponReference();
        }

        private void UpdateWeaponReference()
        {
            RangedWeapon newWeapon = null;

            if (equipmentController.EquippedObject != null)
            {
                newWeapon = equipmentController.EquippedObject.GetComponent<RangedWeapon>();
            }

            if (newWeapon != currentWeapon)
            {
                // Unsubscribe from old
                if (currentWeapon != null)
                {
                    currentWeapon.OnAmmoChanged -= UpdateAmmoDisplay;
                    currentWeapon.OnReloadStarted -= OnReloadStarted;
                    currentWeapon.OnReloadCompleted -= OnReloadCompleted;
                }

                currentWeapon = newWeapon;

                // Subscribe to new
                if (currentWeapon != null)
                {
                    currentWeapon.OnAmmoChanged += UpdateAmmoDisplay;
                    currentWeapon.OnReloadStarted += OnReloadStarted;
                    currentWeapon.OnReloadCompleted += OnReloadCompleted;

                    displayRoot.SetActive(true);
                    UpdateAmmoDisplay(currentWeapon.CurrentAmmo, currentWeapon.MaxAmmo);
                }
                else
                {
                    displayRoot.SetActive(false);
                }
            }
        }

        private void UpdateAmmoDisplay(int current, int max)
        {
            ammoText.text = $"{current} / {max}";
        }

        private void OnReloadStarted()
        {
            ammoText.text = "RELOADING...";
        }

        private void OnReloadCompleted()
        {
            if (currentWeapon != null)
            {
                UpdateAmmoDisplay(currentWeapon.CurrentAmmo, currentWeapon.MaxAmmo);
            }
        }
    }
}
```

#### Step 10.5: Optional Auto-Reload

Add to `RangedWeapon.cs` in the `Fire()` method:

```csharp
// After consuming ammo, check for empty
if (currentAmmo <= 0 && autoReload)
{
    StartReload();
}
```

Add field:
```csharp
[Header("Behavior")]
[SerializeField] private bool autoReload = true;
```

### Validation Checklist

- [ ] Pressing **R** starts reload
- [ ] Reload takes `reloadTime` seconds
- [ ] Ammo refills to `clipSize` after reload
- [ ] Cannot fire while reloading
- [ ] Ammo display shows current/max
- [ ] Ammo display shows "RELOADING" during reload
- [ ] Display hides when no ranged weapon equipped

### What "Done" Looks Like

Complete ammo system with:
- Manual reload via R key
- Visual ammo counter
- Reload feedback
- Auto-reload when empty (optional)

---

## Phase 11 — VFX & SFX Integration

### Goal

Add visual and audio feedback for ranged weapon firing and impacts.

### What Will Be Added

1. Muzzle flash VFX
2. Projectile/beam visual (optional)
3. Impact VFX
4. Organized SFX system

### Implementation Steps

#### Step 11.1: Muzzle Flash System

Add to `RangedWeapon.cs`:

```csharp
[Header("Visual Effects")]
[SerializeField] private ParticleSystem muzzleFlash;
[SerializeField] private LineRenderer beamRenderer;  // For laser weapons
[SerializeField] private float beamDuration = 0.1f;
[SerializeField] private GameObject impactVFXPrefab;
[SerializeField] private float impactVFXLifetime = 2f;

private void Fire()
{
    // ... existing fire logic ...

    // Play muzzle flash
    if (muzzleFlash != null)
    {
        muzzleFlash.Play();
    }
}

private void ProcessHit(RaycastHit hit)
{
    // ... existing hit processing ...

    // Spawn impact VFX
    if (impactVFXPrefab != null)
    {
        GameObject impact = Instantiate(impactVFXPrefab, hit.point,
            Quaternion.LookRotation(hit.normal));
        Destroy(impact, impactVFXLifetime);
    }

    // Show beam if using LineRenderer
    if (beamRenderer != null)
    {
        ShowBeam(firePoint.position, hit.point);
    }
}

private void ShowBeam(Vector3 start, Vector3 end)
{
    beamRenderer.enabled = true;
    beamRenderer.SetPosition(0, start);
    beamRenderer.SetPosition(1, end);

    // Disable after duration
    StartCoroutine(HideBeamAfterDelay());
}

private System.Collections.IEnumerator HideBeamAfterDelay()
{
    yield return new WaitForSeconds(beamDuration);
    if (beamRenderer != null)
    {
        beamRenderer.enabled = false;
    }
}
```

#### Step 11.2: Audio Organization

Create a centralized audio approach using ScriptableObject:

Create: `Assets/Scripts/Audio/WeaponAudioData.cs`

```csharp
using UnityEngine;

namespace StillOrbit.Audio
{
    /// <summary>
    /// Audio configuration for a weapon.
    /// </summary>
    [CreateAssetMenu(fileName = "New Weapon Audio", menuName = "StillOrbit/Audio/Weapon Audio")]
    public class WeaponAudioData : ScriptableObject
    {
        [Header("Fire Sounds")]
        public AudioClip[] fireSounds;
        [Range(0f, 1f)] public float fireVolume = 1f;
        [Range(0.8f, 1.2f)] public float firePitchVariation = 0.1f;

        [Header("Reload Sounds")]
        public AudioClip reloadStart;
        public AudioClip reloadComplete;
        [Range(0f, 1f)] public float reloadVolume = 1f;

        [Header("Empty/Dry Fire")]
        public AudioClip emptyClick;
        [Range(0f, 1f)] public float emptyVolume = 0.8f;

        [Header("Impact Sounds")]
        public AudioClip[] impactSounds;
        [Range(0f, 1f)] public float impactVolume = 1f;

        public AudioClip GetRandomFireSound()
        {
            if (fireSounds == null || fireSounds.Length == 0) return null;
            return fireSounds[Random.Range(0, fireSounds.Length)];
        }

        public AudioClip GetRandomImpactSound()
        {
            if (impactSounds == null || impactSounds.Length == 0) return null;
            return impactSounds[Random.Range(0, impactSounds.Length)];
        }
    }
}
```

Update `RangedWeapon.cs` to use the audio data:

```csharp
[Header("Audio")]
[SerializeField] private WeaponAudioData audioData;
[SerializeField] private AudioSource audioSource;  // Optional: dedicated AudioSource

private void PlayFireSound()
{
    if (audioData == null) return;

    AudioClip clip = audioData.GetRandomFireSound();
    if (clip == null) return;

    if (audioSource != null)
    {
        audioSource.pitch = 1f + Random.Range(-audioData.firePitchVariation, audioData.firePitchVariation);
        audioSource.PlayOneShot(clip, audioData.fireVolume);
    }
    else
    {
        AudioSource.PlayClipAtPoint(clip, firePoint.position, audioData.fireVolume);
    }
}
```

#### Step 11.3: Impact Effect Integration

The existing `HitEffectReceiver` already handles impact VFX/SFX. Ensure ranged weapons call it:

```csharp
private void ProcessHit(RaycastHit hit)
{
    // ... damage logic ...

    // Play hit effects on target
    var hitEffects = hit.collider.GetComponent<HitEffectReceiver>();
    if (hitEffects == null)
    {
        hitEffects = hit.collider.GetComponentInParent<HitEffectReceiver>();
    }

    if (hitEffects != null)
    {
        hitEffects.PlayHitEffect(hit.point, hit.normal);
    }
    else if (impactVFXPrefab != null)
    {
        // Fallback: spawn generic impact if target has no receiver
        SpawnImpactVFX(hit.point, hit.normal);
    }
}

private void SpawnImpactVFX(Vector3 position, Vector3 normal)
{
    GameObject impact = Instantiate(impactVFXPrefab, position,
        Quaternion.LookRotation(normal));
    Destroy(impact, impactVFXLifetime);
}
```

### Validation Checklist

- [ ] Muzzle flash plays on fire
- [ ] Beam/tracer shows briefly (if configured)
- [ ] Impact VFX spawns at hit point
- [ ] Fire SFX plays with variation
- [ ] Empty click plays when out of ammo
- [ ] Reload sounds play at start/end
- [ ] All effects clean up properly (no leaking GameObjects)

### What "Done" Looks Like

Polished ranged weapon feedback:
- Visual muzzle flash
- Optional beam/tracer visual
- Impact effects at hit location
- Varied audio for immersion
- Clean effect lifecycle

---

## Phase 12 — Integration Testing Scenarios

### Goal

Validate that both systems work correctly in realistic gameplay scenarios.

### Test Scenarios

#### Inventory UI Tests

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 1 | Toggle empty inventory | Press I with no items | Panel opens showing 20 empty slots |
| 2 | Toggle with items | Pick up items, press I | Panel shows items with icons |
| 3 | Stack display | Pick up 5 stackable items | Slot shows "5" quantity |
| 4 | Non-stackable | Pick up weapon | Slot shows icon, no quantity |
| 5 | Real-time update | Open panel, pick up item | New item appears without re-toggle |
| 6 | Drop while open | Open panel, drop item | Slot clears immediately |

#### Quick Slot Tests

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 7 | Auto-assign | Pick up sword | Appears in first empty quick slot |
| 8 | Select slot | Press 1 with sword assigned | Sword equips, slot highlights |
| 9 | Deselect slot | Press 1 again | Sword unequips, highlight removed |
| 10 | Swap slots | Press 1, then press 2 | Swaps to slot 2's item |
| 11 | Empty slot select | Press empty slot number | Nothing equips, no errors |
| 12 | Drop active item | Press Q with item equipped | Item drops, quick slot clears |

#### Melee Weapon Tests

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 13 | Basic attack | Equip sword, click | Swing animation, SFX plays |
| 14 | Attack cooldown | Spam click | Cannot attack faster than rate |
| 15 | Hit detection | Attack damageable target | Damage applied, hit effects play |
| 16 | Miss | Attack empty air | Animation plays, no errors |
| 17 | Damage types | Attack tree, rock, creature | Different damage per type |

#### Ranged Weapon Tests

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 18 | Basic fire | Equip gun, click | Raycast fires, muzzle flash |
| 19 | Fire rate | Hold click | Fires at configured rate |
| 20 | Ammo depletion | Fire until empty | Ammo decreases, empty click plays |
| 21 | Manual reload | Press R mid-clip | Reload starts, completes after time |
| 22 | Auto reload | Fire until empty | Automatically starts reload |
| 23 | Can't fire reloading | Press click while reloading | No fire, no error |
| 24 | Hit detection | Shoot damageable target | Damage applied, impact VFX |
| 25 | Max range | Shoot beyond range | Raycast stops at max range |

#### Weapon Switching Tests

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 26 | Melee to ranged | Press 1 (sword), press 2 (gun) | Clean swap, no overlap |
| 27 | Ranged to melee | Reverse of above | Clean swap |
| 28 | Swap during reload | Start reload, press different slot | Reload cancels, swaps cleanly |
| 29 | Swap during attack | Start melee, press different slot | Attack completes or cancels |

### Automated Test Ideas

If using Unity Test Framework:

```csharp
[Test]
public void QuickSlot_Assignment_RejectsNonEquippable()
{
    // Create non-equippable item
    var item = ScriptableObject.CreateInstance<ItemData>();
    item.canEquip = false;

    // Add to inventory
    inventory.TryAddItem(item);
    int inventoryIndex = inventory.FindItem(item);

    // Try to assign to quick slot
    bool result = quickSlotManager.AssignToQuickSlot(0, inventoryIndex);

    Assert.IsFalse(result);
    Assert.IsNull(quickSlotManager.GetQuickSlotItem(0));
}
```

### Validation Checklist

- [ ] All inventory UI tests pass
- [ ] All quick slot tests pass
- [ ] All melee weapon tests pass
- [ ] All ranged weapon tests pass
- [ ] All weapon switching tests pass
- [ ] No errors in console during testing
- [ ] Performance acceptable (no frame drops)

---

## Phase 13 — Common Pitfalls & Debugging Tips

### Inventory System Pitfalls

#### Pitfall 1: Duplicate Item References

**Symptom:** Removing one item affects another slot.

**Cause:** Storing the same `ItemData` reference in multiple slots without proper quantity tracking.

**Solution:** Always use `InventorySlot` which tracks both `ItemData` and `Quantity`. The current system is correct.

#### Pitfall 2: UI Not Updating

**Symptom:** Picking up items doesn't show in inventory panel.

**Cause:** Missing event subscription or panel not active.

**Debug:**
```csharp
// Add to PlayerInventory.TryAddItem
Debug.Log($"[Inventory] Added {item.ItemName}, firing OnInventoryChanged({slotIndex})");
```

Check that `InventoryPanel.OnSlotChanged` is being called.

#### Pitfall 3: Quick Slot Shows Wrong Item

**Symptom:** Quick slot displays item that's not in inventory.

**Cause:** Stale inventory index after items shifted.

**Solution:** The current design uses inventory indices which can become stale if items shift. Consider storing `ItemData` directly in quick slots, or re-validating indices.

**Robust fix:**
```csharp
public ItemData GetQuickSlotItem(int quickSlotIndex)
{
    int invIndex = quickSlotAssignments[quickSlotIndex];
    if (invIndex < 0) return null;

    InventorySlot slot = playerInventory.GetSlot(invIndex);

    // Validate the slot still contains expected item
    if (slot == null || slot.IsEmpty)
    {
        // Clear stale assignment
        quickSlotAssignments[quickSlotIndex] = -1;
        return null;
    }

    return slot.ItemData;
}
```

### Weapon System Pitfalls

#### Pitfall 4: Hitbox Detects Self

**Symptom:** Player takes damage from own weapon.

**Cause:** Layer mask includes player layer.

**Solution:** `WeaponHitbox` already has self-collision prevention. Ensure player root is properly set.

#### Pitfall 5: Raycast Ignores Targets

**Symptom:** Ranged weapon doesn't hit anything.

**Debug:**
```csharp
private void PerformRaycast()
{
    Debug.DrawRay(origin, direction * maxRange, Color.red, 1f);  // Longer duration

    if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, hitLayers))
    {
        Debug.Log($"[RangedWeapon] Hit: {hit.collider.name} at {hit.point}");
    }
    else
    {
        Debug.Log($"[RangedWeapon] No hit. Layers: {hitLayers.value}");
    }
}
```

**Common causes:**
- `hitLayers` set to Nothing (0)
- Target on wrong layer
- `firePoint` inside geometry

#### Pitfall 6: Cooldown Not Working

**Symptom:** Can fire/attack too fast.

**Cause:** `Time.time` comparison issue or rate misconfiguration.

**Debug:**
```csharp
Debug.Log($"LastFire: {lastFireTime}, Now: {Time.time}, Interval: {FireInterval}, CanUse: {CanUse}");
```

Ensure `attackRate`/`fireRate` is > 0.

#### Pitfall 7: Reload State Stuck

**Symptom:** Weapon stuck in "RELOADING" forever.

**Cause:** `Update()` not running (disabled component) or time comparison error.

**Solution:** Add safety check:
```csharp
private void OnEnable()
{
    // Reset reload state when re-enabled
    isReloading = false;
}
```

### General Debugging Commands

Add debug methods to key components:

```csharp
// PlayerInventory
[ContextMenu("Debug: Log All Slots")]
public void DebugLogSlots()
{
    for (int i = 0; i < slots.Count; i++)
    {
        var slot = slots[i];
        Debug.Log($"Slot {i}: {(slot.IsEmpty ? "Empty" : $"{slot.ItemData.ItemName} x{slot.Quantity}")}");
    }
}

// QuickSlotManager
[ContextMenu("Debug: Log Quick Slots")]
public void DebugLogQuickSlots()
{
    for (int i = 0; i < QUICK_SLOT_COUNT; i++)
    {
        var item = GetQuickSlotItem(i);
        Debug.Log($"QuickSlot {i}: {(item == null ? "Empty" : item.ItemName)}");
    }
    Debug.Log($"Active slot: {activeSlotIndex}");
}
```

### Console Error Quick Reference

| Error | Likely Cause | Fix |
|-------|--------------|-----|
| `NullReferenceException` in UI | Missing prefab/reference | Check Inspector bindings |
| `MissingReferenceException` | Destroyed object access | Null check before use |
| "No weapon data assigned" | `RangedWeaponData` not set | Assign in held prefab |
| "Missing hold point" | `itemHoldPoint` null | Assign Transform in player |

---

## Phase 14 — Extension Hooks

### Goal

Document how future features can extend these systems without refactoring.

### Inventory Extensions

#### Item Rarity/Quality

Extend `ItemData`:
```csharp
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

// In ItemData:
[SerializeField] private ItemRarity rarity = ItemRarity.Common;
public ItemRarity Rarity => rarity;
```

Update `InventorySlotUI` to show rarity color:
```csharp
slotBackground.color = GetRarityColor(itemData.Rarity);
```

#### Item Durability

Add interface `IDurable`:
```csharp
public interface IDurable
{
    int CurrentDurability { get; }
    int MaxDurability { get; }
    void TakeDurabilityDamage(int amount);
    event Action<int, int> OnDurabilityChanged;
}
```

Implement on runtime item components (not ScriptableObjects).

#### Crafting Integration

Quick slots can show craftable item previews:
```csharp
// In QuickSlotUI
public void ShowCraftPreview(ItemData recipe)
{
    // Show ghosted version
    iconImage.color = new Color(1, 1, 1, 0.5f);
    UpdateDisplay(recipe);
}
```

#### Drag & Drop

Add `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler` to `InventorySlotUI`:
```csharp
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Implement drag handlers
    // On drop: call PlayerInventory.SwapSlots(from, to)
}
```

### Weapon Extensions

#### Weapon Attachments/Mods

Create `WeaponModData`:
```csharp
[CreateAssetMenu(menuName = "StillOrbit/Items/Weapon Mod")]
public class WeaponModData : ScriptableObject
{
    public float damageMultiplier = 1f;
    public float fireRateMultiplier = 1f;
    public float rangeBonus = 0f;
    public float spreadReduction = 0f;
}
```

Add mod slots to weapons:
```csharp
// In RangedWeapon
private List<WeaponModData> attachedMods = new List<WeaponModData>();

public float GetModifiedDamage(DamageType type)
{
    float base = weaponData.GetDamage(type);
    foreach (var mod in attachedMods)
        base *= mod.damageMultiplier;
    return base;
}
```

#### Alternate Fire Modes

Add fire mode enum:
```csharp
public enum FireMode { Single, Burst, Auto }

// In RangedWeaponData
[SerializeField] private FireMode[] availableModes = { FireMode.Auto };
```

Handle mode switching:
```csharp
// In RangedWeapon
private int currentModeIndex = 0;

public void CycleFireMode()
{
    currentModeIndex = (currentModeIndex + 1) % weaponData.AvailableModes.Length;
    // Update behavior based on mode
}
```

#### Projectile Weapons (Non-Hitscan)

Create `ProjectileWeapon` alongside `RangedWeapon`:
```csharp
public class ProjectileWeapon : MonoBehaviour, IUsable, IWeapon
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 50f;

    private void Fire()
    {
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.GetComponent<Rigidbody>().velocity = firePoint.forward * projectileSpeed;
        proj.GetComponent<Projectile>().Initialize(GetDamage(DamageType.Flesh), ownerTransform);
    }
}
```

#### Melee Combos

Track combo state:
```csharp
// In MeleeWeapon
private int comboIndex = 0;
private float comboTimeout = 0.5f;
private float lastAttackTime;

public UseResult Use(GameObject user)
{
    // Reset combo if timeout elapsed
    if (Time.time > lastAttackTime + comboTimeout)
        comboIndex = 0;

    // Trigger combo-specific animation
    animator.SetInteger("ComboIndex", comboIndex);
    animator.SetTrigger("MeleeAttack");

    comboIndex = (comboIndex + 1) % maxComboLength;
    lastAttackTime = Time.time;

    return UseResult.Success;
}
```

#### Charged Attacks

Add charge mechanic:
```csharp
// In MeleeWeapon or RangedWeapon
private float chargeStartTime;
private bool isCharging;

public void StartCharge()
{
    isCharging = true;
    chargeStartTime = Time.time;
}

public void ReleaseCharge()
{
    if (!isCharging) return;

    float chargeTime = Time.time - chargeStartTime;
    float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);
    float damage = baseDamage * (1 + chargePercent * chargeDamageBonus);

    // Execute attack with modified damage
    isCharging = false;
}
```

### Integration Hooks

#### Save/Load

Inventory is serializable by storing:
```csharp
[Serializable]
public struct SavedInventorySlot
{
    public string itemId;  // ItemData.ItemId
    public int quantity;
}
```

Quick slots store indices which reference inventory.

#### Achievements/Stats

Subscribe to events:
```csharp
quickSlotManager.OnActiveSlotChanged += (prev, current) =>
    Analytics.Track("weapon_switched");

rangedWeapon.OnAmmoChanged += (current, max) =>
    if (current == 0) Analytics.Track("clip_emptied");
```

#### Localization

Item names come from `ItemData.ItemName`. For localization:
```csharp
// In ItemData
public string GetLocalizedName()
{
    return LocalizationManager.Get($"item_{itemId}_name") ?? itemName;
}
```

---

## Summary

This guide provides a complete, incremental path from the existing StillOrbit codebase to full inventory UI and weapon systems:

### Inventory System Additions
- **Phase 1-2:** Inventory panel with grid display
- **Phase 3-4:** Quick slot data model and HUD
- **Phase 5:** Number key switching

### Weapon System Additions
- **Phase 7:** Unified lifecycle contract
- **Phase 9-10:** Ranged weapons with ammo/reload
- **Phase 11:** VFX/SFX polish

### Key Architectural Decisions

1. **Quick slots reference inventory indices** — No data duplication
2. **RangedWeaponData extends WeaponData** — Clean inheritance
3. **IUsable for all usable items** — Unified use pattern
4. **IWeapon for weapon queries** — Optional polymorphism
5. **Event-driven UI** — Reactive to data changes

### Files Created (New)

| Path | Purpose |
|------|---------|
| `UI/Inventory/InventoryUIController.cs` | Toggle input → UI |
| `UI/Inventory/InventoryPanel.cs` | Main panel |
| `UI/Inventory/InventorySlotUI.cs` | Slot display |
| `UI/Inventory/QuickSlotHUD.cs` | Always-visible bar |
| `UI/Inventory/QuickSlotUI.cs` | Quick slot display |
| `Inventory/QuickSlotManager.cs` | Quick slot data |
| `Inventory/QuickSlotInputController.cs` | Number keys |
| `Items/Data/RangedWeaponData.cs` | Ranged config |
| `Items/RangedWeapon.cs` | Ranged behavior |
| `Combat/IWeapon.cs` | Weapon interface |
| `UI/Combat/AmmoDisplay.cs` | Ammo counter |
| `Audio/WeaponAudioData.cs` | Audio config |

### Files Modified

| Path | Changes |
|------|---------|
| `PlayerControls.inputactions` | Add ToggleInventory, QuickSlot1-5, Reload |
| `PlayerInputHandler.cs` | Handle new inputs |
| `PlayerInteractionController.cs` | Auto-assign quick slots |
| `PlayerInventory.cs` | Add SlotCount property |
| `MeleeWeapon.cs` | Implement IWeapon (optional) |

---

*End of Implementation Guide*
