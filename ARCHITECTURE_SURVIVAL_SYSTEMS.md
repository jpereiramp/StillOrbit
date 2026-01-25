# Survival Game Architecture — Core Resource & Damage Systems

**Document Version:** 1.0  
**Date:** January 25, 2026  
**Purpose:** Architecture design for world resource interaction, damage, and health systems

---

## 🎯 Design Philosophy

This architecture follows the established patterns in the existing player systems:

- **Interface-driven behavior** — Components expose capabilities through interfaces
- **Clear separation of concerns** — Each system has a single, well-defined responsibility
- **Consumer pattern** — Player systems consume world systems, never own them
- **Loose coupling** — Systems communicate through interfaces, not concrete references
- **Composability** — Systems combine to create emergent behavior without hard-coding

---

## 📐 System Breakdown

### 1. Health System

**Responsibility:**
- Track durability/hit points for any entity (trees, rocks, enemies, player)
- Respond to damage application
- Emit events when health changes or depletion occurs
- Manage alive → damaged → destroyed lifecycle

**Dependencies:**
- None (fully self-contained)

**Dependents:**
- Damage System (applies damage to health)
- Destruction System (listens for depletion events)
- UI System (displays health bars)
- Audio/VFX System (reacts to damage events)

**Key Characteristics:**
- Generic and reusable across all entity types
- Does NOT know why damage occurred or who applied it
- Does NOT handle destruction logic (only signals it)
- Can exist on static objects (trees) and dynamic entities (enemies)

---

### 2. Damage System

**Responsibility:**
- Provide a standardized way to apply damage to any entity
- Abstract the source of damage (tool, weapon, explosion, environment)
- Route damage from damage dealers to damage receivers
- Support typed damage (physical, fire, tool-specific, etc.) for future extensibility

**Dependencies:**
- Health System (to apply damage)

**Dependents:**
- Tool System (tools deal damage)
- Combat System (weapons deal damage)
- Environmental Hazards (future: falling rocks, fire)

**Key Characteristics:**
- Does NOT own health values
- Acts as a communication layer between "things that hurt" and "things that can be hurt"
- Supports filtering (e.g., "only pickaxes damage rocks")
- Allows for damage modification (resistances, weaknesses) without changing source

---

### 3. Destruction System

**Responsibility:**
- Handle what happens when an entity's health is depleted
- Coordinate removal of destroyed entities
- Trigger destruction effects (animations, particles, sounds)
- Manage resource spawning for destroyed harvestables

**Dependencies:**
- Health System (listens for depletion events)

**Dependents:**
- Resource Drop System (spawns loot)
- VFX/Audio System (plays effects)
- World State System (tracks what's been destroyed)

**Key Characteristics:**
- Responds to health depletion, doesn't check it
- Does NOT apply damage
- Configurable per-entity (trees fall, rocks crumble, enemies ragdoll)
- Can be interrupted or modified (future: salvage, resurrection)

---

### 4. Harvestable Resource System

**Responsibility:**
- Mark world objects as harvestable (trees, rocks, crates)
- Define resource type (wood source, stone source, metal source)
- Configure harvest requirements (tool type, damage type)
- Define what resources drop and in what quantities

**Dependencies:**
- Health System (to be destructible)
- Destruction System (to drop resources)

**Dependents:**
- Player Interaction System (to identify harvestables)
- Tool System (to validate tool effectiveness)
- Resource Drop System (to know what to spawn)

**Key Characteristics:**
- Adds "harvestable" semantics to generic destructible objects
- Does NOT handle damage or health
- Can exist on objects with or without health (some may be instant-harvest)
- Configurable for different resource types without code changes

---

### 5. Resource Drop System

**Responsibility:**
- Spawn physical resource items when harvestables are destroyed
- Handle drop position, scatter, and physics
- Support configurable drop tables (randomization, quantity ranges)
- Integrate with existing Item system

**Dependencies:**
- Destruction System (triggered by destruction)
- Item System (creates item instances)

**Dependents:**
- Inventory System (player picks up dropped resources)

**Key Characteristics:**
- Fully decoupled from health and damage
- Triggered by events, not direct calls
- Can be used for non-harvest drops (enemy loot, quest rewards)
- Supports future extensions (drop quality, rare drops)

---

### 6. Tool System

**Responsibility:**
- Define tool properties (damage amount, damage type, effectiveness)
- Handle tool usage lifecycle (windup, strike, cooldown)
- Determine what the tool can affect (trees, rocks, etc.)
- Integrate with existing Item system as a behavior interface

**Dependencies:**
- Item System (tools are items)
- Damage System (tools deal damage)
- PlayerInteractionController (to trigger tool use)

**Dependents:**
- Player systems (equips and uses tools)
- Harvestable system (validates tool compatibility)

**Key Characteristics:**
- Tools are items with additional capabilities
- Do NOT directly modify world objects
- Deal damage through the Damage System
- Can have durability (future extension using Health System)

---

## 🔌 Conceptual Interfaces & Contracts

### IDamageable

**Purpose:**  
Marks an entity as capable of receiving damage.

**Why it exists:**  
Allows damage sources (tools, weapons, hazards) to uniformly apply damage without knowing the target's type.

**Who implements it:**
- Trees
- Rocks
- Breakable objects
- Enemies
- Player
- Any entity that can be harmed

**Contract:**
- Exposes a method to receive damage (amount, type, source)
- Does NOT guarantee damage will be applied (may resist, block, or ignore)
- May emit events when damage is received

**When NOT to use:**
- Objects that are invulnerable but interactable (use separate interfaces)
- Triggers or UI elements

---

### IHarvestable

**Purpose:**  
Marks a world object as a resource source that can be harvested.

**Why it exists:**  
Separates "things you can hit" from "things that give resources when destroyed."

**Who implements it:**
- Trees
- Rocks/Ore deposits
- Breakable crates/barrels
- Potentially farmable plants (future)

**Contract:**
- Exposes resource type and drop configuration
- Optionally exposes tool requirements (what can harvest this)
- Does NOT handle harvesting logic, only data

**When NOT to use:**
- Enemies (they drop loot, but aren't "harvestable")
- Interactive objects that don't yield resources

---

### IDestructible

**Purpose:**  
Marks an entity that has a destruction lifecycle.

**Why it exists:**  
Provides a standardized way to handle death/destruction across all entity types.

**Who implements it:**
- Anything with health that can be destroyed
- Trees, rocks, enemies, player, vehicles (future)

**Contract:**
- Signals when destruction begins (for animations, effects)
- Exposes destruction state (intact, destroying, destroyed)
- Does NOT handle resource drops (that's Destruction System's job)

**When NOT to use:**
- Objects that disappear instantly without ceremony
- Purely visual effects

---

### ITool (extends Item behavior interfaces)

**Purpose:**  
Marks an item as a tool capable of affecting the world.

**Why it exists:**  
Allows items to define their world-interaction capabilities beyond basic use/equip.

**Who implements it:**
- Axes (chop trees)
- Pickaxes (mine rocks)
- Hammers (break objects, build structures)
- Weapons with melee capabilities (future)

**Contract:**
- Exposes damage type and amount
- Exposes effectiveness categories (what it's good against)
- Exposes usage timing (swing duration, cooldown)
- Does NOT perform raycasting (uses PlayerViewRaycaster)

**When NOT to use:**
- Consumable items
- Passive items (keys, quest items)

---

### IHealth

**Purpose:**  
Exposes health state and modification methods.

**Why it exists:**  
Abstracts health tracking so UI, AI, and systems can query/modify health uniformly.

**Who implements it:**
- Health component (the main implementation)
- Potentially composite systems (shields + health, future)

**Contract:**
- Exposes current/max health
- Provides damage/heal methods
- Emits events on health change, depletion
- Does NOT implement destruction (only signals it)

**When NOT to use:**
- Objects that break instantly without progressive damage
- Purely cosmetic objects

---

## 🔁 Interaction Flows

### Flow A: Chopping a Tree

```
Player Input (mouse click)
  ↓
PlayerInteractionController (detects continuous use input)
  ↓
Equipped Item (Axe) implements ITool
  ↓
Axe.Use() is called
  ↓
PlayerViewRaycaster provides hit target
  ↓
Target checked: implements IDamageable?
  ↓
Target checked: Axe is effective against target? (via IHarvestable.ToolRequirement)
  ↓
Damage System: Apply damage (Axe.Damage, DamageType.Chopping)
  ↓
Health System: Reduce health by damage amount
  ↓
Health System: Emit HealthChanged event (for VFX/Audio)
  ↓
Health depleted? → Emit HealthDepleted event
  ↓
Destruction System: Listen for HealthDepleted
  ↓
Destruction System: Trigger destruction sequence
  ↓
  ├─→ VFX/Audio: Tree fall animation, crash sound
  ├─→ Resource Drop System: Spawn wood items at tree base
  └─→ World State: Mark tree as destroyed, remove GameObject
  ↓
Player Inventory: Can pick up dropped wood items
```

**Key observations:**
- PlayerInteractionController doesn't know it's a tree
- Axe doesn't know health values
- Health doesn't know it's a tree
- Destruction coordinates multiple responses without coupling

---

### Flow B: Mining a Rock

```
Player Input (hold mouse)
  ↓
PlayerInteractionController (continuous interaction)
  ↓
Equipped Item (Pickaxe) implements ITool
  ↓
Pickaxe.Use() called repeatedly (every swing)
  ↓
PlayerViewRaycaster provides hit target (same rock)
  ↓
Target implements IDamageable + IHarvestable
  ↓
IHarvestable: Check if Pickaxe is valid tool for rock type
  ↓
Valid? → Damage System: Apply mining damage
  ↓
Health System: Reduce rock durability
  ↓
Health System: Emit damage events (for particle effects on hit)
  ↓
Rock durability depleted?
  ↓
Destruction System: Trigger rock break
  ↓
  ├─→ VFX: Rock shatter particles
  ├─→ Resource Drop System: Spawn stone + potential ore
  └─→ Remove rock from world
```

**Key observations:**
- Same flow as tree, different tool effectiveness
- IHarvestable provides tool validation without coupling
- Drop system can randomize ore types without health system knowing

---

### Flow C: Breaking a Generic Destructible Object (Crate)

```
Player Input
  ↓
PlayerInteractionController
  ↓
Any Tool or Weapon (implements ITool)
  ↓
PlayerViewRaycaster hits crate
  ↓
Crate implements IDamageable (but NOT IHarvestable — no tool restriction)
  ↓
Damage System: Apply damage (any tool works)
  ↓
Health System: Reduce crate health
  ↓
Health depleted
  ↓
Destruction System:
  ├─→ Break animation
  ├─→ Resource Drop: Random loot table (supplies, ammo, etc.)
  └─→ Remove crate
```

**Key observations:**
- Crate has health but no harvest restrictions
- Drop system uses different logic (loot table vs. resource type)
- Same damage pipeline as harvestables

---

### Flow D: (Future) Damaging an Enemy

```
Player Input (weapon fire)
  ↓
Weapon System (future, similar to Tool System)
  ↓
Weapon.Fire() → raycast or projectile
  ↓
Hit enemy: implements IDamageable
  ↓
Damage System: Apply combat damage (DamageType.Projectile, etc.)
  ↓
Enemy Health System: Reduce health
  ↓
  ├─→ AI System: React to damage (aggro, flee, etc.)
  ├─→ Animation System: Hit reaction
  └─→ If health depleted: Enemy death
       ↓
       Destruction System (enemy variant):
         ├─→ Death animation
         ├─→ Resource Drop System: Spawn loot
         ├─→ AI System: Remove from active enemies
         └─→ Despawn or ragdoll
```

**Key observations:**
- Same IDamageable interface as trees
- Same Health System as rocks
- Same Destruction System as harvestables
- Additional AI/animation hooks, but core flow identical

---

## 🧱 Extensibility Guidelines

### Adding New Resource Types

**Required:**
1. Create a new GameObject with the resource (tree, rock, plant)
2. Add Health component, configure max health
3. Add Harvestable component, set resource type and drop table
4. Configure tool requirements (if any)

**No code changes needed**  
**Handled by:** Data-driven configuration in Unity Inspector

---

### Adding New Tools

**Required:**
1. Create new Item asset
2. Add Tool behavior interface implementation
3. Configure damage amount, damage type, effectiveness tags
4. Configure animation/timing (swing duration, cooldown)

**May require:**
- New DamageType enum value (if tool has unique damage mechanic)
- New effectiveness category (e.g., "GoodAgainstIce")

**Code changes minimal:** Extend enums, no new systems

---

### Adding Enemy Combat

**Required:**
1. Enemies implement IDamageable (already defined)
2. Enemies use Health System (already defined)
3. Enemies use Destruction System variant (configure for death, not resource harvest)
4. Create AI hooks that listen to Health events (damage received, health low, death)

**Additional systems needed:**
- AI Behavior System (separate from damage pipeline)
- Enemy Loot System (reuses Resource Drop System)
- Animation Controller Integration (hooks into Destruction events)

**No changes to:** Health System, Damage System, core interfaces

---

### Adding Special Cases

#### Example: Weak Points on Large Enemies

**Approach:**
- Weak point is a separate GameObject with its own Health component
- Implements IDamageable
- Weak point takes increased damage (via Damage System modifier)
- Weak point destruction emits event that main enemy listens to

**No changes to core systems**

---

#### Example: Tool Durability

**Approach:**
- Tools get their own Health component
- Tool usage applies "self-damage" (wear)
- When tool health depletes, tool breaks (Destruction System)
- Destruction System configured to not spawn drops, just remove item

**Reuses existing systems:** Health, Destruction

---

#### Example: Elemental Resistances (Fire, Ice, etc.)

**Approach:**
- Damage System checks damage type against target's resistance data
- Resistance data lives on a separate ResistanceProfile component
- Damage is modified before reaching Health System
- Health System remains generic

**Extension point:** Damage System supports modifiers, but isn't cluttered by them

---

#### Example: Regenerating Health

**Approach:**
- Health component has optional Regeneration configuration
- Regeneration ticks are handled internally by Health System
- No other systems need to know about regeneration

**Contained within:** Health System

---

## 🏗️ Component Ownership Model

### Who Owns What?

#### World Objects (Trees, Rocks, etc.)
- **Own:** Health, Harvestable, visual/audio components
- **Do NOT own:** Player interaction logic, tool data, inventory

#### Tools (Axe, Pickaxe, etc.)
- **Own:** Damage data, effectiveness data, usage timing
- **Do NOT own:** Health of targets, world object references

#### Player
- **Owns:** Input handling, camera, inventory, equipped item reference
- **Consumes:** World object interfaces (IDamageable, IHarvestable)
- **Does NOT own:** World object state, resource spawning

#### Systems (Damage, Destruction, Resource Drop)
- **Own:** Logic and coordination
- **Do NOT own:** Entity-specific data or references
- **Operate on:** Interfaces and events

---

## 🚫 Anti-Patterns to Avoid

### ❌ God Manager Classes

**Bad Example Concept:**
- "ResourceManager" that tracks all trees, all rocks, handles all damage, spawns all drops
- Single point of failure, impossible to extend

**Instead:**
- Each tree/rock is self-contained with its own components
- Systems coordinate through events, not central control

---

### ❌ Player Directly Modifying World State

**Bad Example Concept:**
- PlayerInteractionController reaches into tree GameObject and reduces its health
- Tool directly spawns resource items

**Instead:**
- Player systems ask world objects to do things via interfaces
- World objects handle their own state changes

---

### ❌ Type-Checking Instead of Interfaces

**Bad Example Concept:**
- "if (target is Tree) { do tree logic } else if (target is Rock) { do rock logic }"

**Instead:**
- "if (target implements IDamageable) { apply damage }"
- Behavior is defined by capabilities (interfaces), not types

---

### ❌ Hard-Coded Object-Specific Logic

**Bad Example Concept:**
- Axe has special logic for trees
- Pickaxe has special logic for rocks
- Every tool needs updates when new harvestables are added

**Instead:**
- Tools define generic effectiveness categories
- Harvestables specify what categories they respond to
- No coupling between tool and harvestable types

---

### ❌ Over-Abstraction (Premature ECS)

**Bad Example Concept:**
- Damage is a "data component" managed by a "damage system singleton"
- Health is a pure data structure with no logic
- 10+ tiny systems manage every tiny interaction

**Instead:**
- Components can have logic (MonoBehaviour pattern is fine)
- Systems coordinate components, but don't replace them
- Balance between data-driven and behavior-driven design

---

## 📊 System Dependency Graph

```
┌─────────────────────────────────────────────────────────┐
│                    PLAYER SYSTEMS                        │
│  (PlayerInteractionController, ViewRaycaster, Input)    │
└────────────────┬────────────────────────────────────────┘
                 │ consumes
                 ↓
┌─────────────────────────────────────────────────────────┐
│                   TOOL SYSTEM                            │
│            (Axes, Pickaxes, Weapons)                     │
└────────────────┬────────────────────────────────────────┘
                 │ deals damage via
                 ↓
┌─────────────────────────────────────────────────────────┐
│                  DAMAGE SYSTEM                           │
│        (Routes damage from dealers to receivers)        │
└────────────────┬────────────────────────────────────────┘
                 │ applies to
                 ↓
┌─────────────────────────────────────────────────────────┐
│                  HEALTH SYSTEM                           │
│       (Tracks hit points, emits lifecycle events)       │
└────────────────┬────────────────────────────────────────┘
                 │ depletion triggers
                 ↓
┌─────────────────────────────────────────────────────────┐
│                DESTRUCTION SYSTEM                        │
│    (Coordinates death/destruction across subsystems)    │
└─────┬──────────────────┬─────────────────┬──────────────┘
      │                  │                 │
      ↓                  ↓                 ↓
┌──────────┐    ┌─────────────────┐   ┌──────────┐
│ VFX/Audio│    │ RESOURCE DROP   │   │  World   │
│  System  │    │     SYSTEM      │   │  State   │
└──────────┘    └────────┬────────┘   └──────────┘
                         │ spawns items
                         ↓
                ┌─────────────────┐
                │  ITEM SYSTEM    │
                └────────┬────────┘
                         │ picked up by
                         ↓
                ┌─────────────────┐
                │    INVENTORY    │
                └─────────────────┘
```

**Critical observation:**  
Dependencies flow in one direction. No circular dependencies. Each layer can be tested independently.

---

## 🔮 Future Extension Paths

### Tool System Deep Dive
- Tool durability and repair
- Tool upgrades and crafting
- Specialized tool effects (fire axe, explosive pickaxe)
- Tool combos and skill trees

### Resource Drop & Loot Pipeline
- Dynamic loot tables
- Rarity/quality system
- Conditional drops (time of day, player stats, world events)
- Resource respawning

### Combat Extension
- Enemy AI integration with damage events
- Advanced damage types (status effects, armor penetration)
- Player combat abilities using same damage pipeline
- Team damage and friendly fire considerations

### Building System
- Placeable structures use Health System
- Tools can damage/repair structures
- Destruction System handles structure collapse physics

---

## ✅ Design Validation Checklist

Use this to verify that new features align with the architecture:

- [ ] Does this feature use existing interfaces, or create new composable ones?
- [ ] Can this feature be added without modifying existing systems?
- [ ] Is responsibility clearly assigned to one system?
- [ ] Would this feature make sense as a self-contained component?
- [ ] Can this feature be configured in Unity Inspector without code changes?
- [ ] Does this avoid creating "manager" or "controller" god objects?
- [ ] Is this feature testable in isolation?
- [ ] Will this make sense to me in 6 months when I revisit it?

---

## 📝 Summary

This architecture provides:

✅ **Unified damage model** for all gameplay — harvesting, combat, destruction  
✅ **Reusable health system** that scales from trees to enemies to player  
✅ **Clean separation** between player actions, tools, and world objects  
✅ **Interface-driven** extensibility without coupling  
✅ **Event-based** coordination between systems  
✅ **Data-driven** configuration for new content  
✅ **Future-proof** foundation for complex survival mechanics  

The design philosophy is: **"Each component knows its own job, and systems coordinate them through well-defined contracts."**

This avoids the two common traps:
1. God objects that do everything
2. Over-engineered frameworks that do nothing simply

Instead, you get composable, understandable systems that will serve you for years.

---

**End of Document**
