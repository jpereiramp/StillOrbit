# Enemy & Combat System - Unity Setup Guide

> **Project:** StillOrbit
> **Unity Version:** 6
> **Document Type:** Unity Editor Setup Instructions
> **Last Updated:** 2026-01-28

This guide covers all Unity Editor configuration required to use the Enemy & Combat systems. All code has been implemented - this document focuses on **prefabs, assets, layers, and animator setup**.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Project Settings](#2-project-settings)
3. [ScriptableObject Assets](#3-scriptableobject-assets)
4. [Enemy Prefab Setup](#4-enemy-prefab-setup)
5. [Animator Configuration](#5-animator-configuration)
6. [Encounter Setup](#6-encounter-setup)
7. [Special Enemy Types](#7-special-enemy-types)
8. [Testing & Validation](#8-testing--validation)

---

## 1. Prerequisites

Before setting up enemies, ensure the following are in place:

### NavMesh
- [ ] NavMesh is baked for all walkable surfaces
- [ ] NavMesh includes all areas enemies should traverse
- [ ] NavMesh Off Mesh Links configured for jumps/drops (if needed)

### Player Setup
- [ ] Player has `PlayerManager` singleton accessible via `PlayerManager.Instance`
- [ ] Player has `PlayerPerceivable` component attached (for enemy perception)
- [ ] Player has collider on "Player" layer

### Existing Systems
- [ ] `HealthComponent` is functional
- [ ] `HitEffectReceiver` is configured with VFX/SFX
- [ ] `WeaponHitbox` pattern is established for melee

---

## 2. Project Settings

### 2.1 Layers

Create the following layers in **Edit → Project Settings → Tags and Layers**:

| Layer Name | Recommended Index | Purpose |
|------------|-------------------|---------|
| `Player` | 8 | Player character |
| `Enemy` | 9 | All enemy entities |
| `EnemyProjectile` | 10 | Enemy projectiles |
| `Perceivable` | 11 | Things enemies can detect |

### 2.2 Physics Layer Matrix

Configure in **Edit → Project Settings → Physics**:

| | Player | Enemy | EnemyProjectile |
|------|--------|-------|-----------------|
| Player | - | ✓ | ✓ |
| Enemy | ✓ | ✗ | ✗ |
| EnemyProjectile | ✓ | ✗ | ✗ |

- Enemies should NOT collide with each other (handled by NavMesh avoidance)
- Enemy projectiles should only hit players

### 2.3 Create Asset Folders

Create the following folder structure in your Project:

```
Assets/
├── Data/
│   └── Enemies/
│       ├── Archetypes/       → EnemyArchetype assets
│       ├── Abilities/        → EnemyAbilityData assets
│       └── Encounters/       → EncounterData assets
├── Prefabs/
│   └── Enemies/              → Enemy prefabs
└── Animation/
    └── Enemies/              → Animator Controllers
```

---

## 3. ScriptableObject Assets

### 3.1 Create Enemy Archetype

**Right-click → Create → StillOrbit → Enemy → Archetype**

Name convention: `Archetype_[EnemyName]` (e.g., `Archetype_MeleeGrunt`)

#### Example: Melee Grunt

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Archetype ID | `melee_grunt` |
| | Display Name | `Grunt` |
| | Description | `Basic melee attacker` |
| **Prefab** | Prefab | *(assign after creating prefab)* |
| **Stats** | Max Health | `50` |
| | Damage Type | `Flesh` |
| | Damage Resistance | `1.0` |
| **Movement** | Movement Type | `Ground` |
| | Move Speed | `5` |
| | Turn Speed | `180` |
| **Combat** | Combat Style | `Melee` |
| | Preferred Combat Range | `1.5` |
| | Attack Range | `2.0` |
| | Stagger Chance | `0.3` |
| **Perception** | Sight Range | `15` |
| | Sight Angle | `120` |
| | Hearing Range | `20` |
| | Memory Duration | `5` |
| **Behavior** | Can Patrol | `true` |
| | Can Flee | `false` |
| **Boss** | Is Boss | `false` |

#### Example: Ranged Shooter

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Archetype ID | `ranged_shooter` |
| | Display Name | `Shooter` |
| **Stats** | Max Health | `30` |
| **Movement** | Movement Type | `Ground` |
| | Move Speed | `3.5` |
| **Combat** | Combat Style | `Ranged` |
| | Preferred Combat Range | `12` |
| | Attack Range | `15` |
| **Perception** | Sight Range | `25` |

#### Example: Flying Enemy

| Group | Field | Value |
|-------|-------|-------|
| **Movement** | Movement Type | `Flying` |
| | Flying Height | `3` |

#### Example: Boss Enemy

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Archetype ID | `boss_brute` |
| | Display Name | `The Brute` |
| **Stats** | Max Health | `500` |
| | Damage Resistance | `0.8` |
| **Boss** | Is Boss | `true` |
| | Boss Phases | *(see below)* |

**Boss Phases Configuration:**

| Phase | Health Threshold | Phase Name | Speed Mult | Damage Mult | On Enter Trigger |
|-------|-----------------|------------|------------|-------------|------------------|
| 1 | `0.7` (70%) | `Enraged` | `1.3` | `1.2` | `PhaseTransition` |
| 2 | `0.3` (30%) | `Desperate` | `1.5` | `1.5` | `PhaseTransition2` |

### 3.2 Create Enemy Ability

**Right-click → Create → StillOrbit → Enemy → Ability**

Name convention: `Ability_[AbilityName]` (e.g., `Ability_MeleeSwipe`)

#### Example: Melee Swipe

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Ability ID | `melee_swipe` |
| | Display Name | `Swipe` |
| **Timing** | Cooldown | `1.5` |
| | Windup Time | `0.3` |
| | Recovery Time | `0.5` |
| **Range** | Min Range | `0` |
| | Max Range | `2.5` |
| **Damage** | Base Damage | `15` |
| | Damage Type | `Generic` |
| **Animation** | Animation Trigger | `Attack` |
| | Animation State Name | `Attack` |
| **Behavior** | Can Be Interrupted | `true` |
| | Track Target During Windup | `true` |

#### Example: Ranged Shot

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Ability ID | `ranged_shot` |
| | Display Name | `Plasma Shot` |
| **Timing** | Cooldown | `2.0` |
| | Windup Time | `0.5` |
| | Recovery Time | `0.3` |
| **Range** | Min Range | `5` |
| | Max Range | `20` |
| **Damage** | Base Damage | `20` |
| | Damage Type | `Generic` |

### 3.3 Link Abilities to Archetype

1. Open the EnemyArchetype asset
2. In the **Abilities** section, add your ability assets to the list
3. Set **Primary Ability Index** to `0` (first ability)

---

## 4. Enemy Prefab Setup

### 4.1 Basic Structure

Create a new empty GameObject with this hierarchy:

```
Enemy_[Name]                    → Root with all components
├── Visual                      → 3D model, Animator
├── EyePoint                    → Empty, positioned at eye level
├── AttackOrigin                → Empty, positioned at weapon/hand
└── Hitbox (optional)           → For melee weapon hitbox
```

### 4.2 Root GameObject Components

Add these components to the root GameObject:

| Component | Configuration |
|-----------|---------------|
| **NavMeshAgent** | See NavMeshAgent Settings below |
| **HealthComponent** | Leave defaults (archetype sets max health) |
| **EnemyController** | Assign archetype, animator, visual root |
| **EnemyPerception** | Assign eye point, configure layer masks |
| **EnemyAbilityExecutor** | Assign controller, attack origin |
| **HitEffectReceiver** | Configure hit VFX/SFX |
| **Capsule Collider** | Height: 2, Radius: 0.5, Center: (0, 1, 0) |
| **Rigidbody** | Is Kinematic: ✓, Use Gravity: ✗ |

**For Flying Enemies, also add:**
| Component | Configuration |
|-----------|---------------|
| **EnemyFlyingMovement** | Assign controller, configure obstacle layer |

### 4.3 NavMeshAgent Settings

| Property | Value | Notes |
|----------|-------|-------|
| Agent Type | Humanoid | Or custom agent type |
| Base Offset | 0 | |
| Speed | 5 | Overridden by archetype |
| Angular Speed | 180 | Overridden by archetype |
| Acceleration | 8 | |
| Stopping Distance | 0.5 | |
| Auto Braking | ✓ | |
| Radius | 0.5 | Match collider |
| Height | 2 | Match collider |
| Obstacle Avoidance | High Quality | |
| Avoidance Priority | 50 | |

### 4.4 EnemyController Configuration

| Field | Assignment |
|-------|------------|
| Archetype | Your EnemyArchetype asset |
| Visual Root | The "Visual" child transform |
| Animator | The Animator on the Visual child |

### 4.5 EnemyPerception Configuration

| Field | Assignment |
|-------|------------|
| Controller | The EnemyController on this object |
| Eye Point | The "EyePoint" child transform |
| Sight Blocking Layers | `Default`, `Environment`, etc. |
| Target Layers | `Player`, `Perceivable` |
| Update Rate | `10` (times per second) |
| Max Tracked Targets | `5` |

### 4.6 EnemyAbilityExecutor Configuration

| Field | Assignment |
|-------|------------|
| Controller | The EnemyController on this object |
| Attack Origin | The "AttackOrigin" child transform |
| Target Layers | `Player` |
| Hit Effect Prefab | (Optional) VFX prefab |

### 4.7 Layer Assignment

1. Select the root GameObject
2. Set Layer to `Enemy`
3. When prompted, select "Yes, change children"

### 4.8 Save as Prefab

1. Drag the configured GameObject into `Assets/Prefabs/Enemies/`
2. Name it `Enemy_[Name]` (e.g., `Enemy_MeleeGrunt`)
3. **Important:** Go back to your EnemyArchetype and assign this prefab to the Prefab field

---

## 5. Animator Configuration

### 5.1 Create Animator Controller

1. **Right-click → Create → Animator Controller**
2. Name: `AC_Enemy_[Name]` (e.g., `AC_Enemy_MeleeGrunt`)
3. Place in `Assets/Animation/Enemies/`

### 5.2 Required Parameters

Add these parameters in the Animator window:

| Parameter | Type | Purpose |
|-----------|------|---------|
| `IsMoving` | Bool | Walking/Idle transitions |
| `Attack` | Trigger | Attack animation |
| `Hurt` | Trigger | Stagger/hit reaction |
| `Die` | Trigger | Death animation |

**For Boss Enemies, add:**
| Parameter | Type | Purpose |
|-----------|------|---------|
| `PhaseTransition` | Trigger | Phase 1 transition |
| `PhaseTransition2` | Trigger | Phase 2 transition |

### 5.3 State Machine Setup

Create these states in the Base Layer:

```
[Entry] → Idle (Default)
           ↓↑ (IsMoving)
         Walk/Run

[Any State] → Attack (Attack trigger)
[Any State] → Hurt (Hurt trigger)
[Any State] → Death (Die trigger)

Attack → Idle (Has Exit Time)
Hurt → Idle (Has Exit Time)
```

### 5.4 Transition Configuration

#### Idle ↔ Walk
| Property | Value |
|----------|-------|
| Has Exit Time | ✗ |
| Transition Duration | 0.1 |
| Conditions | IsMoving = true/false |

#### Any State → Attack
| Property | Value |
|----------|-------|
| Has Exit Time | ✗ |
| Transition Duration | 0 |
| Conditions | Attack (trigger) |
| Can Transition To Self | ✗ |

#### Any State → Hurt
| Property | Value |
|----------|-------|
| Has Exit Time | ✗ |
| Transition Duration | 0 |
| Conditions | Hurt (trigger) |

#### Any State → Death
| Property | Value |
|----------|-------|
| Has Exit Time | ✗ |
| Transition Duration | 0 |
| Conditions | Die (trigger) |

#### Attack → Idle
| Property | Value |
|----------|-------|
| Has Exit Time | ✓ |
| Exit Time | 0.9 |
| Transition Duration | 0.1 |

### 5.5 Animation Events

On the **Attack animation clip**, add an Animation Event at the damage frame:

1. Select the Attack animation clip
2. Open the Animation window
3. Navigate to the frame where damage should occur
4. Click "Add Event"
5. Configure:
   - Function: `AnimationEvent_MeleeHit`
   - (No parameters needed)

**For Ranged Enemies:**
- Function: `AnimationEvent_RangedFire`

### 5.6 Assign to Prefab

1. Open your enemy prefab
2. Select the Visual child (with Animator)
3. Assign the Animator Controller to the Controller field

---

## 6. Encounter Setup

### 6.1 Create EncounterData

**Right-click → Create → StillOrbit → Encounters → Encounter Data**

Name convention: `Encounter_[Name]` (e.g., `Encounter_ForestPatrol`)

| Group | Field | Value |
|-------|-------|-------|
| **Identity** | Encounter ID | `forest_patrol` |
| | Display Name | `Forest Patrol` |
| | Encounter Type | `RandomInvasion` |
| **Spawning** | Spawn Pool | *(see below)* |
| | Min Enemy Count | `3` |
| | Max Enemy Count | `6` |
| | Staggered Spawning | ✓ |
| | Spawn Interval | `2` |
| **Positioning** | Min Spawn Distance | `15` |
| | Max Spawn Distance | `30` |
| | Prefer Outside FOV | ✓ |
| | Require NavMesh Reachable | ✓ |
| **Duration** | Max Duration | `0` (no limit) |
| | End On All Dead | ✓ |

### 6.2 Configure Spawn Pool

In the Spawn Pool list, add entries:

| Entry | Archetype | Weight | Max Count |
|-------|-----------|--------|-----------|
| 1 | `Archetype_MeleeGrunt` | `60` | `4` |
| 2 | `Archetype_RangedShooter` | `30` | `2` |
| 3 | `Archetype_Elite` | `10` | `1` |

- **Weight**: Relative spawn chance (higher = more common)
- **Max Count**: Maximum of this type per encounter

### 6.3 Scene Setup for EncounterDirector

1. Create empty GameObject: `EncounterDirector`
2. Add `EncounterDirector` component
3. Configure:
   - Player Transform: Assign player reference (or leave null for auto-find)
   - Player Camera: Assign main camera (or leave null for auto-find)
   - Max Spawn Attempts: `30`
   - NavMesh Sample Radius: `2`

### 6.4 Trigger an Encounter

From any script:

```csharp
// Reference your EncounterData asset
[SerializeField] private EncounterData forestEncounter;

void StartEncounter()
{
    EncounterDirector.Instance.StartEncounter(forestEncounter);
}
```

---

## 7. Special Enemy Types

### 7.1 Flying Enemy Additional Setup

For enemies with `Movement Type: Flying`:

1. Add `EnemyFlyingMovement` component to prefab root
2. Configure:
   - Hover Variation: `0.5`
   - Hover Speed: `2`
   - Bank Angle: `15`
   - Obstacle Layer: Select layers to avoid

3. NavMeshAgent will be automatically disabled at runtime

### 7.2 Boss Enemy Additional Setup

1. In Archetype, set `Is Boss: true`
2. Configure Boss Phases (see Section 3.1)
3. Add phase transition animations to Animator
4. Add triggers matching `On Enter Trigger` names

### 7.3 Enemy Group Setup

For coordinated enemy groups:

1. Create empty GameObject: `EnemyGroup_[Name]`
2. Add `EnemyGroup` component
3. Configure:
   - Max Attackers: `3`
   - Attacker Spacing: `2`

4. Add enemies to group via script:
```csharp
enemyGroup.AddMember(enemyController);
```

---

## 8. Testing & Validation

### 8.1 Prefab Validation Checklist

- [ ] Root has all required components
- [ ] NavMeshAgent is configured
- [ ] Layer is set to "Enemy" (including children)
- [ ] Archetype is assigned to EnemyController
- [ ] Animator is assigned
- [ ] Eye Point is positioned at eye level
- [ ] Attack Origin is positioned at weapon/hand
- [ ] Perception layer masks are correct

### 8.2 Animator Validation Checklist

- [ ] All required parameters exist
- [ ] Idle is default state
- [ ] IsMoving transitions work
- [ ] Attack trigger plays attack
- [ ] Hurt trigger plays stagger
- [ ] Die trigger plays death
- [ ] Animation events fire at correct frames

### 8.3 Runtime Testing

Use the Odin Inspector debug buttons on EnemyController:

| Button | What It Does |
|--------|--------------|
| **Force Chase Player** | Makes enemy chase the player |
| **Force Attack** | Triggers attack state |
| **Force Kill** | Kills the enemy |
| **Log Context** | Outputs debug info |

### 8.4 Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Enemy doesn't move | NavMesh not baked | Bake NavMesh |
| Enemy doesn't see player | Wrong target layers | Check EnemyPerception layer mask |
| Enemy walks through walls | No obstacle layer | Add obstacles to NavMesh |
| Attack doesn't damage | Missing animation event | Add `AnimationEvent_MeleeHit` |
| Enemy dies instantly | Health not set | Check archetype Max Health |
| Flying enemy falls | Missing component | Add `EnemyFlyingMovement` |

---

## Quick Reference: Component Checklist

### Ground Melee Enemy
- [ ] NavMeshAgent
- [ ] HealthComponent
- [ ] EnemyController
- [ ] EnemyPerception
- [ ] EnemyAbilityExecutor
- [ ] HitEffectReceiver
- [ ] Capsule Collider
- [ ] Rigidbody (kinematic)
- [ ] Animator (on visual child)

### Ground Ranged Enemy
Same as above, plus projectile prefab configured in ability

### Flying Enemy
Same as Ground, plus:
- [ ] EnemyFlyingMovement

### Boss Enemy
Same as Ground/Flying, plus:
- [ ] Boss Phases in archetype
- [ ] Phase transition animations

---

## Asset Naming Conventions

| Asset Type | Convention | Example |
|------------|------------|---------|
| Archetype | `Archetype_[Name]` | `Archetype_MeleeGrunt` |
| Ability | `Ability_[Name]` | `Ability_MeleeSwipe` |
| Encounter | `Encounter_[Name]` | `Encounter_ForestPatrol` |
| Prefab | `Enemy_[Name]` | `Enemy_MeleeGrunt` |
| Animator | `AC_Enemy_[Name]` | `AC_Enemy_MeleeGrunt` |
| Projectile | `Proj_[Name]` | `Proj_PlasmaBolt` |
