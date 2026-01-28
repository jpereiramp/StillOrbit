# Unity Editor Setup Instructions: Audio & Death Systems

This document lists every manual Unity Editor action required to make the Audio, SFX, and Player Death systems functional. Each step is grouped by system and ordered by dependency.

---

## 1. AudioManager GameObject

**Scene:** Your startup/persistent scene (e.g., `GameScene`)

1. Create an empty GameObject: `GameObject > Create Empty`
2. Name it `AudioManager`
3. Add the `AudioManager` component (Add Component > AudioManager)
4. The two `AudioSource` components are auto-created at runtime — no manual setup needed
5. Leave the `Music Source A` and `Music Source B` fields empty (auto-created)
6. Assign the `MusicStateConfig` asset (created in step 2 below) to the `Music Config` field

> **Note:** `DontDestroyOnLoad` is handled in code. The AudioManager persists across scene loads automatically.

---

## 2. MusicStateConfig ScriptableObject

1. In the Project window, create the folder: `Assets/Data/Audio/`
2. Right-click in that folder: `Create > StillOrbit > Audio > Music State Config`
3. Name it `MusicStateConfig`
4. In the Inspector, add entries for each music state you need:

| State | Priority | Loop | Notes |
|-------|----------|------|-------|
| None | 0 | - | No clip — leave Track empty |
| Calm | 10 | Yes | Base/safe zone music |
| Exploration | 20 | Yes | Default open-world music |
| Stealth | 30 | Yes | Near-enemy tension |
| Combat | 40 | Yes | Active combat |
| Boss | 50 | Yes | Boss encounter (optional intro clip) |
| GameOver | 60 | No | Death sting — set `loop = false` |

5. For each entry:
   - Set the `State` enum
   - Set the `Priority` value
   - Under `Track`: assign an `AudioClip` to `Clip`, set `Volume` (0.5–0.8 recommended), check/uncheck `Loop`
   - Optionally assign an `Intro Clip` for states that have a musical intro before looping
   - Optionally set `Fade Override` (0 = use the global `Default Fade Duration`)
6. Set `Default Fade Duration` (1.5s is a good starting value)
7. Drag this asset into the `AudioManager` component's `Music Config` field (from step 1)

---

## 3. FootstepSurfaceData ScriptableObject

1. In `Assets/Data/Audio/`, right-click: `Create > StillOrbit > Audio > Footstep Surface Data`
2. Name it `FootstepSurfaceData`
3. Configure the **Default Surface** entry:
   - Assign 2–4 generic footstep `AudioClip` assets to `Clips`
   - Set `Volume` (0.5–0.7)
   - Set `Pitch Variation` (0.05–0.1)
4. Add surface-specific entries under **Surfaces** for each ground type:

| Material Name | Example Clips | Volume | Notes |
|---------------|---------------|--------|-------|
| `Dirt` | dirt_step_01–04 | 0.6 | Match the `PhysicsMaterial` name on terrain colliders |
| `Metal` | metal_step_01–03 | 0.7 | |
| `Grass` | grass_step_01–04 | 0.5 | |
| `Stone` | stone_step_01–03 | 0.65 | |
| `Wood` | wood_step_01–03 | 0.6 | |

> **Important:** The `Material Name` field must exactly match the `name` property of the `PhysicsMaterial` assigned to your ground colliders (case-insensitive). If a ground collider has no `PhysicsMaterial`, the default surface is used.

---

## 4. Player Character Prefab Modifications

**Prefab:** `Assets/Data/Prefabs/Player/Player Character Prefab`

### 4a. FootstepEmitter Component

1. Select the Player Character Prefab root
2. Add Component: `FootstepEmitter`
3. Set the fields:
   - `Surface Data` → drag the `FootstepSurfaceData` asset
   - `Step Distance` → `2` (meters between steps — adjust to taste)
   - `Foot Origin` → leave empty (defaults to the root transform) OR assign a child transform at foot level
   - `Ground Layers` → set to layers that represent walkable ground
   - `Raycast Distance` → `1.5`

### 4b. Wire FootstepEmitter to PlayerLocomotionController

1. On the `PlayerLocomotionController` component, find the new `Audio` header
2. Drag the `FootstepEmitter` component (from step 4a) into the `Footstep Emitter` field

### 4c. PlayerDeathController Component

1. On the Player Character Prefab root, Add Component: `PlayerDeathController`
2. Set the fields:
   - `Respawn Delay` → `5` (seconds — adjust to taste)
   - `Respawn Position` → `(0, 0, 0)` (or wherever your spawn point is)
   - `Item Drop Radius` → `2`
   - `Item Drop Up Force` → `2`
   - `Player Manager` → leave empty (auto-resolved from the same GameObject)

---

## 5. Enemy Prefab Modifications

For **each ground-based enemy prefab** (e.g., WitchHag, Grunt, etc.):

### 5a. EnemySFXData ScriptableObject (Per Archetype)

1. Create folder: `Assets/Data/Audio/Enemies/`
2. Right-click: `Create > StillOrbit > Audio > Enemy SFX`
3. Name it to match the enemy (e.g., `WitchHag SFX`)
4. Fill in the sound categories:

| Category | Clips | Volume | Cooldown | Notes |
|----------|-------|--------|----------|-------|
| Death Sounds | 1–3 clips | 1.0 | 0 | Must always play |
| Attack Sounds | 1–3 clips | 0.9 | 0.5 | Match attack rate |
| Aggro Sounds | 1–2 clips | 0.8 | 3.0 | First detection growl/screech |
| Hurt Sounds | 1–3 clips | 0.9 | 0.3 | On-damage reaction |
| Idle Sounds | 1–3 clips | 0.5 | 5.0+ | Ambient vocalizations |

### 5b. Assign SFXData to EnemyArchetype

1. Open the enemy's `EnemyArchetype` ScriptableObject (e.g., `Assets/Data/Enemies/WitchHag.asset`)
2. Under the new **Audio** group, drag the `EnemySFXData` asset into the `SFX Data` field

### 5c. EnemyAudioHandler Component (On Prefab)

1. Open the enemy prefab
2. Add Component on the root: `EnemyAudioHandler`
3. Leave `SFX Data Override` empty (it reads from the archetype by default)
4. Leave `Audio Source` empty (auto-created)

### 5d. Footstep Components (Ground Enemies Only)

**Skip this for flying or stationary enemies.**

1. Add Component: `FootstepEmitter`
   - `Surface Data` → same `FootstepSurfaceData` asset as the player
   - `Step Distance` → `2` (adjust for enemy size — larger enemies use 3–4)
   - `Ground Layers` → same as player
2. Add Component: `EnemyFootstepBridge`
   - `Footstep Emitter` → drag the `FootstepEmitter` component you just added (or leave empty for auto-resolve)

---

## 6. Death Screen UI

**Canvas:** The Canvas that `UIManager` lives on (it auto-discovers child panels)

### 6a. Create Death Screen Panel

1. Under the UIManager's Canvas, create: `UI > Panel`
2. Name the panel GameObject `DeathScreenPanel`
3. Set the `Image` color to semi-transparent black (e.g., `RGBA: 0, 0, 0, 180`)
4. Stretch to fill the entire screen (Rect Transform: stretch all)
5. Add Component: `Canvas Group` (required by `UIPanel` base class — auto-added if missing)
6. Add Component: `DeathScreenPanel` (the script)

### 6b. Header Text

1. Under `DeathScreenPanel`, create: `UI > Text - TextMeshPro`
2. Name it `HeaderText`
3. Set text to `YOU DIED` (this is overridden at runtime, but useful for layout)
4. Style: center-aligned, large font size (60–80), bold, white or red
5. Position: upper-center of the panel

### 6c. Timer Text

1. Under `DeathScreenPanel`, create: `UI > Text - TextMeshPro`
2. Name it `TimerText`
3. Set text to `Respawning in 5.0s` (for layout reference)
4. Style: center-aligned, medium font size (30–40), white
5. Position: center of the panel, below the header

### 6d. Wire References

1. Select the `DeathScreenPanel` GameObject
2. In the `DeathScreenPanel` component:
   - `Header Text` → drag `HeaderText` TMP_Text
   - `Timer Text` → drag `TimerText` TMP_Text
   - `Timer Format` → leave as default: `Respawning in {0:F1}s`
   - `Death Header Text` → leave as default: `YOU DIED`

### 6e. Initial State

1. Set the `Canvas Group` on `DeathScreenPanel`:
   - `Alpha` → `0`
   - `Interactable` → unchecked
   - `Blocks Raycasts` → unchecked
2. This ensures the panel is invisible until the player dies

---

## 7. PhysicsMaterials for Surface Detection (Optional but Recommended)

If you want surface-specific footstep sounds:

1. Create PhysicsMaterial assets: `Create > Physic Material`
2. Name them to match the surface entries in `FootstepSurfaceData` (e.g., `Dirt`, `Metal`, `Stone`, `Grass`)
3. Assign these PhysicsMaterials to the `Material` slot of ground colliders:
   - Select the terrain/floor/platform GameObject
   - On its `Collider` component, set `Material` to the appropriate PhysicsMaterial
4. No physics properties need to change — the materials are only used for name-based surface identification

---

## 8. Verification Checklist

After completing all editor setup, verify in Play Mode:

### Audio System
- [ ] Entering Play Mode: `AudioManager` persists (check Hierarchy → DontDestroyOnLoad)
- [ ] Calling `AudioManager.Instance.SetMusicState(MusicState.Exploration)` plays music
- [ ] Music crossfades smoothly between states

### Footsteps
- [ ] Player walking produces footstep sounds
- [ ] Walking on a surface with a PhysicsMaterial produces the matching sound
- [ ] Standing still = silence
- [ ] Jumping = silence until landing

### Enemy SFX
- [ ] Enemy aggros → plays aggro sound
- [ ] Enemy attacks → plays attack sound
- [ ] Enemy takes damage → plays hurt sound
- [ ] Enemy dies → plays death sound (even after destruction)
- [ ] Enemy standing idle → occasional idle vocalization

### Player Death
- [ ] Killing the player shows the death screen
- [ ] Timer counts down from configured delay
- [ ] Items appear near death position as world pickups
- [ ] Resources are cleared (verify via console log)
- [ ] Player respawns at (0,0,0) with full health
- [ ] Death screen hides after respawn
- [ ] Player can move after respawn
- [ ] Music changes to GameOver on death, back to Exploration on respawn
- [ ] Dying again after respawn works correctly

---

## Summary of Assets to Create

| Asset | Type | Location | Created Via |
|-------|------|----------|-------------|
| `MusicStateConfig` | ScriptableObject | `Assets/Data/Audio/` | Create > StillOrbit > Audio > Music State Config |
| `FootstepSurfaceData` | ScriptableObject | `Assets/Data/Audio/` | Create > StillOrbit > Audio > Footstep Surface Data |
| `[EnemyName] SFX` | ScriptableObject (per type) | `Assets/Data/Audio/Enemies/` | Create > StillOrbit > Audio > Enemy SFX |
| PhysicsMaterials | PhysicMaterial (optional) | `Assets/Data/Audio/` or `Assets/Art/Materials/` | Create > Physic Material |

## Summary of Components to Add

| Component | Target | Notes |
|-----------|--------|-------|
| `AudioManager` | New `AudioManager` GameObject in scene | One per game, persists |
| `PlayerDeathController` | Player Character Prefab root | Alongside PlayerManager |
| `FootstepEmitter` | Player Character Prefab root | Wire to PlayerLocomotionController |
| `EnemyAudioHandler` | Each enemy prefab root | Reads from archetype |
| `FootstepEmitter` | Ground enemy prefabs | Not for flying/stationary |
| `EnemyFootstepBridge` | Ground enemy prefabs | Alongside FootstepEmitter |
| `DeathScreenPanel` | UIManager Canvas child | With TMP_Text children |
