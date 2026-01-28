# StillOrbit: SFX Extensions & Player Death System Guide

**Version:** 1.0
**Target:** Unity 6 / C# / Single-player
**Approach:** Additive extension of existing Audio, Health, and Inventory systems
**Prerequisite:** [AudioSystemGuide.md](AudioSystemGuide.md) must be implemented first

---

## Table of Contents

1. [Phase 0 — Review of Existing Systems](#phase-0--review-of-existing-systems)
2. [Phase 1 — SFX Data Model Extensions](#phase-1--sfx-data-model-extensions)
3. [Phase 2 — Enemy SFX Integration](#phase-2--enemy-sfx-integration)
4. [Phase 3 — SFX Cooldowns & Anti-Spam](#phase-3--sfx-cooldowns--anti-spam)
5. [Phase 4 — Footstep System Core](#phase-4--footstep-system-core)
6. [Phase 5 — Player Footsteps](#phase-5--player-footsteps)
7. [Phase 6 — Enemy Footsteps](#phase-6--enemy-footsteps)
8. [Phase 7 — Player Death Detection](#phase-7--player-death-detection)
9. [Phase 8 — Inventory Drop Logic](#phase-8--inventory-drop-logic)
10. [Phase 9 — Death Screen Timer Logic](#phase-9--death-screen-timer-logic)
11. [Phase 10 — Respawn Flow](#phase-10--respawn-flow)
12. [Phase 11 — Integration & Edge Cases](#phase-11--integration--edge-cases)
13. [Phase 12 — Debugging & Failure Modes](#phase-12--debugging--failure-modes)
14. [Phase 13 — Extension Hooks](#phase-13--extension-hooks)

---

## Phase 0 — Review of Existing Systems

### Goal

Document what exists, confirm integration points, and establish contracts for everything that follows.

### What Already Exists

#### Audio System (from AudioSystemGuide.md)

| Component | Location | Relevance |
|-----------|----------|-----------|
| `AudioManager` | `Scripts/Audio/AudioManager.cs` | Singleton music authority — SFX does NOT go through this |
| `MusicState` | `Scripts/Audio/MusicState.cs` | Includes `GameOver` state used by death system |
| `WeaponAudioData` | `Scripts/Audio/WeaponAudioData.cs` | Existing SFX pattern — randomized clips + volume + pitch |
| `HitEffectReceiver` | `Scripts/Combat/HitEffectReceiver.cs` | Per-object hit SFX via `PlayClipAtPoint` |

#### Health & Damage System

| Component | Location | Integration Point |
|-----------|----------|-------------------|
| `HealthComponent` | `Scripts/Health/HealthComponent.cs` | `OnDeath` event triggers death flow; `OnHealthChanged` triggers hurt SFX |
| `IDamageable` | `Scripts/Combat/IDamageable.cs` | All damage flows through this interface |

#### Inventory Systems

| Component | Location | Integration Point |
|-----------|----------|-------------------|
| `PlayerInventory` | `Scripts/Inventory/PlayerInventory.cs` | `TryRemoveItem`, `GetSlot` for drop-on-death |
| `PlayerResourceInventory` | `Scripts/Resources/PlayerResourceInventory.cs` | `GetInventory().Clear()` for resource wipe |
| `PlayerEquipmentController` | `Scripts/Player/PlayerEquipmentController.cs` | `UnequipItem` to remove held item visual |
| `ItemData.WorldPrefab` | `Scripts/Items/Data/ItemData.cs` | Prefab instantiated when items are dropped |

#### Enemy System

| Component | Location | Integration Point |
|-----------|----------|-------------------|
| `EnemyController` | `Scripts/AI/Enemy/EnemyController.cs` | `OnDeath`, `OnStateChanged` events for SFX triggers |
| `EnemyArchetype` | `Scripts/AI/Enemy/Data/EnemyArchetype.cs` | New `SFXData` field added for per-type audio config |
| `EnemyDeadState` | `Scripts/AI/Enemy/States/EnemyDeadState.cs` | Death SFX must survive GameObject destruction |

#### Player Movement

| Component | Location | Integration Point |
|-----------|----------|-------------------|
| `PlayerLocomotionController` | `Scripts/Player/PlayerLocomotionController.cs` | `Motor.GroundingStatus.IsStableOnGround` for footstep gating; `Motor.BaseVelocity` for speed |
| `KinematicCharacterMotor` | KCC package | `SetPosition()` for respawn teleport |

#### UI System

| Component | Location | Integration Point |
|-----------|----------|-------------------|
| `UIManager` | `Scripts/UI/Core/UIManager.cs` | Auto-discovers `UIPanel` children; death screen is a panel |
| `UIPanel` | `Scripts/UI/Core/UIPanel.cs` | `Show()`/`Hide()` via `CanvasGroup` — death screen extends this |

### Architecture Decisions

1. **SFX stays decentralized.** Unlike music (one `AudioManager`), SFX is played at the point of origin. Each system owns its own `AudioSource` or uses `PlayClipAtPoint`. This is consistent with `WeaponAudioData` and `HitEffectReceiver`.

2. **Enemy SFX is data-driven.** A new `EnemySFXData` ScriptableObject per enemy type. No hard-coded clips in AI states.

3. **Footsteps are distance-driven, not frame-based.** Distance traveled determines when steps play, not animation events. This is more reliable across different animation rigs and movement speeds.

4. **Death uses events, not polling.** `HealthComponent.OnDeath` fires once → `PlayerDeathController` handles everything → UI subscribes to death controller events.

### What Will NOT Change

- `HealthComponent` internals
- `PlayerInventory` internals
- `ResourceInventory` internals
- `PlayerEquipmentController` core logic
- `EnemyController` state machine or death handling
- `UIManager` / `UIPanel` base architecture
- `WeaponAudioData` or `HitEffectReceiver`

---

## Phase 1 — SFX Data Model Extensions

### Goal

Create data containers for enemy SFX and footstep surfaces, following the `WeaponAudioData` pattern.

### What Already Exists

`WeaponAudioData` established the pattern: ScriptableObject with `AudioClip[]` arrays, `[Range]` volume controls, and helper methods for random selection.

### What Will Be Added

#### `EnemySFXData.cs` — `Scripts/Audio/EnemySFXData.cs`

ScriptableObject with categorized sound entries:

- **Death** — one-shot clips, guaranteed to play once
- **Attack** — played when entering Attack state
- **Aggro** — played on first detection (Idle/Patrol → Chase)
- **Hurt** — played on damage/stagger
- **Idle** — ambient vocalizations with long cooldowns

Each category is an `SFXEntry` with:
- `AudioClip[] clips` — random selection pool
- `float volume` — playback volume
- `float pitchVariation` — random pitch offset per play
- `float cooldown` — minimum seconds between plays

#### `FootstepSurfaceData.cs` — `Scripts/Audio/FootstepSurfaceData.cs`

ScriptableObject mapping surface types to footstep clips:

- Surfaces identified by `PhysicsMaterial` name (case-insensitive)
- Default fallback surface for untagged ground
- Per-surface volume and pitch variation
- Runtime lookup cache for O(1) surface resolution

### What Is Explicitly NOT Changing

- `WeaponAudioData` — untouched, continues to work independently
- `HitEffectReceiver` — untouched

### Asset Setup

1. **Create per-enemy-type SFX assets:**
   Right-click → Create → StillOrbit → Audio → Enemy SFX
   Name: `WitchHag SFX`, `Grunt SFX`, etc.
   Place in: `Assets/Data/Audio/Enemies/`

2. **Create one footstep surface asset:**
   Right-click → Create → StillOrbit → Audio → Footstep Surface Data
   Name: `FootstepSurfaceData`
   Place in: `Assets/Data/Audio/`

### Validation Checklist

- [ ] `EnemySFXData` asset created with at least one death clip
- [ ] `FootstepSurfaceData` asset created with a default surface entry
- [ ] Both assets appear under the StillOrbit/Audio menu in Create
- [ ] `SFXEntry.GetRandomClip()` returns null for empty arrays (no exception)

### What "Done" Looks Like

Two new ScriptableObject types exist with clear Inspector UIs. No runtime behavior yet.

---

## Phase 2 — Enemy SFX Integration

### Goal

Create `EnemyAudioHandler` — a component that listens to enemy events and plays the correct SFX with proper timing.

### What Already Exists

- `EnemyController.OnStateChanged` fires on every state transition
- `EnemyController.OnDeath` fires when the enemy dies
- `HealthComponent.OnHealthChanged` fires on damage
- `EnemyArchetype` now has an `SFXData` field (added in Phase 1)

### What Will Be Added

#### `EnemyAudioHandler.cs` — `Scripts/Audio/EnemyAudioHandler.cs`

Component attached to enemy prefabs. Responsibilities:

1. Resolves `EnemySFXData` from archetype or explicit override
2. Subscribes to `EnemyController.OnStateChanged`, `OnDeath`, `HealthComponent.OnHealthChanged`
3. Plays SFX based on state transitions:
   - `Idle/Patrol → Chase` = aggro sound
   - `→ Attack` = attack sound
   - `→ Hurt` = hurt sound
   - `→ Dead` = death sound (via `PlayClipAtPoint` for survival)
4. Idle vocalizations in `Update()` with cooldown gating
5. Per-category cooldown enforcement

**Key Design Decision:** Death SFX uses `AudioSource.PlayClipAtPoint()` instead of the instance's `AudioSource`. This guarantees the sound completes even if the enemy GameObject is destroyed mid-playback.

#### `EnemyArchetype` Modification

One new field added:

```csharp
[BoxGroup("Audio")]
[SerializeField] private EnemySFXData sfxData;
public EnemySFXData SFXData => sfxData;
```

### What Is Explicitly NOT Changing

- `EnemyController` — no code changes, just event consumption
- `EnemyDeadState` — no audio code added here
- AI state logic — no SFX triggers in state classes

### Integration Pattern

```
EnemyController.OnStateChanged ──────► EnemyAudioHandler.HandleStateChanged()
                                           │
                                           ├─ Chase from Idle? → aggroSounds.Play()
                                           ├─ Attack?          → attackSounds.Play()
                                           └─ Hurt?            → hurtSounds.Play()

EnemyController.OnDeath ─────────────► EnemyAudioHandler.HandleDeath()
                                           │
                                           └─ PlayClipAtPoint(deathClip) ← guaranteed completion

HealthComponent.OnHealthChanged ─────► EnemyAudioHandler.HandleHealthChanged()
                                           │
                                           └─ If NOT in Hurt state → hurtSounds.Play() (backup)
```

### Validation Checklist

- [ ] Enemy plays aggro sound when first spotting the player
- [ ] Enemy plays attack sound when entering Attack state
- [ ] Enemy plays hurt sound when staggered
- [ ] Enemy plays death sound when killed
- [ ] Death sound completes even after the enemy GameObject is destroyed
- [ ] No sound spam — each category respects its cooldown
- [ ] An enemy with no `EnemySFXData` is silent (no errors)

### What "Done" Looks Like

Enemies produce contextual audio feedback. Sounds never stack or spam. Silent enemies are handled gracefully.

---

## Phase 3 — SFX Cooldowns & Anti-Spam

### Goal

Document and verify the cooldown mechanism built into `EnemyAudioHandler`.

### How Cooldowns Work

Each `SFXEntry` has a `cooldown` float (seconds). `EnemyAudioHandler` maintains a `Dictionary<SFXEntry, float>` mapping each entry to the last time it was played.

```csharp
private bool TryPlaySFX(EnemySFXData.SFXEntry entry)
{
    // 1. Check if clips exist
    if (entry == null || !entry.HasClips) return false;

    // 2. Check cooldown
    if (_cooldowns.TryGetValue(entry, out float lastTime))
    {
        if (Time.time - lastTime < entry.cooldown) return false;
    }

    // 3. Play
    AudioClip clip = entry.GetRandomClip();
    audioSource.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
    audioSource.PlayOneShot(clip, entry.volume);

    // 4. Record
    _cooldowns[entry] = Time.time;
    return true;
}
```

### Recommended Cooldown Values

| Category | Cooldown | Rationale |
|----------|----------|-----------|
| Death | 0s | Must always play, fires only once anyway |
| Attack | 0.5s | Matches typical attack rate |
| Aggro | 3s | Prevents re-trigger on chase re-entry |
| Hurt | 0.3s | Fast enough for responsive feedback, slow enough to prevent overlap |
| Idle | 5s+ | Ambient, should be infrequent |

### Edge Cases Handled

- **Rapid state changes:** Cooldown prevents the same category from playing twice within its window.
- **Multiple enemies:** Each `EnemyAudioHandler` instance has its own cooldown dictionary. Enemy A and Enemy B can aggro simultaneously.
- **Idle spam prevention:** Idle sounds only play during Idle/Patrol states, gated by Update + cooldown.

### Validation Checklist

- [ ] Attacking an enemy 5 times in 1 second produces at most 2-3 hurt sounds (0.3s cooldown)
- [ ] An enemy that enters and exits chase repeatedly only aggros once per 3 seconds
- [ ] Idle vocalizations space out correctly (5s+ apart)

### What "Done" Looks Like

No audio spam under any gameplay condition. Each sound category plays at a controlled rate.

---

## Phase 4 — Footstep System Core

### Goal

Create `FootstepEmitter` — a reusable component that plays footstep sounds based on distance traveled on the ground.

### What Already Exists

No footstep system exists. Audio is currently limited to weapons and hit effects.

### What Will Be Added

#### `FootstepEmitter.cs` — `Scripts/Audio/FootstepEmitter.cs`

A component that:

1. Receives movement state updates from its owner (`UpdateMovement(isGrounded, speed)`)
2. Accumulates horizontal distance traveled
3. Plays a footstep sound every `stepDistance` meters
4. Raycasts down to detect ground surface (`PhysicsMaterial`)
5. Looks up the correct clip set from `FootstepSurfaceData`
6. Applies random clip selection and pitch variation

**Key Design Decisions:**

- **Movement-driven, not frame-based.** Distance accumulation means footsteps scale naturally with speed. Running produces faster steps without any extra configuration.
- **Owner calls `UpdateMovement()`.** The emitter doesn't poll or guess — the movement system tells it the ground state and speed. This avoids coupling to any specific character controller.
- **No animation events required.** Works out of the box for any character. Animation events can be added later as an optional enhancement.
- **Distance resets when not grounded.** Prevents a step from playing immediately after landing if the character accumulated distance while airborne.

### Surface Detection

```
FootstepEmitter.PlayFootstep()
    │
    ├─ Raycast down from footOrigin
    │       │
    │       └─ Hit? → collider.sharedMaterial → FootstepSurfaceData.GetSurface(materialName)
    │                                                │
    │                                                ├─ Match found → use matched entry
    │                                                └─ No match   → use defaultSurface
    │
    └─ No hit? → use defaultSurface
```

### Validation Checklist

- [ ] Moving on ground produces footstep sounds at regular intervals
- [ ] Standing still produces no footsteps
- [ ] Jumping / falling produces no footsteps
- [ ] Landing after a jump does not produce an immediate step
- [ ] Different PhysicsMaterials produce different sounds
- [ ] Missing PhysicsMaterial falls back to default

### What "Done" Looks Like

A character with a `FootstepEmitter` produces regular, natural-sounding footsteps that vary by surface. No special animation setup required.

---

## Phase 5 — Player Footsteps

### Goal

Integrate `FootstepEmitter` with the player's `PlayerLocomotionController`.

### What Already Exists

`PlayerLocomotionController` uses `KinematicCharacterMotor` with:
- `Motor.GroundingStatus.IsStableOnGround` for grounded state
- `Motor.BaseVelocity` for current velocity
- `AfterCharacterUpdate()` called every physics tick

### What Will Be Added

One new serialized field on `PlayerLocomotionController`:

```csharp
[Header("Audio")]
[SerializeField] private FootstepEmitter footstepEmitter;
```

And at the end of `AfterCharacterUpdate()`:

```csharp
if (footstepEmitter != null)
{
    Vector3 velocity = Motor.BaseVelocity;
    float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
    footstepEmitter.UpdateMovement(Motor.GroundingStatus.IsStableOnGround, horizontalSpeed);
}
```

### What Is Explicitly NOT Changing

- `PlayerLocomotionController` movement logic
- `KinematicCharacterMotor` internals
- Input handling

### Setup

1. Add a `FootstepEmitter` component to the Player Character Prefab
2. Assign the `FootstepSurfaceData` asset to the emitter's `surfaceData` field
3. Drag the `FootstepEmitter` reference into `PlayerLocomotionController.footstepEmitter`
4. Adjust `stepDistance` (default: 2m — suitable for walking speed)

### Validation Checklist

- [ ] Player produces footstep sounds while walking
- [ ] Faster movement produces more frequent steps
- [ ] Footsteps stop when standing still
- [ ] Footsteps stop while airborne
- [ ] Surface-specific sounds play on different ground materials

### What "Done" Looks Like

The player produces convincing footsteps that react to surface type and movement speed.

---

## Phase 6 — Enemy Footsteps

### Goal

Add footsteps to ground-based enemies using the same `FootstepEmitter` component.

### What Already Exists

Enemies use `NavMeshAgent` for movement. The `EnemyController` has access to the agent via `NavAgent`.

### Integration Pattern

For enemies, the `FootstepEmitter.UpdateMovement()` call should be made from the enemy's Update loop or from a dedicated component. The simplest approach:

**Option A: Direct integration in a new helper component**

```csharp
/// <summary>
/// Bridges enemy NavMeshAgent movement to FootstepEmitter.
/// Attach to enemy prefabs alongside FootstepEmitter.
/// </summary>
public class EnemyFootstepBridge : MonoBehaviour
{
    [SerializeField] private FootstepEmitter footstepEmitter;
    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (footstepEmitter == null)
            footstepEmitter = GetComponent<FootstepEmitter>();
    }

    private void Update()
    {
        if (_agent == null || footstepEmitter == null) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        bool isGrounded = _agent.isOnNavMesh && !_agent.isOnOffMeshLink;
        float speed = _agent.velocity.magnitude;
        footstepEmitter.UpdateMovement(isGrounded, speed);
    }
}
```

This is implemented separately so that `FootstepEmitter` remains agnostic to the movement system.

### Setup

1. Add `FootstepEmitter` + `EnemyFootstepBridge` to ground-based enemy prefabs
2. Assign the same `FootstepSurfaceData` asset (shared with player)
3. Adjust `stepDistance` per enemy (larger enemies = larger step distance)
4. **Do NOT add to flying or stationary enemies** — they don't walk

### Validation Checklist

- [ ] Ground enemies produce footsteps while moving
- [ ] Stopped enemies produce no footsteps
- [ ] Flying enemies have no footstep components
- [ ] Enemy footsteps use the same surface detection as player

### What "Done" Looks Like

Ground enemies produce footsteps that match the player system. The player can hear approaching enemies.

---

## Phase 7 — Player Death Detection

### Goal

Create `PlayerDeathController` — the orchestrator that detects death and coordinates the entire death/respawn sequence.

### What Already Exists

- `HealthComponent.OnDeath` — fires once when health reaches 0
- `HealthComponent.IsAlive()` — boolean check
- Player has a single `HealthComponent` accessible via `PlayerManager.HealthComponent`

### What Will Be Added

#### `PlayerDeathController.cs` — `Scripts/Player/PlayerDeathController.cs`

Attached to the Player GameObject. Responsibilities:

1. Subscribe to `HealthComponent.OnDeath` in `Start()`
2. Guard against re-entry with `_isDead` flag
3. Execute the death sequence:
   - Disable player movement (`Motor.enabled = false`)
   - Drop inventory (Phase 8)
   - Clear resources (Phase 8)
   - Trigger `MusicState.GameOver` via `AudioManager`
   - Fire `OnPlayerDied` event (UI subscribes)
   - Start respawn countdown coroutine
4. Execute respawn after countdown (Phase 10)

**Death is detected via event, not polling.** The controller subscribes to `OnDeath` once in `Start()` and unsubscribes in `OnDestroy()`. No Update-loop health checking.

**Death fires once.** The `_isDead` guard prevents any re-entry if `OnDeath` somehow fires multiple times.

### Events Exposed

```csharp
event Action<float> OnPlayerDied;       // param: respawn delay
event Action<float> OnRespawnTimerTick; // param: remaining seconds
event Action OnPlayerRespawned;         // no params
```

These events decouple gameplay logic from UI entirely.

### What Is Explicitly NOT Changing

- `HealthComponent` — no modifications
- `PlayerManager` — no modifications
- No new health system

### Validation Checklist

- [ ] Reducing player health to 0 fires `OnDeath` exactly once
- [ ] `PlayerDeathController.HandleDeath()` executes exactly once
- [ ] Taking additional damage while dead does nothing
- [ ] `OnPlayerDied` event fires with the correct respawn delay value

### What "Done" Looks Like

Death is detected reliably, once, via existing health events. The death controller takes over and no other system needs to check for death independently.

---

## Phase 8 — Inventory Drop Logic

### Goal

When the player dies, drop all inventory items as world pickups and clear resource inventory.

### What Already Exists

- `PlayerInventory.GetSlot(i)` returns slot data (ItemData + Quantity)
- `PlayerInventory.TryRemoveItem(item, quantity)` removes items
- `ItemData.WorldPrefab` is the prefab spawned when items exist in the world
- `PlayerEquipmentController.UnequipItem(destroy: true)` removes held visuals
- `ResourceInventory.Clear()` zeroes all resources
- `PlayerEquipmentController.DropItem()` shows the existing drop pattern (spawn WorldPrefab, apply Rigidbody force)

### What Happens on Death

**Item Inventory:**
1. Iterate all slots, collect non-empty entries
2. Remove items from inventory (prevents duplication)
3. For each item, instantiate `WorldPrefab` near death position
4. Apply scatter physics (golden angle distribution + upward force)
5. Stackable items: one pickup per stack
6. Non-stackable items: one pickup per unit

**Resource Inventory:**
1. Call `resourceInventory.GetInventory().Clear()`
2. This fires `OnResourceChanged` for each type (UI can react)

**Equipped Item:**
1. Call `equipment.UnequipItem(destroy: true)` to remove the held visual
2. The equipped item was already in inventory — its WorldPrefab was spawned above

### Item Scatter Algorithm

Items distribute in a circle around the death position using the golden angle (137.5°) for even spacing:

```csharp
float angle = index * 137.5f * Mathf.Deg2Rad;
float distance = Mathf.Sqrt(index + 1) * (dropRadius / 3f);
```

This prevents items from piling on top of each other.

### Edge Cases

- **Item with no WorldPrefab:** Logged as a warning, item is lost (data was already removed from inventory). This is acceptable — items should always have WorldPrefabs.
- **Empty inventory:** No drops, no errors.
- **Held item not in inventory:** The held visual is destroyed regardless. No ghost items.

### Validation Checklist

- [ ] Dying with 5 items drops 5 world pickups near the death position
- [ ] Dying with an equipped item removes it from hands
- [ ] Resources are zeroed out (check via debug log)
- [ ] Items scatter and don't stack on one point
- [ ] Dropped items can be picked up again (existing pickup system)
- [ ] Dying with empty inventory produces no drops (no errors)

### What "Done" Looks Like

All carried items appear as world pickups near where the player died. Resources are gone. The inventory is empty. Nothing is duplicated or silently lost.

---

## Phase 9 — Death Screen Timer Logic

### Goal

Show a death screen with a countdown timer during the respawn delay. Fully event-driven, decoupled from gameplay.

### What Already Exists

- `UIPanel` base class with `Show()`/`Hide()` via `CanvasGroup`
- `UIManager` auto-discovers panels in children
- `PlayerDeathController` fires `OnPlayerDied`, `OnRespawnTimerTick`, `OnPlayerRespawned`

### What Will Be Added

#### `DeathScreenPanel.cs` — `Scripts/UI/Panels/DeathScreenPanel.cs`

Extends `UIPanel`. Responsibilities:

1. Finds `PlayerDeathController` at `Start()` (via `PlayerManager` or `FindAnyObjectByType`)
2. Subscribes to death controller events
3. On `OnPlayerDied`: Shows panel, sets header text
4. On `OnRespawnTimerTick`: Updates timer text each frame
5. On `OnPlayerRespawned`: Hides panel

**No gameplay logic in the UI.** The panel doesn't know about health, inventory, or timers. It just reacts to events and updates text.

### Timer Display

```csharp
timerText.text = string.Format(timerFormat, remainingSeconds);
// Default format: "Respawning in {0:F1}s"
// Example output: "Respawning in 3.2s"
```

### UI Setup (Editor — Called Out Separately)

See the **Unity Editor Setup Instructions** document for:
- Creating the Death Screen Canvas child
- Adding TMP_Text components
- Wiring references

### What Is Explicitly NOT Changing

- `UIManager` — no modifications
- `UIPanel` — no modifications

### Validation Checklist

- [ ] Death screen appears when the player dies
- [ ] Timer counts down in real-time
- [ ] Timer reaches "0.0s" and the screen hides
- [ ] Death screen is hidden on start (not visible until death)
- [ ] Multiple deaths show the screen each time

### What "Done" Looks Like

A simple death screen appears on death, counts down, and disappears on respawn. Zero coupling to gameplay systems.

---

## Phase 10 — Respawn Flow

### Goal

After the death timer expires, teleport the player, restore health, and return control.

### What Happens

`PlayerDeathController.PerformRespawn()` executes after the countdown coroutine completes:

1. **Teleport** — `locomotion.Motor.SetPosition(respawnPosition)` (default: `Vector3.zero`)
2. **Heal** — `health.SetMaxHealth(health.MaxHealth, healToMax: true)` (restores to full)
3. **Re-enable control** — `locomotion.SetCharacterControllerMotorEnabled(true)`
4. **Restore music** — `AudioManager.ForceSetMusicState(MusicState.Exploration)`
5. **Reset death flag** — `_isDead = false`
6. **Fire event** — `OnPlayerRespawned?.Invoke()` (UI hides death screen)

### What Is NOT Implemented (Future Hooks)

- Checkpoint/spawn selection — `respawnPosition` is a serialized `Vector3`, easily replaced
- Save/load — no persistence
- Death animation — can be triggered via Animator before the countdown starts
- Respawn invulnerability — can be added via `health.SetInvulnerable(true)` with a timer

### Validation Checklist

- [ ] Player appears at (0, 0, 0) after respawn
- [ ] Health is full
- [ ] Player can move and interact
- [ ] Music returns to Exploration
- [ ] Death screen is hidden
- [ ] Dying again after respawn works correctly (full cycle)

### What "Done" Looks Like

The player seamlessly returns to gameplay after the countdown. Everything is clean — health full, inventory empty, music normal, controls responsive.

---

## Phase 11 — Integration & Edge Cases

### Goal

Verify the entire flow works end-to-end and handle edge cases.

### Full Death Sequence

```
1. Player takes lethal damage
   └─ HealthComponent.OnDeath fires

2. PlayerDeathController.HandleDeath()
   ├─ _isDead = true (guard)
   ├─ Motor.enabled = false (freeze player)
   ├─ DropAllItems() → WorldPrefabs spawned
   ├─ ClearResources() → ResourceInventory zeroed
   ├─ UnequipHeldItem() → held visual destroyed
   ├─ AudioManager.SetMusicState(GameOver)
   ├─ OnPlayerDied.Invoke(respawnDelay)
   │   └─ DeathScreenPanel.Show()
   └─ StartCoroutine(RespawnCountdown)
       └─ Each frame: OnRespawnTimerTick(remaining)
           └─ DeathScreenPanel.UpdateTimerDisplay()

3. Timer expires → PerformRespawn()
   ├─ Motor.SetPosition(respawnPosition)
   ├─ health.SetMaxHealth(max, healToMax: true)
   ├─ Motor.enabled = true
   ├─ AudioManager.ForceSetMusicState(Exploration)
   ├─ _isDead = false
   └─ OnPlayerRespawned.Invoke()
       └─ DeathScreenPanel.Hide()
```

### Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| Player dies with empty inventory | No drops, no errors |
| Player dies while already dead | `_isDead` guard prevents re-entry |
| Player dies during music crossfade | AudioManager handles interruption cleanly |
| Enemy kills player, then enemy dies | Both death handlers fire independently |
| Player dies mid-attack | Equipment is unequipped, attack interrupted by motor disable |
| Item has no WorldPrefab | Warning logged, item is lost (not silently) |
| No AudioManager in scene | Music calls are null-checked, death still works |
| No DeathScreenPanel in UI | Death/respawn works, just no visual feedback |

### Validation Checklist

- [ ] Full death-to-respawn cycle works 3 times in a row without issues
- [ ] Dying during combat correctly stops the player
- [ ] Dying with a full inventory drops all items
- [ ] Dying with resources clears them all
- [ ] No console errors during the entire flow

### What "Done" Looks Like

The death/respawn system is bulletproof. Every edge case is handled or explicitly documented as acceptable.

---

## Phase 12 — Debugging & Failure Modes

### Goal

Provide tools and knowledge for diagnosing issues quickly.

### Debug Logging

All systems use prefixed logs:

```
[Audio]         → AudioManager music transitions
[EnemyAudio]    → Enemy SFX playback and cooldowns
[PlayerDeath]   → Death sequence steps
[DeathScreenPanel] → UI show/hide
```

Filter your console by prefix to isolate issues.

### Debug Buttons (Inspector)

`PlayerDeathController` provides:
- **Force Death** — kills the player instantly (for testing)
- **Force Respawn** — skips the timer and respawns immediately

`EnemyAudioHandler` shows:
- Current SFX data name
- Dead state

### Common Failure Modes

#### 1. Enemy makes no sound

**Check:** Does the `EnemyArchetype` have an `SFXData` asset assigned?
**Check:** Does the enemy prefab have an `EnemyAudioHandler` component?
**Check:** Are the clip arrays populated in the `EnemySFXData` asset?

#### 2. Footsteps don't play

**Check:** Is `FootstepEmitter` attached and its `surfaceData` assigned?
**Check:** Is `PlayerLocomotionController.footstepEmitter` reference set?
**Check:** Is the `stepDistance` reasonable (2m for walking)?
**Check:** Do the ground colliders have PhysicsMaterials? (If not, default surface plays)

#### 3. Death screen doesn't appear

**Check:** Is `DeathScreenPanel` a child of the UIManager's Canvas?
**Check:** Does `UIManager.DiscoverPanels()` log finding it?
**Check:** Is `PlayerDeathController` attached to the player?
**Check:** Do `timerText` and `headerText` references point to valid TMP_Text components?

#### 4. Items don't drop on death

**Check:** Do the items have `WorldPrefab` assigned in their `ItemData`?
**Check:** Is `PlayerInventory` accessible via `PlayerManager.Inventory`?
**Check:** Console warnings for "no WorldPrefab assigned"

#### 5. Music doesn't change on death

**Check:** Is `AudioManager.Instance` not null?
**Check:** Does `MusicStateConfig` have a `GameOver` entry?
**Check:** Is `GameOver` priority higher than the current state?

#### 6. Respawn fails

**Check:** Console for `[PlayerDeath] Respawning player.`
**Check:** Is `KinematicCharacterMotor` not null on the locomotion controller?
**Check:** Is the respawn position (0,0,0) above a valid NavMesh/ground surface?

### Validation Checklist

- [ ] All debug buttons work in Play Mode
- [ ] Console logs clearly trace the death sequence
- [ ] Each failure mode listed above can be diagnosed from logs alone

### What "Done" Looks Like

Any audio or death issue can be diagnosed in under a minute using console logs and Inspector state.

---

## Phase 13 — Extension Hooks

### Goal

Document how these systems can grow without refactoring.

### Extension 1: Death Animation

Add a death animation trigger before the countdown starts:

```csharp
// In PlayerDeathController.HandleDeath(), before starting countdown:
if (playerManager.Animator != null)
{
    playerManager.Animator.SetTrigger("Die");
}
```

### Extension 2: Respawn Invulnerability

Temporary invulnerability after respawn:

```csharp
// In PerformRespawn():
_health.SetInvulnerable(true);
StartCoroutine(RemoveInvulnerability(3f));

private IEnumerator RemoveInvulnerability(float duration)
{
    yield return new WaitForSeconds(duration);
    _health.SetInvulnerable(false);
}
```

### Extension 3: Dynamic Respawn Points

Replace the static `Vector3 respawnPosition` with a spawn point system:

```csharp
// Concept:
public interface IRespawnPointProvider
{
    Vector3 GetRespawnPosition();
}

// PlayerDeathController resolves the provider at respawn time.
```

### Extension 4: Loot Drop on Enemy Death

The same item-drop pattern from `PlayerDeathController.DropAllItems()` can be extracted into a shared utility:

```csharp
public static class ItemDropHelper
{
    public static void DropItems(Vector3 position, List<(ItemData, int)> items, float radius) { ... }
}
```

### Extension 5: Enemy Footstep Variation by Size

Use `FootstepEmitter.SetStepDistance()` to scale step frequency:

```csharp
// In EnemyFootstepBridge:
footstepEmitter.SetStepDistance(archetype.IsBoss ? 4f : 2f);
```

### Extension 6: Terrain-Based Surface Detection

For Unity terrain, sample the dominant texture at the character's position:

```csharp
// Concept — not implemented
Terrain terrain = Terrain.activeTerrain;
int textureIndex = GetDominantTexture(terrain, position);
string textureName = terrain.terrainData.terrainLayers[textureIndex].name;
surface = surfaceData.GetSurface(textureName);
```

### Extension 7: SFX Volume Settings

Add a master SFX volume multiplier, applied in `EnemyAudioHandler.TryPlaySFX()` and `FootstepEmitter.PlayFootstep()`:

```csharp
// In a future SettingsManager or AudioManager:
public float MasterSFXVolume { get; set; } = 1f;

// In playback:
audioSource.PlayOneShot(clip, entry.volume * SettingsManager.Instance.MasterSFXVolume);
```

### What "Done" Looks Like

Nothing is implemented. But when any of these features are needed, the path forward is clear and the existing code doesn't change.

---

## Complete File Listing

### New Files

```
Assets/Scripts/Audio/
├── AudioManager.cs          (Music singleton — from AudioSystemGuide)
├── MusicState.cs            (Music state enum — from AudioSystemGuide)
├── MusicTrackData.cs        (Music track config — from AudioSystemGuide)
├── MusicStateConfig.cs      (Music state mapping — from AudioSystemGuide)
├── EnemySFXData.cs          (Enemy SFX ScriptableObject)
├── EnemyAudioHandler.cs     (Enemy SFX component)
├── FootstepSurfaceData.cs   (Surface→clip mapping ScriptableObject)
├── FootstepEmitter.cs       (Distance-based footstep component)
└── EnemyFootstepBridge.cs   (NavMeshAgent→FootstepEmitter adapter)

Assets/Scripts/Player/
└── PlayerDeathController.cs (Death/respawn orchestrator)

Assets/Scripts/UI/Panels/
└── DeathScreenPanel.cs      (Death countdown UI)
```

### Modified Files

```
Assets/Scripts/AI/Enemy/Data/EnemyArchetype.cs
    + [BoxGroup("Audio")] EnemySFXData sfxData
    + public EnemySFXData SFXData => sfxData

Assets/Scripts/Player/PlayerLocomotionController.cs
    + [Header("Audio")] FootstepEmitter footstepEmitter
    + FootstepEmitter.UpdateMovement() call in AfterCharacterUpdate()
```

### New Assets (Editor Setup Required)

```
Assets/Data/Audio/
├── MusicStateConfig.asset       (Music state → track mapping)
├── FootstepSurfaceData.asset    (Surface → footstep clips)
└── Enemies/
    └── (Per-archetype EnemySFXData assets)
```

---

## Quick Reference: Public APIs

### AudioManager (Music)

```csharp
bool  AudioManager.Instance.SetMusicState(MusicState state)
void  AudioManager.Instance.ForceSetMusicState(MusicState state)
void  AudioManager.Instance.ReturnToPreviousState()
void  AudioManager.Instance.StopMusic()
```

### PlayerDeathController (Death/Respawn)

```csharp
bool  PlayerDeathController.IsDead
event Action<float> OnPlayerDied          // respawn delay
event Action<float> OnRespawnTimerTick    // remaining seconds
event Action        OnPlayerRespawned
```

### FootstepEmitter (Footsteps)

```csharp
void FootstepEmitter.UpdateMovement(bool isGrounded, float horizontalSpeed)
void FootstepEmitter.SetStepDistance(float distance)
```

### EnemyAudioHandler (Enemy SFX)

No public API — fully event-driven. Attach the component and assign SFX data.
