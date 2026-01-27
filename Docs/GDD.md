# Still Orbit – Game Design Document (GDD)

## Version
v0.1 – Living Document

## High Concept
**Still Orbit** is a sci-fi first-person survival + light RTS hybrid where the player explores a hostile alien world, gathers resources, builds infrastructure, and relies on autonomous and semi-autonomous drone companions to scale operations. The core fantasy is **being a lone operator who gradually builds a self-sustaining orbital foothold**, where automation handles the mundane and the player focuses on exploration, expansion, and combat.

The game is offline-first, designed with future co-op in mind but not dependent on it.

---

## Design Pillars
1. **Player First, Automation Second**  
   Automation should reduce tedium, not remove player agency. The player *chooses* what to automate.

2. **Readable Systems**  
   Every system (resources, companions, combat) must be understandable at a glance. If a drone does something, the player should know *why*.

3. **Gradual Scale-Up**  
   Start intimate and hands-on → end with sprawling, semi-autonomous operations.

4. **Companions as Tools, Not Pets (Mostly)**  
   Companions are functional, upgradable, and specialized. Emotional attachment comes from usefulness and reliability, not dialog trees.

5. **Diegetic UX**  
   Commands, upgrades, and information are communicated through in-world devices, holograms, and animations.

---

## Genre & Perspective
- **Primary:** First-Person Survival
- **Secondary:** RTS / Automation
- **Perspective:** First-Person (with optional tactical overlays later)
- **Tone:** Quiet sci-fi, isolation, industrial, slightly ominous but hopeful

---

## Target Player Fantasy
> “I land alone. I survive. I automate. I expand. I dominate the planet—not by brute force, but by systems.”

---

## Core Gameplay Loop
1. Explore environment
2. Gather resources (manually or via companions)
3. Return resources to depots
4. Construct buildings and infrastructure
5. Unlock and upgrade companions
6. Defend against environmental threats / enemies
7. Expand operations further from player’s physical presence

---

## World & Setting
- Alien planet with:
  - Biomes (forest, desert, frozen, irradiated)
  - Hostile fauna / rogue machines
  - Valuable underground resources
- Static map initially (procedural later possible)
- Player is part of an orbital expedition; orbit presence is narrative-only for now

---

## Player Systems

### Player Abilities
- Walk, sprint, jump
- Interact (pickup, deposit, activate)
- Use tools (mining, chopping)
- Use weapons (ranged / possibly melee later)
- Construct buildings
- Command companions (lightweight, contextual)

---

## Resource System

### Resources (Initial)
- Wood
- Gold

### Planned Expansion
- Iron
- Copper
- Energy Cells
- Rare Crystals
- Biomass
- Alien Artifacts

### Resource Types
- **Raw** – gathered directly
- **Processed** – refined via buildings
- **Energy** – powers buildings & advanced companions

---

## Inventory System
- **Player Inventory**
  - Items (tools, weapons, upgrades)
  - Resources (stack-based)
- **Entity Inventories**
  - Depots
  - Companions
  - Future: vehicles, factories

All inventories use the same underlying system (already implemented 👍).

---

## Building System

### Current
- Resource Depot

### Planned Buildings
- Refinery (process ores)
- Companion Bay (build/upgrade drones)
- Power Generator
- Turrets (defensive automation)
- Radar / Scanner
- Storage Expansion Modules

### Placement Rules
- Flat ground
- No obstruction
- Within build range (expandable later)

---

## Companion System (CORE FEATURE)

### Companion Philosophy
Companions are split into **two distinct categories** to maximize clarity and player control:

---

## Companion Categories

### 1. **Automation Units (AU)**
> “Set it and forget it.”

These are **task-bound, semi-stationary or zone-based drones**.

#### Characteristics
- No following the player
- Assigned to:
  - A building
  - A resource node
  - A defined area
- Fully autonomous
- Limited AI
- Cheap to produce
- Replaceable

#### Examples
- Mining Drone
- Logging Drone
- Hauler Drone
- Defense Turret (counts as AU)
- Repair Drone

#### Player Interaction
- Assign task once
- Upgrade via building UI
- Minimal micromanagement

---

### 2. **Companion Units (CU)**
> “Your squad. Your safety net.”

These are **smarter, mobile, player-centric drones**.

#### Characteristics
- Follow the player
- Limited in number
- Multi-role capable
- Upgradable and customizable
- More expensive
- Can engage in combat

#### Examples
- Depot Companion (current one)
- Combat Companion
- Scout Companion
- Hybrid Utility Companion

---

## Companion Limits (Important Design Choice)

To avoid chaos and preserve clarity:

- **Automation Units:** Unlimited (bounded by resources & power)
- **Companion Units:** Hard cap (e.g. 2–4 max)

This ensures:
- The player doesn’t become a walking swarm
- Companions feel meaningful
- Combat balance stays manageable

---

## Companion Control Model (User-Friendly)

### No RTS-Style Command Menus
Instead, use **Contextual + Behavior-Based Control**.

#### Control Methods
1. **Behavior Mode**
   - Follow
   - Hold Position
   - Defensive
   - Aggressive
   - Utility Priority

2. **Context Interaction**
   - Look at resource → “Assign Mining”
   - Look at enemy → “Focus Target”
   - Interact with depot → “Unload All”

3. **Recall / Call**
   - Global recall (already implemented)
   - Emergency teleport with cooldown (late game)

---

## Companion Upgrades

### Upgrade Axes
- Capacity
- Speed
- Efficiency
- Durability
- AI Behaviors
- Combat Modules (for CU)

### Upgrade Delivery
- Through Companion Bay
- Modular system (slots)
- Visual changes on the drone

### Example Upgrades
- Auto-sorting inventory
- Threat detection
- Resource prioritization
- Shield generator
- Weapon attachments

---

## Combat System

### Player Combat
- FPS-style shooting
- Weapons:
  - Basic rifle
  - Energy weapons later
- Resource cost for ammo / energy

### Companion Combat
- CU can:
  - Defend player
  - Draw aggro
  - Provide suppressive fire
- AU turrets defend areas

### Enemies
- Wildlife
- Rogue machines
- Environmental hazards

---

## Progression

### Tech Tree
- Unlock buildings
- Unlock companion types
- Improve automation
- Improve combat survivability

### Player Progress
- New tools
- Better weapons
- Better command abilities

---

## UX / UI

### Diegetic UI
- Wrist-mounted HUD
- Holographic build menus
- Companion status lights
  - Green = idle
  - Yellow = working
  - Red = danger

---

## Audio & Atmosphere
- Subtle ambient soundscapes
- Mechanical companion sounds
- Minimal music, reactive to danger

---

## Future Co-Op Considerations
- Shared depots
- Shared automation
- Per-player companion caps
- Ping-based communication instead of voice reliance

---

## Development Roadmap (Next Steps)

### Phase 1 – System Solidification
1. Add more resource types
2. Improve damage/resource node feedback
3. Expand inventory UI clarity
4. Add Companion Bay building

---

### Phase 2 – Companion Expansion
5. Implement Companion Categories (AU vs CU)
6. Add Mining & Logging Automation Units
7. Add Combat Companion
8. Add upgrade system for companions
9. Add companion limit logic

---

### Phase 3 – World & Threats
10. Add enemy AI
11. Add base defense mechanics
12. Add environmental hazards
13. Add power/energy system

---

### Phase 4 – Progression & Polish
14. Tech tree implementation
15. More buildings
16. More biomes
17. Save/load robustness
18. Performance optimization

---

### Phase 5 – Pre-Ship
19. UX pass
20. Audio pass
21. Tutorial onboarding
22. Difficulty tuning
23. Bug fixing

---

## Shipping Criteria
- Fully playable loop
- Clear progression
- No required co-op
- Stable save system
- At least:
  - 5 resources
  - 6 buildings
  - 5 companion types
  - 3 enemy types

---

## Closing Vision
**Still Orbit** is not about frantic action—it’s about mastery.  
You start alone, overwhelmed.  
You end as the architect of a living machine.

You don’t conquer the planet.  
You make it *work*.

---
