# Enemy & Combat System Implementation Guide

> **Project:** StillOrbit
> **Unity Version:** 6
> **Document Type:** Staff-Level Iterative Implementation Playbook
> **Last Updated:** 2026-01-28

---

## Executive Summary

This document provides a phased implementation plan for two tightly integrated systems:

1. **Encounter & Enemy Spawning System** — Controls *when* and *where* enemies appear
2. **Enemy AI & Combat System** — Controls *how* enemies behave and fight

Both systems are designed to **extend existing infrastructure**, not replace it.

### Key Reuse Commitments

| Existing System | Location | Reuse Strategy |
|-----------------|----------|----------------|
| `IDamageable` | `Scripts/Combat/IDamageable.cs` | Use as-is for all enemy damage reception |
| `HealthComponent` | `Scripts/Health/HealthComponent.cs` | Attach to enemies unchanged |
| `DamageType` enum | `Scripts/Combat/DamageType.cs` | Extend if needed, never replace |
| `WeaponHitbox` | `Scripts/Combat/WeaponHitbox.cs` | Enemies use identical pattern |
| `HitEffectReceiver` | `Scripts/Combat/HitEffectReceiver.cs` | Attach to enemies for VFX/SFX |
| State machine pattern | `Scripts/Companion/CompanionCoreController.cs` | Extract and generalize |
| ScriptableObject data | `Scripts/Companion/Data/CompanionData.cs` | Follow same pattern for `EnemyArchetype` |

---

## Phase 0 — Codebase Review & Assumptions

### Goal
Document what exists, what can be reused, and what gaps must be filled.

### What Already Exists

#### Damage & Health System
```
Scripts/Combat/
├── IDamageable.cs          → Interface: TakeDamage(float, DamageType, GameObject)
├── IDamageTypeProvider.cs  → Interface for damage type specification
├── DamageType.cs           → Enum: Generic, Wood, Rock, Flesh
├── WeaponHitbox.cs         → Trigger-based melee hit detection
└── HitEffectReceiver.cs    → SFX/VFX on hit

Scripts/Health/
└── HealthComponent.cs      → Full health system with events
    - Implements IDamageable
    - Events: OnHealthChanged, OnDeath
    - Supports invulnerability
    - Odin debug buttons
```

#### Weapon System
```
Scripts/Items/Weapons/
├── IWeapon.cs              → Interface: GetDamage, GetRange, NotifyHit
├── MeleeWeapon.cs          → Cooldown, delegates to PlayerCombatManager
└── RangedWeapon.cs         → Raycast, ammo, spread, beam VFX
```

#### Combat Flow (Player → Enemy)
```
Player Attack (Melee):
  MeleeWeapon.Use()
  → PlayerCombatManager.PerformMeleeAttack()
  → Animation triggers AnimationEvent_PerformMeleeAttack_Start()
  → Uses PlayerAimController.CurrentAimHitInfo for raycast
  → RegisterHit() finds IDamageable
  → damageable.TakeDamage(damage, type, source)

Player Attack (Ranged):
  RangedWeapon.Use()
  → Fire()
  → PerformRaycast()
  → ProcessHit() finds IDamageable
  → damageable.TakeDamage(damage, type, source)
```

#### State Machine (Companion)
The companion uses an **enum-based state machine** embedded in `CompanionCoreController`:
- States defined in `CompanionState` enum
- Transitions via `RequestStateChange()` with validation
- `ForceState()` for edge cases
- `OnStateEnter()` / `OnStateExit()` callbacks
- Subsystems react to `OnStateChanged` event

**Critical Observation:** This is NOT a reusable generic state machine. It's tightly coupled to `CompanionState` enum. We will **extract** a generic pattern.

### What Must Be Created

| Component | Purpose |
|-----------|---------|
| Generic State Machine | Reusable FSM for enemies AND companions |
| `EnemyController` | Orchestrator (like `CompanionCoreController`) |
| `EnemyArchetype` | ScriptableObject configuration |
| `EncounterDirector` | Global spawning coordinator |
| `EnemyPerception` | Sight, hearing, threat tracking |
| Combat states | Idle, Patrol, Chase, Attack, Hurt, Dead |
| Enemy weapons | Using existing `WeaponHitbox` / raycast patterns |

### Assumptions

1. NavMesh is baked for all traversable areas
2. Player has a dedicated layer (e.g., "Player")
3. Enemies will have a dedicated layer (e.g., "Enemy")
4. Physics layers are configured for appropriate collisions
5. Animation controllers will be created per enemy type

### What is Explicitly NOT Being Changed

- `IDamageable` interface signature
- `HealthComponent` implementation
- `DamageType` enum values (may add `Energy` later)
- Player weapon scripts
- `PlayerCombatManager` logic
- Companion system (will share extracted state machine)

### Validation Checklist

- [x] `IDamageable` can receive damage from any source
- [x] `HealthComponent.OnDeath` fires but has no listeners (enemies will listen)
- [x] `WeaponHitbox` uses layer masks (enemies can use same pattern)
- [x] Ranged weapons use Physics.Raycast (enemies can use same)
- [x] Companion state machine is functional but not generic

### What "Done" Looks Like

A documented understanding of the codebase with clear integration points identified. No code changes yet.

---

## Phase 1 — Generic State Machine Extraction

### Goal
Extract a reusable state machine framework that both enemies and companions can use.

### What Already Exists
`CompanionCoreController.cs` contains:
- `RequestStateChange()` with validation
- `ForceState()` for bypassing validation
- `OnStateEnter()` / `OnStateExit()` hooks
- `OnStateChanged` event
- `IsValidTransition()` validation

### What Will Be Added

```
Scripts/AI/StateMachine/
├── IState.cs
├── IStateContext.cs
├── StateMachine.cs
└── StateTransitionTable.cs
```

### What is Explicitly NOT Being Changed
- `CompanionCoreController` will be **refactored** to use the new system, not replaced
- All existing companion behavior remains identical

### Concrete Implementation Steps

#### Step 1.1: Create IState Interface

```csharp
// Scripts/AI/StateMachine/IState.cs
using UnityEngine;

/// <summary>
/// Base interface for all states in the state machine.
/// States should be stateless - all per-instance data lives in the context.
/// </summary>
public interface IState<TContext> where TContext : class
{
    /// <summary>
    /// Called when entering this state.
    /// </summary>
    void Enter(TContext context);

    /// <summary>
    /// Called every frame while in this state.
    /// </summary>
    void Update(TContext context);

    /// <summary>
    /// Called at fixed intervals while in this state.
    /// </summary>
    void FixedUpdate(TContext context);

    /// <summary>
    /// Called when exiting this state.
    /// </summary>
    void Exit(TContext context);
}
```

#### Step 1.2: Create Base State Class

```csharp
// Scripts/AI/StateMachine/BaseState.cs
using UnityEngine;

/// <summary>
/// Abstract base class for states with default empty implementations.
/// Inherit from this to only override methods you need.
/// </summary>
public abstract class BaseState<TContext> : IState<TContext> where TContext : class
{
    public virtual void Enter(TContext context) { }
    public virtual void Update(TContext context) { }
    public virtual void FixedUpdate(TContext context) { }
    public virtual void Exit(TContext context) { }
}
```

#### Step 1.3: Create StateMachine Class

```csharp
// Scripts/AI/StateMachine/StateMachine.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic state machine that can be used by any agent type.
/// TState is typically an enum, TContext is the agent's data container.
/// </summary>
public class StateMachine<TState, TContext>
    where TState : Enum
    where TContext : class
{
    private readonly TContext _context;
    private readonly Dictionary<TState, IState<TContext>> _states = new();
    private readonly HashSet<(TState from, TState to)> _validTransitions = new();

    private IState<TContext> _currentStateInstance;
    private TState _currentState;
    private TState _previousState;
    private bool _isInitialized;

    public TState CurrentState => _currentState;
    public TState PreviousState => _previousState;
    public bool IsInitialized => _isInitialized;

    public event Action<TState, TState> OnStateChanged;

    public StateMachine(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Register a state implementation for a given state enum value.
    /// </summary>
    public void RegisterState(TState state, IState<TContext> stateInstance)
    {
        _states[state] = stateInstance ?? throw new ArgumentNullException(nameof(stateInstance));
    }

    /// <summary>
    /// Register a valid transition between two states.
    /// </summary>
    public void RegisterTransition(TState from, TState to)
    {
        _validTransitions.Add((from, to));
    }

    /// <summary>
    /// Register multiple transitions from one state to several others.
    /// </summary>
    public void RegisterTransitions(TState from, params TState[] toStates)
    {
        foreach (var to in toStates)
        {
            _validTransitions.Add((from, to));
        }
    }

    /// <summary>
    /// Initialize the state machine with a starting state.
    /// </summary>
    public void Initialize(TState initialState)
    {
        if (!_states.TryGetValue(initialState, out var stateInstance))
        {
            Debug.LogError($"[StateMachine] No state registered for {initialState}");
            return;
        }

        _currentState = initialState;
        _previousState = initialState;
        _currentStateInstance = stateInstance;
        _isInitialized = true;

        _currentStateInstance.Enter(_context);
    }

    /// <summary>
    /// Request a state change with transition validation.
    /// </summary>
    /// <returns>True if transition was successful</returns>
    public bool RequestStateChange(TState newState)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[StateMachine] Cannot change state - not initialized");
            return false;
        }

        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
            return true;

        if (!IsValidTransition(_currentState, newState))
        {
            Debug.LogWarning($"[StateMachine] Invalid transition: {_currentState} -> {newState}");
            return false;
        }

        return ExecuteTransition(newState);
    }

    /// <summary>
    /// Force a state change without validation (use sparingly).
    /// </summary>
    public void ForceState(TState newState)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[StateMachine] Cannot force state - not initialized");
            return;
        }

        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
            return;

        ExecuteTransition(newState);
    }

    private bool ExecuteTransition(TState newState)
    {
        if (!_states.TryGetValue(newState, out var newStateInstance))
        {
            Debug.LogError($"[StateMachine] No state registered for {newState}");
            return false;
        }

        // Exit current state
        _currentStateInstance?.Exit(_context);

        // Update state tracking
        _previousState = _currentState;
        _currentState = newState;
        _currentStateInstance = newStateInstance;

        // Enter new state
        _currentStateInstance.Enter(_context);

        // Fire event
        OnStateChanged?.Invoke(_previousState, _currentState);

        return true;
    }

    /// <summary>
    /// Check if a transition is valid.
    /// </summary>
    public bool IsValidTransition(TState from, TState to)
    {
        return _validTransitions.Contains((from, to));
    }

    /// <summary>
    /// Called every frame by the owner.
    /// </summary>
    public void Update()
    {
        if (_isInitialized)
        {
            _currentStateInstance?.Update(_context);
        }
    }

    /// <summary>
    /// Called at fixed intervals by the owner.
    /// </summary>
    public void FixedUpdate()
    {
        if (_isInitialized)
        {
            _currentStateInstance?.FixedUpdate(_context);
        }
    }
}
```

#### Step 1.4: Validate Extraction

Before proceeding, test the state machine independently:

```csharp
// Test in a simple MonoBehaviour
public enum TestState { A, B, C }

public class TestContext
{
    public int Counter;
}

public class TestStateA : BaseState<TestContext>
{
    public override void Enter(TestContext ctx) => Debug.Log("Entered A");
    public override void Update(TestContext ctx) => ctx.Counter++;
}

// In a test MonoBehaviour:
var ctx = new TestContext();
var sm = new StateMachine<TestState, TestContext>(ctx);
sm.RegisterState(TestState.A, new TestStateA());
sm.RegisterState(TestState.B, new TestStateB());
sm.RegisterTransition(TestState.A, TestState.B);
sm.Initialize(TestState.A);
// Later: sm.RequestStateChange(TestState.B);
```

### Validation Checklist

- [ ] `IState<T>` compiles with Enter/Update/FixedUpdate/Exit
- [ ] `StateMachine<TState, TContext>` compiles
- [ ] State registration works
- [ ] Transition registration works
- [ ] `RequestStateChange` validates and executes
- [ ] `ForceState` bypasses validation
- [ ] `OnStateChanged` event fires correctly
- [ ] State lifecycle (Enter → Update → Exit) works

### What "Done" Looks Like

A fully functional generic state machine in `Scripts/AI/StateMachine/` that can be instantiated with any enum and context type. The companion system has NOT been migrated yet.

---

## Phase 2 — EncounterDirector Foundation

### Goal
Create the global coordinator that decides when and where enemies spawn.

### What Already Exists
Nothing. This is new infrastructure.

### What Will Be Added

```
Scripts/Encounters/
├── EncounterDirector.cs       → Singleton coordinator
├── EncounterType.cs           → Enum for encounter categories
├── EncounterData.cs           → ScriptableObject for encounter definitions
└── IEncounterTrigger.cs       → Interface for trigger sources
```

### What is Explicitly NOT Being Changed
- No AI logic in this phase
- No enemy behavior
- Spawning only (placement, not behavior)

### Concrete Implementation Steps

#### Step 2.1: Create EncounterType Enum

```csharp
// Scripts/Encounters/EncounterType.cs

/// <summary>
/// Categories of encounters that can occur.
/// Used by EncounterDirector to select appropriate spawning logic.
/// </summary>
public enum EncounterType
{
    /// <summary>No active encounter.</summary>
    None,

    /// <summary>Random enemies appearing during exploration.</summary>
    RandomInvasion,

    /// <summary>Boss encounter with special rules.</summary>
    BossIncursion,

    /// <summary>Enemies native to a procedural planet.</summary>
    PlanetPopulation,

    /// <summary>Scripted story encounter.</summary>
    Scripted,

    /// <summary>Defensive wave (e.g., base defense).</summary>
    DefenseWave
}
```

#### Step 2.2: Create EncounterData ScriptableObject

```csharp
// Scripts/Encounters/EncounterData.cs
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Defines a specific encounter configuration.
/// </summary>
[CreateAssetMenu(fileName = "EncounterData", menuName = "StillOrbit/Encounters/Encounter Data")]
public class EncounterData : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string encounterId;

    [BoxGroup("Identity")]
    [SerializeField] private string displayName;

    [BoxGroup("Identity")]
    [SerializeField] private EncounterType encounterType;

    [BoxGroup("Spawning")]
    [Tooltip("Enemy archetypes that can spawn in this encounter")]
    [SerializeField] private List<EnemySpawnEntry> spawnPool = new();

    [BoxGroup("Spawning")]
    [Tooltip("Total enemies to spawn (min)")]
    [SerializeField] private int minEnemyCount = 1;

    [BoxGroup("Spawning")]
    [Tooltip("Total enemies to spawn (max)")]
    [SerializeField] private int maxEnemyCount = 5;

    [BoxGroup("Spawning")]
    [Tooltip("Spawn all at once or staggered")]
    [SerializeField] private bool staggeredSpawning = true;

    [BoxGroup("Spawning")]
    [ShowIf("staggeredSpawning")]
    [Tooltip("Delay between spawns")]
    [SerializeField] private float spawnInterval = 2f;

    [BoxGroup("Positioning")]
    [Tooltip("Minimum distance from player")]
    [SerializeField] private float minSpawnDistance = 15f;

    [BoxGroup("Positioning")]
    [Tooltip("Maximum distance from player")]
    [SerializeField] private float maxSpawnDistance = 30f;

    [BoxGroup("Positioning")]
    [Tooltip("Prefer spawning outside player's FOV")]
    [SerializeField] private bool preferOutsideFOV = true;

    [BoxGroup("Positioning")]
    [Tooltip("Require NavMesh-reachable spawn points")]
    [SerializeField] private bool requireNavMeshReachable = true;

    [BoxGroup("Duration")]
    [Tooltip("Auto-end encounter after this duration (0 = never)")]
    [SerializeField] private float maxDuration = 0f;

    [BoxGroup("Duration")]
    [Tooltip("End encounter when all enemies dead")]
    [SerializeField] private bool endOnAllDead = true;

    // Public Accessors
    public string EncounterId => encounterId;
    public string DisplayName => displayName;
    public EncounterType EncounterType => encounterType;
    public IReadOnlyList<EnemySpawnEntry> SpawnPool => spawnPool;
    public int MinEnemyCount => minEnemyCount;
    public int MaxEnemyCount => maxEnemyCount;
    public bool StaggeredSpawning => staggeredSpawning;
    public float SpawnInterval => spawnInterval;
    public float MinSpawnDistance => minSpawnDistance;
    public float MaxSpawnDistance => maxSpawnDistance;
    public bool PreferOutsideFOV => preferOutsideFOV;
    public bool RequireNavMeshReachable => requireNavMeshReachable;
    public float MaxDuration => maxDuration;
    public bool EndOnAllDead => endOnAllDead;

    public int GetRandomEnemyCount() => Random.Range(minEnemyCount, maxEnemyCount + 1);
}

/// <summary>
/// Entry in the spawn pool with weight for random selection.
/// </summary>
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Reference to enemy archetype (will be created in Phase 3)")]
    public EnemyArchetype Archetype;

    [Tooltip("Relative spawn weight (higher = more common)")]
    [Range(1, 100)]
    public int Weight = 10;

    [Tooltip("Maximum of this type per encounter")]
    public int MaxCount = 10;
}
```

#### Step 2.3: Create EncounterDirector Singleton

```csharp
// Scripts/Encounters/EncounterDirector.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Global singleton that manages encounter spawning.
/// Does NOT contain AI or combat logic - only spawn coordination.
/// </summary>
public class EncounterDirector : MonoBehaviour
{
    public static EncounterDirector Instance { get; private set; }

    [BoxGroup("References")]
    [SerializeField] private Transform playerTransform;

    [BoxGroup("References")]
    [SerializeField] private Camera playerCamera;

    [BoxGroup("Active Encounter")]
    [ShowInInspector, ReadOnly]
    private EncounterData currentEncounter;

    [BoxGroup("Active Encounter")]
    [ShowInInspector, ReadOnly]
    private EncounterState encounterState = EncounterState.Inactive;

    [BoxGroup("Active Encounter")]
    [ShowInInspector, ReadOnly]
    private float encounterStartTime;

    [BoxGroup("Active Encounter")]
    [ShowInInspector, ReadOnly]
    private int enemiesSpawned;

    [BoxGroup("Active Encounter")]
    [ShowInInspector, ReadOnly]
    private int enemiesRemaining;

    [BoxGroup("Tracking")]
    [ShowInInspector, ReadOnly]
    private readonly List<EnemyController> activeEnemies = new();

    [BoxGroup("Settings")]
    [SerializeField] private int maxSpawnAttempts = 30;

    [BoxGroup("Settings")]
    [SerializeField] private float navMeshSampleRadius = 2f;

    // Events
    public event Action<EncounterData> OnEncounterStarted;
    public event Action<EncounterData> OnEncounterEnded;
    public event Action<EnemyController> OnEnemySpawned;
    public event Action<EnemyController> OnEnemyDied;

    public EncounterState CurrentEncounterState => encounterState;
    public EncounterData CurrentEncounter => currentEncounter;
    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;
    public int EnemiesRemaining => enemiesRemaining;

    private Coroutine _spawnCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (playerTransform == null)
            playerTransform = PlayerManager.Instance?.transform;

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        if (encounterState == EncounterState.Active)
        {
            CheckEncounterEndConditions();
        }
    }

    #region Public API

    /// <summary>
    /// Start an encounter from data.
    /// </summary>
    public bool StartEncounter(EncounterData data)
    {
        if (data == null)
        {
            Debug.LogError("[EncounterDirector] Cannot start null encounter");
            return false;
        }

        if (encounterState != EncounterState.Inactive)
        {
            Debug.LogWarning($"[EncounterDirector] Cannot start encounter - already in state {encounterState}");
            return false;
        }

        currentEncounter = data;
        encounterState = EncounterState.Spawning;
        encounterStartTime = Time.time;
        enemiesSpawned = 0;
        enemiesRemaining = 0;

        Debug.Log($"[EncounterDirector] Starting encounter: {data.DisplayName}");

        _spawnCoroutine = StartCoroutine(SpawnEncounterEnemies(data));

        return true;
    }

    /// <summary>
    /// End the current encounter immediately.
    /// </summary>
    public void EndEncounter(bool killRemaining = false)
    {
        if (encounterState == EncounterState.Inactive)
            return;

        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        if (killRemaining)
        {
            foreach (var enemy in activeEnemies.ToArray())
            {
                if (enemy != null)
                    enemy.ForceKill();
            }
        }

        var endedEncounter = currentEncounter;

        currentEncounter = null;
        encounterState = EncounterState.Inactive;

        Debug.Log($"[EncounterDirector] Encounter ended: {endedEncounter?.DisplayName}");
        OnEncounterEnded?.Invoke(endedEncounter);
    }

    /// <summary>
    /// Register an enemy with the director (called by EnemyController on spawn).
    /// </summary>
    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null || activeEnemies.Contains(enemy))
            return;

        activeEnemies.Add(enemy);
        enemiesRemaining++;

        // Subscribe to death
        enemy.OnDeath += HandleEnemyDeath;

        OnEnemySpawned?.Invoke(enemy);
    }

    /// <summary>
    /// Unregister an enemy (called on death or despawn).
    /// </summary>
    public void UnregisterEnemy(EnemyController enemy)
    {
        if (enemy == null || !activeEnemies.Contains(enemy))
            return;

        activeEnemies.Remove(enemy);
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);

        enemy.OnDeath -= HandleEnemyDeath;
    }

    #endregion

    #region Spawning Logic

    private IEnumerator SpawnEncounterEnemies(EncounterData data)
    {
        int totalToSpawn = data.GetRandomEnemyCount();

        Debug.Log($"[EncounterDirector] Spawning {totalToSpawn} enemies");

        for (int i = 0; i < totalToSpawn; i++)
        {
            // Select enemy type
            var archetype = SelectWeightedArchetype(data.SpawnPool);
            if (archetype == null)
            {
                Debug.LogWarning("[EncounterDirector] No valid archetype selected");
                continue;
            }

            // Find spawn position
            if (TryFindSpawnPosition(data, out Vector3 spawnPos))
            {
                SpawnEnemy(archetype, spawnPos);
                enemiesSpawned++;
            }
            else
            {
                Debug.LogWarning("[EncounterDirector] Could not find valid spawn position");
            }

            // Stagger spawning
            if (data.StaggeredSpawning && i < totalToSpawn - 1)
            {
                yield return new WaitForSeconds(data.SpawnInterval);
            }
        }

        // Transition to active
        encounterState = EncounterState.Active;
        OnEncounterStarted?.Invoke(data);

        Debug.Log($"[EncounterDirector] Spawning complete. {enemiesSpawned} enemies spawned.");
    }

    private EnemyArchetype SelectWeightedArchetype(IReadOnlyList<EnemySpawnEntry> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in pool)
        {
            if (entry.Archetype != null)
                totalWeight += entry.Weight;
        }

        if (totalWeight == 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var entry in pool)
        {
            if (entry.Archetype == null)
                continue;

            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.Archetype;
        }

        return pool[0].Archetype;
    }

    private bool TryFindSpawnPosition(EncounterData data, out Vector3 position)
    {
        position = Vector3.zero;

        if (playerTransform == null)
            return false;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // Generate random position in ring around player
            float distance = UnityEngine.Random.Range(data.MinSpawnDistance, data.MaxSpawnDistance);
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );

            Vector3 candidatePos = playerTransform.position + offset;

            // Check FOV preference
            if (data.PreferOutsideFOV && playerCamera != null)
            {
                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(candidatePos);
                bool inView = viewportPoint.x > 0 && viewportPoint.x < 1 &&
                              viewportPoint.y > 0 && viewportPoint.y < 1 &&
                              viewportPoint.z > 0;

                // Skip if in view and we prefer outside (50% chance to allow anyway for variety)
                if (inView && UnityEngine.Random.value > 0.5f)
                    continue;
            }

            // Validate NavMesh
            if (data.RequireNavMeshReachable)
            {
                if (!NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                candidatePos = hit.position;

                // Verify path exists to player
                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(candidatePos, playerTransform.position, NavMesh.AllAreas, path))
                    continue;

                if (path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            position = candidatePos;
            return true;
        }

        return false;
    }

    private void SpawnEnemy(EnemyArchetype archetype, Vector3 position)
    {
        if (archetype == null || archetype.Prefab == null)
        {
            Debug.LogError("[EncounterDirector] Cannot spawn - null archetype or prefab");
            return;
        }

        GameObject enemyObj = Instantiate(archetype.Prefab, position, Quaternion.identity);

        var controller = enemyObj.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.Initialize(archetype);
            RegisterEnemy(controller);
        }
        else
        {
            Debug.LogError($"[EncounterDirector] Spawned prefab missing EnemyController: {archetype.Prefab.name}");
        }
    }

    #endregion

    #region Encounter State Management

    private void CheckEncounterEndConditions()
    {
        if (currentEncounter == null)
            return;

        // Check duration limit
        if (currentEncounter.MaxDuration > 0)
        {
            float elapsed = Time.time - encounterStartTime;
            if (elapsed >= currentEncounter.MaxDuration)
            {
                Debug.Log("[EncounterDirector] Encounter timed out");
                EndEncounter(false);
                return;
            }
        }

        // Check all dead condition
        if (currentEncounter.EndOnAllDead && enemiesRemaining <= 0 && enemiesSpawned > 0)
        {
            Debug.Log("[EncounterDirector] All enemies defeated");
            EndEncounter(false);
        }
    }

    private void HandleEnemyDeath(EnemyController enemy)
    {
        UnregisterEnemy(enemy);
        OnEnemyDied?.Invoke(enemy);
    }

    #endregion

    #region Debug

#if UNITY_EDITOR
    [Button("Force Start Random Invasion"), BoxGroup("Debug")]
    private void DebugForceRandomInvasion()
    {
        // Would need a reference to a test EncounterData asset
        Debug.Log("[EncounterDirector] Debug: Would start random invasion here");
    }

    [Button("End Current Encounter"), BoxGroup("Debug")]
    private void DebugEndEncounter()
    {
        EndEncounter(true);
    }

    [Button("Kill All Enemies"), BoxGroup("Debug")]
    private void DebugKillAll()
    {
        foreach (var enemy in activeEnemies.ToArray())
        {
            enemy?.ForceKill();
        }
    }

    [Button("Log Active Enemies"), BoxGroup("Debug")]
    private void DebugLogEnemies()
    {
        Debug.Log($"[EncounterDirector] Active enemies: {activeEnemies.Count}");
        foreach (var enemy in activeEnemies)
        {
            Debug.Log($"  - {enemy?.name ?? "null"}");
        }
    }
#endif

    #endregion
}

/// <summary>
/// Current state of the encounter system.
/// </summary>
public enum EncounterState
{
    Inactive,
    Spawning,
    Active,
    Ending
}
```

### Validation Checklist

- [ ] `EncounterDirector` singleton initializes correctly
- [ ] `StartEncounter()` transitions state and begins spawning
- [ ] Spawn positions are validated against NavMesh
- [ ] FOV check works (enemies prefer to spawn behind player)
- [ ] `EndEncounter()` cleans up properly
- [ ] Events fire correctly
- [ ] Odin debug buttons work in inspector

### What "Done" Looks Like

`EncounterDirector` can be triggered to spawn enemies at valid positions around the player. Enemies are tracked but have no AI yet. The system is fully decoupled from combat logic.

---

## Phase 3 — Enemy Archetype Data Model

### Goal
Create the ScriptableObject-based configuration system for enemy types.

### What Already Exists
`CompanionData.cs` provides the pattern to follow.

### What Will Be Added

```
Scripts/AI/Enemy/Data/
├── EnemyArchetype.cs          → Main configuration SO
├── EnemyMovementType.cs       → Movement style enum
├── EnemyCombatStyle.cs        → Combat behavior enum
└── EnemyAbilityData.cs        → Individual ability definition
```

### What is Explicitly NOT Being Changed
- No runtime behavior yet
- No state implementations
- Data structures only

### Concrete Implementation Steps

#### Step 3.1: Create Movement and Combat Enums

```csharp
// Scripts/AI/Enemy/Data/EnemyMovementType.cs

/// <summary>
/// How the enemy moves through the world.
/// </summary>
public enum EnemyMovementType
{
    /// <summary>Standard ground NavMesh navigation.</summary>
    Ground,

    /// <summary>Flying movement (ignores NavMesh Y).</summary>
    Flying,

    /// <summary>Stationary turret-style enemy.</summary>
    Stationary,

    /// <summary>Burrowing/tunneling movement.</summary>
    Burrowing
}
```

```csharp
// Scripts/AI/Enemy/Data/EnemyCombatStyle.cs

/// <summary>
/// Primary combat behavior pattern.
/// </summary>
public enum EnemyCombatStyle
{
    /// <summary>Close-range attacks only.</summary>
    Melee,

    /// <summary>Ranged attacks, maintains distance.</summary>
    Ranged,

    /// <summary>Mix of melee and ranged based on distance.</summary>
    Hybrid,

    /// <summary>Support role (buffs allies, debuffs player).</summary>
    Support,

    /// <summary>Suicide bomber style.</summary>
    Kamikaze
}
```

#### Step 3.2: Create EnemyAbilityData

```csharp
// Scripts/AI/Enemy/Data/EnemyAbilityData.cs
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Defines a single ability an enemy can use.
/// </summary>
[CreateAssetMenu(fileName = "EnemyAbility", menuName = "StillOrbit/Enemy/Ability")]
public class EnemyAbilityData : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string abilityId;

    [BoxGroup("Identity")]
    [SerializeField] private string displayName;

    [BoxGroup("Timing")]
    [Tooltip("Cooldown between uses")]
    [SerializeField] private float cooldown = 3f;

    [BoxGroup("Timing")]
    [Tooltip("Time to wind up before execution")]
    [SerializeField] private float windupTime = 0.5f;

    [BoxGroup("Timing")]
    [Tooltip("Recovery time after execution")]
    [SerializeField] private float recoveryTime = 0.3f;

    [BoxGroup("Range")]
    [Tooltip("Minimum range to use this ability")]
    [SerializeField] private float minRange = 0f;

    [BoxGroup("Range")]
    [Tooltip("Maximum range to use this ability")]
    [SerializeField] private float maxRange = 10f;

    [BoxGroup("Damage")]
    [SerializeField] private float baseDamage = 10f;

    [BoxGroup("Damage")]
    [SerializeField] private DamageType damageType = DamageType.Generic;

    [BoxGroup("Animation")]
    [Tooltip("Animator trigger name")]
    [SerializeField] private string animationTrigger;

    [BoxGroup("Animation")]
    [Tooltip("Animation state name for duration calculation")]
    [SerializeField] private string animationStateName;

    [BoxGroup("Effects")]
    [SerializeField] private GameObject vfxPrefab;

    [BoxGroup("Effects")]
    [SerializeField] private AudioClip sfxClip;

    [BoxGroup("Behavior")]
    [Tooltip("Can this ability be interrupted?")]
    [SerializeField] private bool canBeInterrupted = true;

    [BoxGroup("Behavior")]
    [Tooltip("Does enemy track target during windup?")]
    [SerializeField] private bool trackTargetDuringWindup = true;

    // Public Accessors
    public string AbilityId => abilityId;
    public string DisplayName => displayName;
    public float Cooldown => cooldown;
    public float WindupTime => windupTime;
    public float RecoveryTime => recoveryTime;
    public float MinRange => minRange;
    public float MaxRange => maxRange;
    public float BaseDamage => baseDamage;
    public DamageType DamageType => damageType;
    public string AnimationTrigger => animationTrigger;
    public string AnimationStateName => animationStateName;
    public GameObject VfxPrefab => vfxPrefab;
    public AudioClip SfxClip => sfxClip;
    public bool CanBeInterrupted => canBeInterrupted;
    public bool TrackTargetDuringWindup => trackTargetDuringWindup;

    /// <summary>
    /// Check if a target is within range for this ability.
    /// </summary>
    public bool IsInRange(float distance)
    {
        return distance >= minRange && distance <= maxRange;
    }
}
```

#### Step 3.3: Create EnemyArchetype ScriptableObject

```csharp
// Scripts/AI/Enemy/Data/EnemyArchetype.cs
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Complete definition of an enemy type.
/// All enemy behavior is driven by this data - no EnemyX.cs per type.
/// </summary>
[CreateAssetMenu(fileName = "EnemyArchetype", menuName = "StillOrbit/Enemy/Archetype")]
public class EnemyArchetype : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string archetypeId;

    [BoxGroup("Identity")]
    [SerializeField] private string displayName;

    [BoxGroup("Identity")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [BoxGroup("Prefab")]
    [Required]
    [SerializeField] private GameObject prefab;

    [BoxGroup("Stats")]
    [SerializeField] private int maxHealth = 100;

    [BoxGroup("Stats")]
    [SerializeField] private DamageType damageType = DamageType.Flesh;

    [BoxGroup("Stats")]
    [Tooltip("Damage resistance multiplier (0.5 = takes half damage)")]
    [Range(0f, 2f)]
    [SerializeField] private float damageResistance = 1f;

    [BoxGroup("Movement")]
    [SerializeField] private EnemyMovementType movementType = EnemyMovementType.Ground;

    [BoxGroup("Movement")]
    [SerializeField] private float moveSpeed = 4f;

    [BoxGroup("Movement")]
    [SerializeField] private float turnSpeed = 120f;

    [BoxGroup("Movement")]
    [ShowIf("movementType", EnemyMovementType.Flying)]
    [SerializeField] private float flyingHeight = 3f;

    [BoxGroup("Combat")]
    [SerializeField] private EnemyCombatStyle combatStyle = EnemyCombatStyle.Melee;

    [BoxGroup("Combat")]
    [Tooltip("Preferred engagement distance")]
    [SerializeField] private float preferredCombatRange = 2f;

    [BoxGroup("Combat")]
    [Tooltip("Distance at which enemy stops approaching")]
    [SerializeField] private float attackRange = 1.5f;

    [BoxGroup("Abilities")]
    [SerializeField] private List<EnemyAbilityData> abilities = new();

    [BoxGroup("Abilities")]
    [Tooltip("Primary attack ability (index in abilities list)")]
    [SerializeField] private int primaryAbilityIndex = 0;

    [BoxGroup("Perception")]
    [Tooltip("How far the enemy can see")]
    [SerializeField] private float sightRange = 20f;

    [BoxGroup("Perception")]
    [Tooltip("Field of view angle")]
    [Range(0f, 360f)]
    [SerializeField] private float sightAngle = 120f;

    [BoxGroup("Perception")]
    [Tooltip("How far the enemy can hear")]
    [SerializeField] private float hearingRange = 15f;

    [BoxGroup("Perception")]
    [Tooltip("Time to lose interest after losing sight")]
    [SerializeField] private float memoryDuration = 5f;

    [BoxGroup("Behavior")]
    [Tooltip("Will patrol when idle")]
    [SerializeField] private bool canPatrol = true;

    [BoxGroup("Behavior")]
    [Tooltip("Will flee when low health")]
    [SerializeField] private bool canFlee = false;

    [BoxGroup("Behavior")]
    [ShowIf("canFlee")]
    [Range(0f, 1f)]
    [SerializeField] private float fleeHealthThreshold = 0.2f;

    [BoxGroup("Boss")]
    [Tooltip("Is this a boss enemy?")]
    [SerializeField] private bool isBoss = false;

    [BoxGroup("Boss")]
    [ShowIf("isBoss")]
    [SerializeField] private List<BossPhase> bossPhases = new();

    [BoxGroup("Loot")]
    [Tooltip("Experience points awarded on death")]
    [SerializeField] private int experienceReward = 10;

    // Future: loot table reference

    // Public Accessors
    public string ArchetypeId => archetypeId;
    public string DisplayName => displayName;
    public string Description => description;
    public GameObject Prefab => prefab;
    public int MaxHealth => maxHealth;
    public DamageType DamageType => damageType;
    public float DamageResistance => damageResistance;
    public EnemyMovementType MovementType => movementType;
    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;
    public float FlyingHeight => flyingHeight;
    public EnemyCombatStyle CombatStyle => combatStyle;
    public float PreferredCombatRange => preferredCombatRange;
    public float AttackRange => attackRange;
    public IReadOnlyList<EnemyAbilityData> Abilities => abilities;
    public int PrimaryAbilityIndex => primaryAbilityIndex;
    public float SightRange => sightRange;
    public float SightAngle => sightAngle;
    public float HearingRange => hearingRange;
    public float MemoryDuration => memoryDuration;
    public bool CanPatrol => canPatrol;
    public bool CanFlee => canFlee;
    public float FleeHealthThreshold => fleeHealthThreshold;
    public bool IsBoss => isBoss;
    public IReadOnlyList<BossPhase> BossPhases => bossPhases;
    public int ExperienceReward => experienceReward;

    public EnemyAbilityData PrimaryAbility =>
        abilities.Count > primaryAbilityIndex ? abilities[primaryAbilityIndex] : null;

    /// <summary>
    /// Get ability by index, safely.
    /// </summary>
    public EnemyAbilityData GetAbility(int index)
    {
        if (index < 0 || index >= abilities.Count)
            return null;
        return abilities[index];
    }

#if UNITY_EDITOR
    [Button("Validate"), BoxGroup("Debug")]
    private void Validate()
    {
        bool valid = true;

        if (string.IsNullOrEmpty(archetypeId))
        {
            Debug.LogWarning($"[{name}] Archetype ID is empty");
            valid = false;
        }

        if (prefab == null)
        {
            Debug.LogError($"[{name}] Prefab is not assigned!");
            valid = false;
        }

        if (abilities.Count == 0)
        {
            Debug.LogWarning($"[{name}] No abilities assigned");
        }

        if (primaryAbilityIndex >= abilities.Count)
        {
            Debug.LogWarning($"[{name}] Primary ability index out of range");
            valid = false;
        }

        if (isBoss && bossPhases.Count == 0)
        {
            Debug.LogWarning($"[{name}] Boss has no phases defined");
        }

        if (valid)
            Debug.Log($"[{name}] Validation passed!");
    }
#endif
}

/// <summary>
/// Defines a boss phase with health threshold and ability changes.
/// </summary>
[System.Serializable]
public class BossPhase
{
    [Tooltip("Phase triggers when health drops below this percentage")]
    [Range(0f, 1f)]
    public float HealthThreshold = 0.5f;

    [Tooltip("Display name for this phase")]
    public string PhaseName = "Phase 2";

    [Tooltip("Abilities available during this phase")]
    public List<EnemyAbilityData> PhaseAbilities = new();

    [Tooltip("Speed multiplier during this phase")]
    public float SpeedMultiplier = 1.2f;

    [Tooltip("Damage multiplier during this phase")]
    public float DamageMultiplier = 1.5f;

    [Tooltip("Trigger special behavior on phase entry")]
    public string OnEnterTrigger;
}
```

### Validation Checklist

- [ ] `EnemyArchetype` SO can be created via Asset menu
- [ ] All fields serialize and display correctly in Inspector
- [ ] Odin attributes render properly
- [ ] `EnemyAbilityData` SO can be created
- [ ] Abilities can be assigned to archetypes
- [ ] Boss phases serialize correctly
- [ ] Validation button works

### What "Done" Looks Like

A complete data model where designers can create new enemy types entirely through ScriptableObjects without touching code. No actual enemy behavior yet.

---

## Phase 4 — EnemyController Foundation

### Goal
Create the central enemy orchestrator component, following the `CompanionCoreController` pattern.

### What Already Exists
- `CompanionCoreController` as architectural reference
- Generic `StateMachine<TState, TContext>` from Phase 1

### What Will Be Added

```
Scripts/AI/Enemy/
├── EnemyController.cs         → Main orchestrator
├── EnemyContext.cs            → Shared data for states
├── EnemyState.cs              → State enum
└── EnemyRegistry.cs           → Optional global tracking
```

### What is Explicitly NOT Being Changed
- `HealthComponent` is reused as-is
- `IDamageable` is implemented via `HealthComponent`
- No changes to player systems

### Concrete Implementation Steps

#### Step 4.1: Create EnemyState Enum

```csharp
// Scripts/AI/Enemy/EnemyState.cs

/// <summary>
/// Possible states for enemies.
/// States are implemented as IState classes, not embedded logic.
/// </summary>
public enum EnemyState
{
    /// <summary>Inactive/not yet initialized.</summary>
    Inactive,

    /// <summary>Idle, no target, not moving.</summary>
    Idle,

    /// <summary>Patrolling along waypoints or randomly.</summary>
    Patrol,

    /// <summary>Investigating a noise or last known position.</summary>
    Investigate,

    /// <summary>Actively chasing a target.</summary>
    Chase,

    /// <summary>Positioning for attack.</summary>
    Positioning,

    /// <summary>Executing an attack ability.</summary>
    Attack,

    /// <summary>Reacting to being hit (stagger).</summary>
    Hurt,

    /// <summary>Fleeing from danger.</summary>
    Flee,

    /// <summary>Dead, playing death animation.</summary>
    Dead
}
```

#### Step 4.2: Create EnemyContext

```csharp
// Scripts/AI/Enemy/EnemyContext.cs
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared context data accessible by all enemy states.
/// This is the "blackboard" for the enemy's state machine.
/// States should be stateless - all per-instance data lives here.
/// </summary>
public class EnemyContext
{
    // Core References
    public EnemyController Controller { get; }
    public EnemyArchetype Archetype { get; }
    public Transform Transform { get; }
    public NavMeshAgent NavAgent { get; }
    public Animator Animator { get; }
    public HealthComponent Health { get; }

    // Target Tracking
    public Transform CurrentTarget { get; set; }
    public Vector3 LastKnownTargetPosition { get; set; }
    public float TimeSinceTargetSeen { get; set; }
    public bool HasTarget => CurrentTarget != null;

    // Combat State
    public float LastAttackTime { get; set; }
    public int CurrentAbilityIndex { get; set; }
    public bool IsAbilityInProgress { get; set; }
    public float AbilityStartTime { get; set; }

    // Movement
    public Vector3 PatrolDestination { get; set; }
    public bool HasPatrolDestination { get; set; }
    public float StuckTimer { get; set; }
    public Vector3 LastPosition { get; set; }

    // Boss Phase (if applicable)
    public int CurrentBossPhase { get; set; }
    public bool PhaseTransitionPending { get; set; }

    // Utility
    public float StateEnterTime { get; set; }
    public float TimeSinceStateEnter => Time.time - StateEnterTime;

    public EnemyContext(
        EnemyController controller,
        EnemyArchetype archetype,
        Transform transform,
        NavMeshAgent navAgent,
        Animator animator,
        HealthComponent health)
    {
        Controller = controller;
        Archetype = archetype;
        Transform = transform;
        NavAgent = navAgent;
        Animator = animator;
        Health = health;
    }

    /// <summary>
    /// Get distance to current target.
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (CurrentTarget == null)
            return float.MaxValue;
        return Vector3.Distance(Transform.position, CurrentTarget.position);
    }

    /// <summary>
    /// Get direction to current target.
    /// </summary>
    public Vector3 GetDirectionToTarget()
    {
        if (CurrentTarget == null)
            return Transform.forward;
        return (CurrentTarget.position - Transform.position).normalized;
    }

    /// <summary>
    /// Check if currently within attack range.
    /// </summary>
    public bool IsInAttackRange()
    {
        return GetDistanceToTarget() <= Archetype.AttackRange;
    }

    /// <summary>
    /// Check if primary ability is off cooldown.
    /// </summary>
    public bool CanUsePrimaryAbility()
    {
        var ability = Archetype.PrimaryAbility;
        if (ability == null)
            return false;

        return Time.time >= LastAttackTime + ability.Cooldown;
    }

    /// <summary>
    /// Reset state timing on state enter.
    /// </summary>
    public void OnStateEnter()
    {
        StateEnterTime = Time.time;
    }
}
```

#### Step 4.3: Create EnemyController

```csharp
// Scripts/AI/Enemy/EnemyController.cs
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Central coordinator for enemy behavior.
/// Mirrors CompanionCoreController architecture.
/// All behavior is data-driven via EnemyArchetype.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyController : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [SerializeField] private EnemyArchetype archetype;

    [BoxGroup("References")]
    [SerializeField] private Transform visualRoot;

    [BoxGroup("References")]
    [SerializeField] private Animator animator;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private EnemyState currentState = EnemyState.Inactive;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isInitialized;

    // Components
    private NavMeshAgent _navAgent;
    private HealthComponent _health;

    // State Machine
    private StateMachine<EnemyState, EnemyContext> _stateMachine;
    private EnemyContext _context;

    // Perception (will be added in Phase 5)
    private EnemyPerception _perception;

    // Events
    public event Action<EnemyController> OnDeath;
    public event Action<EnemyState, EnemyState> OnStateChanged;

    // Public Accessors
    public EnemyArchetype Archetype => archetype;
    public EnemyState CurrentState => currentState;
    public bool IsInitialized => isInitialized;
    public bool IsAlive => _health != null && _health.IsAlive();
    public HealthComponent Health => _health;
    public NavMeshAgent NavAgent => _navAgent;
    public EnemyContext Context => _context;
    public Transform CurrentTarget => _context?.CurrentTarget;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _health = GetComponent<HealthComponent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // Auto-initialize if archetype is assigned in inspector
        if (archetype != null && !isInitialized)
        {
            Initialize(archetype);
        }
    }

    private void Update()
    {
        if (!isInitialized || currentState == EnemyState.Dead)
            return;

        _stateMachine?.Update();
        UpdatePerception();
    }

    private void FixedUpdate()
    {
        if (!isInitialized || currentState == EnemyState.Dead)
            return;

        _stateMachine?.FixedUpdate();
    }

    /// <summary>
    /// Initialize enemy with archetype data.
    /// Called by EncounterDirector after spawning.
    /// </summary>
    public void Initialize(EnemyArchetype archetypeData)
    {
        if (isInitialized)
        {
            Debug.LogWarning($"[EnemyController] {name} already initialized");
            return;
        }

        archetype = archetypeData;

        // Configure NavMeshAgent
        _navAgent.speed = archetype.MoveSpeed;
        _navAgent.angularSpeed = archetype.TurnSpeed;

        // Configure Health
        _health.SetMaxHealth(archetype.MaxHealth, true);
        _health.OnDeath += HandleDeath;

        // Create context
        _context = new EnemyContext(
            this,
            archetype,
            transform,
            _navAgent,
            animator,
            _health
        );

        // Initialize state machine
        InitializeStateMachine();

        isInitialized = true;

        Debug.Log($"[EnemyController] {name} initialized as {archetype.DisplayName}");
    }

    private void InitializeStateMachine()
    {
        _stateMachine = new StateMachine<EnemyState, EnemyContext>(_context);

        // Register states (implementations in Phase 6)
        _stateMachine.RegisterState(EnemyState.Inactive, new EnemyInactiveState());
        _stateMachine.RegisterState(EnemyState.Idle, new EnemyIdleState());
        _stateMachine.RegisterState(EnemyState.Patrol, new EnemyPatrolState());
        _stateMachine.RegisterState(EnemyState.Chase, new EnemyChaseState());
        _stateMachine.RegisterState(EnemyState.Attack, new EnemyAttackState());
        _stateMachine.RegisterState(EnemyState.Hurt, new EnemyHurtState());
        _stateMachine.RegisterState(EnemyState.Dead, new EnemyDeadState());

        // Register transitions
        RegisterStateTransitions();

        // Subscribe to state changes
        _stateMachine.OnStateChanged += HandleStateChanged;

        // Start in Idle
        _stateMachine.Initialize(EnemyState.Idle);
    }

    private void RegisterStateTransitions()
    {
        // From Inactive
        _stateMachine.RegisterTransitions(EnemyState.Inactive,
            EnemyState.Idle);

        // From Idle
        _stateMachine.RegisterTransitions(EnemyState.Idle,
            EnemyState.Patrol,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Patrol
        _stateMachine.RegisterTransitions(EnemyState.Patrol,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Chase
        _stateMachine.RegisterTransitions(EnemyState.Chase,
            EnemyState.Idle,
            EnemyState.Attack,
            EnemyState.Positioning,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Attack
        _stateMachine.RegisterTransitions(EnemyState.Attack,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Hurt
        _stateMachine.RegisterTransitions(EnemyState.Hurt,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Flee,
            EnemyState.Dead);

        // From Flee
        _stateMachine.RegisterTransitions(EnemyState.Flee,
            EnemyState.Idle,
            EnemyState.Dead);
    }

    private void HandleStateChanged(EnemyState from, EnemyState to)
    {
        currentState = to;
        _context.OnStateEnter();
        OnStateChanged?.Invoke(from, to);

        Debug.Log($"[EnemyController] {name}: {from} -> {to}");
    }

    private void UpdatePerception()
    {
        // Will be implemented in Phase 5
        // For now, simple player detection
        if (_context.CurrentTarget == null)
        {
            var player = PlayerManager.Instance?.transform;
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.position);
                if (distance <= archetype.SightRange)
                {
                    _context.CurrentTarget = player;
                    _context.LastKnownTargetPosition = player.position;
                    _context.TimeSinceTargetSeen = 0f;
                }
            }
        }
        else
        {
            // Update last known position
            _context.LastKnownTargetPosition = _context.CurrentTarget.position;
            _context.TimeSinceTargetSeen = 0f;
        }
    }

    private void HandleDeath()
    {
        _stateMachine.ForceState(EnemyState.Dead);
        OnDeath?.Invoke(this);
    }

    /// <summary>
    /// Force kill this enemy (for debug or encounter end).
    /// </summary>
    public void ForceKill()
    {
        if (!IsAlive)
            return;

        _health.TakeDamage(_health.MaxHealth * 10, DamageType.Generic, null);
    }

    /// <summary>
    /// Request a state change.
    /// </summary>
    public bool RequestStateChange(EnemyState newState)
    {
        return _stateMachine?.RequestStateChange(newState) ?? false;
    }

    /// <summary>
    /// Force a state change without validation.
    /// </summary>
    public void ForceState(EnemyState newState)
    {
        _stateMachine?.ForceState(newState);
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnDeath -= HandleDeath;

        // Unregister from encounter director
        EncounterDirector.Instance?.UnregisterEnemy(this);
    }

#if UNITY_EDITOR
    [Button("Force Chase Player"), BoxGroup("Debug")]
    private void DebugChasePlayer()
    {
        if (!Application.isPlaying) return;

        _context.CurrentTarget = PlayerManager.Instance?.transform;
        RequestStateChange(EnemyState.Chase);
    }

    [Button("Force Attack"), BoxGroup("Debug")]
    private void DebugForceAttack()
    {
        if (!Application.isPlaying) return;
        RequestStateChange(EnemyState.Attack);
    }

    [Button("Force Kill"), BoxGroup("Debug")]
    private void DebugForceKill()
    {
        if (!Application.isPlaying) return;
        ForceKill();
    }

    [Button("Log Context"), BoxGroup("Debug")]
    private void DebugLogContext()
    {
        if (_context == null)
        {
            Debug.Log("Context is null");
            return;
        }

        Debug.Log($"Target: {_context.CurrentTarget?.name ?? "none"}");
        Debug.Log($"Distance to target: {_context.GetDistanceToTarget():F2}");
        Debug.Log($"In attack range: {_context.IsInAttackRange()}");
        Debug.Log($"Can use primary ability: {_context.CanUsePrimaryAbility()}");
    }

    private void OnDrawGizmosSelected()
    {
        if (archetype == null) return;

        // Sight range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, archetype.SightRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, archetype.AttackRange);

        // Hearing range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, archetype.HearingRange);

        // Target line
        if (_context?.CurrentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _context.CurrentTarget.position);
        }
    }
#endif
}
```

### Validation Checklist

- [ ] `EnemyController` compiles
- [ ] Enemy prefab can have `EnemyController` attached
- [ ] `HealthComponent` on same GameObject works
- [ ] Initialize() configures NavMeshAgent correctly
- [ ] State machine initializes and starts in Idle
- [ ] Debug buttons work in inspector
- [ ] Gizmos render correctly

### What "Done" Looks Like

An enemy prefab with `EnemyController` + `HealthComponent` that initializes correctly, has a functioning state machine (states are stubs), and tracks a target. The enemy doesn't move or attack yet.

---

## Phase 5 — Perception System

### Goal
Create a perception system that states can query without embedding raycast logic.

### What Already Exists
- `PlayerAimController` does raycasts for player aiming (reference pattern)

### What Will Be Added

```
Scripts/AI/Perception/
├── EnemyPerception.cs         → Main perception component
├── PerceptionTarget.cs        → Tracked target data
└── IPerceivable.cs            → Interface for detectable objects
```

### What is Explicitly NOT Being Changed
- States will QUERY perception, not perform raycasts
- No duplicate raycast logic

### Concrete Implementation Steps

#### Step 5.1: Create IPerceivable Interface

```csharp
// Scripts/AI/Perception/IPerceivable.cs
using UnityEngine;

/// <summary>
/// Interface for objects that can be perceived by enemies.
/// Implement on player, companions, or any detectable entity.
/// </summary>
public interface IPerceivable
{
    /// <summary>World position of this perceivable target.</summary>
    Vector3 PerceptionPosition { get; }

    /// <summary>Is this target currently perceivable (not hidden, etc.).</summary>
    bool IsPerceivable { get; }

    /// <summary>How "loud" this target is (affects hearing detection).</summary>
    float NoiseLevel { get; }

    /// <summary>Priority when multiple targets exist (higher = preferred).</summary>
    int TargetPriority { get; }
}
```

#### Step 5.2: Create PerceptionTarget Data Class

```csharp
// Scripts/AI/Perception/PerceptionTarget.cs
using UnityEngine;

/// <summary>
/// Data about a perceived target.
/// </summary>
public class PerceptionTarget
{
    public Transform Transform { get; set; }
    public Vector3 LastKnownPosition { get; set; }
    public float LastSeenTime { get; set; }
    public float LastHeardTime { get; set; }
    public bool IsCurrentlyVisible { get; set; }
    public bool IsCurrentlyAudible { get; set; }
    public float Distance { get; set; }
    public int Priority { get; set; }

    /// <summary>
    /// Time since this target was last perceived (seen or heard).
    /// </summary>
    public float TimeSincePerceived => Time.time - Mathf.Max(LastSeenTime, LastHeardTime);

    /// <summary>
    /// Is this target still in memory (within memory duration)?
    /// </summary>
    public bool IsInMemory(float memoryDuration)
    {
        return TimeSincePerceived <= memoryDuration;
    }

    /// <summary>
    /// Is this target actively perceived right now?
    /// </summary>
    public bool IsActivelyPerceived => IsCurrentlyVisible || IsCurrentlyAudible;
}
```

#### Step 5.3: Create EnemyPerception Component

```csharp
// Scripts/AI/Perception/EnemyPerception.cs
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles enemy perception (sight, hearing).
/// States query this component rather than performing their own raycasts.
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("Configuration")]
    [Tooltip("Transform to raycast from (usually head/eyes)")]
    [SerializeField] private Transform eyePoint;

    [BoxGroup("Configuration")]
    [SerializeField] private LayerMask sightBlockingLayers;

    [BoxGroup("Configuration")]
    [SerializeField] private LayerMask targetLayers;

    [BoxGroup("Performance")]
    [Tooltip("Perception update rate (times per second)")]
    [SerializeField] private float updateRate = 10f;

    [BoxGroup("Performance")]
    [Tooltip("Max targets to track simultaneously")]
    [SerializeField] private int maxTrackedTargets = 5;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private PerceptionTarget primaryTarget;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private readonly List<PerceptionTarget> trackedTargets = new();

    private float _lastUpdateTime;
    private float _updateInterval;

    // Cached archetype values
    private float _sightRange;
    private float _sightAngle;
    private float _hearingRange;
    private float _memoryDuration;

    // Public accessors
    public PerceptionTarget PrimaryTarget => primaryTarget;
    public IReadOnlyList<PerceptionTarget> TrackedTargets => trackedTargets;
    public bool HasTarget => primaryTarget != null && primaryTarget.IsInMemory(_memoryDuration);
    public Transform TargetTransform => primaryTarget?.Transform;
    public Vector3 LastKnownTargetPosition => primaryTarget?.LastKnownPosition ?? transform.position;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        if (eyePoint == null)
            eyePoint = transform;
    }

    private void Start()
    {
        _updateInterval = 1f / updateRate;

        if (controller.Archetype != null)
        {
            CacheArchetypeValues();
        }
    }

    private void Update()
    {
        if (!controller.IsInitialized)
            return;

        // Throttled perception updates
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = Time.time;
            UpdatePerception();
        }
    }

    private void CacheArchetypeValues()
    {
        var archetype = controller.Archetype;
        _sightRange = archetype.SightRange;
        _sightAngle = archetype.SightAngle;
        _hearingRange = archetype.HearingRange;
        _memoryDuration = archetype.MemoryDuration;
    }

    private void UpdatePerception()
    {
        // Find potential targets
        var colliders = Physics.OverlapSphere(
            transform.position,
            Mathf.Max(_sightRange, _hearingRange),
            targetLayers
        );

        // Update tracked targets
        foreach (var col in colliders)
        {
            var perceivable = col.GetComponentInParent<IPerceivable>();
            if (perceivable == null || !perceivable.IsPerceivable)
                continue;

            var target = GetOrCreateTarget(col.transform);
            UpdateTargetPerception(target, perceivable);
        }

        // Clean up stale targets
        CleanupStaleTargets();

        // Select primary target
        SelectPrimaryTarget();

        // Update controller context
        UpdateControllerContext();
    }

    private PerceptionTarget GetOrCreateTarget(Transform targetTransform)
    {
        var existing = trackedTargets.Find(t => t.Transform == targetTransform);
        if (existing != null)
            return existing;

        if (trackedTargets.Count >= maxTrackedTargets)
        {
            // Remove oldest
            float oldestTime = float.MaxValue;
            PerceptionTarget oldest = null;
            foreach (var t in trackedTargets)
            {
                float perceiveTime = Mathf.Max(t.LastSeenTime, t.LastHeardTime);
                if (perceiveTime < oldestTime)
                {
                    oldestTime = perceiveTime;
                    oldest = t;
                }
            }
            if (oldest != null)
                trackedTargets.Remove(oldest);
        }

        var newTarget = new PerceptionTarget { Transform = targetTransform };
        trackedTargets.Add(newTarget);
        return newTarget;
    }

    private void UpdateTargetPerception(PerceptionTarget target, IPerceivable perceivable)
    {
        Vector3 targetPos = perceivable.PerceptionPosition;
        float distance = Vector3.Distance(transform.position, targetPos);
        target.Distance = distance;
        target.Priority = perceivable.TargetPriority;

        // Sight check
        target.IsCurrentlyVisible = CheckSight(targetPos, distance);
        if (target.IsCurrentlyVisible)
        {
            target.LastSeenTime = Time.time;
            target.LastKnownPosition = targetPos;
        }

        // Hearing check
        target.IsCurrentlyAudible = CheckHearing(distance, perceivable.NoiseLevel);
        if (target.IsCurrentlyAudible)
        {
            target.LastHeardTime = Time.time;
            target.LastKnownPosition = targetPos;
        }
    }

    private bool CheckSight(Vector3 targetPos, float distance)
    {
        // Range check
        if (distance > _sightRange)
            return false;

        // Angle check
        Vector3 directionToTarget = (targetPos - eyePoint.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToTarget);
        if (angle > _sightAngle / 2f)
            return false;

        // Line of sight check
        Vector3 rayOrigin = eyePoint.position;
        Vector3 rayDirection = targetPos - rayOrigin;

        if (Physics.Raycast(rayOrigin, rayDirection.normalized, out RaycastHit hit, distance, sightBlockingLayers))
        {
            // Check if we hit something before the target
            if (hit.distance < distance - 0.5f)
                return false;
        }

        return true;
    }

    private bool CheckHearing(float distance, float noiseLevel)
    {
        if (distance > _hearingRange)
            return false;

        // Noise level affects effective hearing range
        float effectiveHearingRange = _hearingRange * noiseLevel;
        return distance <= effectiveHearingRange;
    }

    private void CleanupStaleTargets()
    {
        trackedTargets.RemoveAll(t =>
            t.Transform == null ||
            !t.IsInMemory(_memoryDuration)
        );
    }

    private void SelectPrimaryTarget()
    {
        PerceptionTarget best = null;
        float bestScore = float.MinValue;

        foreach (var target in trackedTargets)
        {
            if (!target.IsInMemory(_memoryDuration))
                continue;

            // Score: priority, visibility, distance
            float score = target.Priority * 100f;
            if (target.IsCurrentlyVisible)
                score += 50f;
            if (target.IsCurrentlyAudible)
                score += 25f;
            score -= target.Distance;

            if (score > bestScore)
            {
                bestScore = score;
                best = target;
            }
        }

        primaryTarget = best;
    }

    private void UpdateControllerContext()
    {
        if (controller.Context == null)
            return;

        controller.Context.CurrentTarget = primaryTarget?.Transform;
        controller.Context.LastKnownTargetPosition = primaryTarget?.LastKnownPosition ?? Vector3.zero;
        controller.Context.TimeSinceTargetSeen = primaryTarget?.TimeSincePerceived ?? float.MaxValue;
    }

    /// <summary>
    /// Manually alert this enemy to a position (e.g., from gunshot).
    /// </summary>
    public void AlertToPosition(Vector3 position, float priority = 1f)
    {
        // Create temporary "phantom" target
        if (primaryTarget == null)
        {
            primaryTarget = new PerceptionTarget
            {
                LastKnownPosition = position,
                LastHeardTime = Time.time,
                Priority = (int)priority
            };
        }
        else
        {
            primaryTarget.LastKnownPosition = position;
            primaryTarget.LastHeardTime = Time.time;
        }
    }

    /// <summary>
    /// Clear all perception (e.g., after respawn).
    /// </summary>
    public void ClearPerception()
    {
        trackedTargets.Clear();
        primaryTarget = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && controller?.Archetype == null)
            return;

        var archetype = controller?.Archetype;
        float sightRange = archetype?.SightRange ?? 20f;
        float sightAngle = archetype?.SightAngle ?? 120f;
        float hearingRange = archetype?.HearingRange ?? 15f;

        Vector3 eyePos = eyePoint != null ? eyePoint.position : transform.position;

        // Sight cone
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        DrawViewCone(eyePos, transform.forward, sightAngle, sightRange);

        // Hearing range
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, hearingRange);

        // Primary target
        if (primaryTarget != null)
        {
            Gizmos.color = primaryTarget.IsCurrentlyVisible ? Color.green : Color.yellow;
            Gizmos.DrawLine(eyePos, primaryTarget.LastKnownPosition);
            Gizmos.DrawWireSphere(primaryTarget.LastKnownPosition, 0.5f);
        }
    }

    private void DrawViewCone(Vector3 origin, Vector3 forward, float angle, float range)
    {
        int segments = 20;
        float halfAngle = angle / 2f;

        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

        Gizmos.DrawLine(origin, origin + leftDir * range);
        Gizmos.DrawLine(origin, origin + rightDir * range);

        for (int i = 0; i < segments; i++)
        {
            float t1 = (float)i / segments;
            float t2 = (float)(i + 1) / segments;
            float a1 = Mathf.Lerp(-halfAngle, halfAngle, t1);
            float a2 = Mathf.Lerp(-halfAngle, halfAngle, t2);

            Vector3 p1 = origin + Quaternion.Euler(0, a1, 0) * forward * range;
            Vector3 p2 = origin + Quaternion.Euler(0, a2, 0) * forward * range;

            Gizmos.DrawLine(p1, p2);
        }
    }
#endif
}
```

#### Step 5.4: Add IPerceivable to Player

```csharp
// Add this component to the Player GameObject
// Scripts/Player/PlayerPerceivable.cs
using UnityEngine;

/// <summary>
/// Makes the player perceivable by enemies.
/// </summary>
public class PlayerPerceivable : MonoBehaviour, IPerceivable
{
    [SerializeField] private Transform perceptionPoint;
    [SerializeField] private float baseNoiseLevel = 0.5f;

    private PlayerLocomotionController _locomotion;

    public Vector3 PerceptionPosition => perceptionPoint != null ? perceptionPoint.position : transform.position;
    public bool IsPerceivable => true; // Could check for invisibility power-ups, etc.
    public int TargetPriority => 100; // Player is always high priority

    public float NoiseLevel
    {
        get
        {
            // Louder when moving, sprinting, shooting
            float noise = baseNoiseLevel;

            if (_locomotion != null)
            {
                if (_locomotion.IsMoving)
                    noise += 0.3f;
                if (_locomotion.IsSprinting)
                    noise += 0.5f;
            }

            return Mathf.Clamp01(noise);
        }
    }

    private void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotionController>();

        if (perceptionPoint == null)
            perceptionPoint = transform;
    }
}
```

### Validation Checklist

- [ ] `EnemyPerception` compiles and attaches to enemy prefab
- [ ] Sight checks work (angle + range + line of sight)
- [ ] Hearing checks work (range + noise level)
- [ ] Target memory persists for configured duration
- [ ] Primary target selection works
- [ ] Controller context is updated
- [ ] `PlayerPerceivable` works on player
- [ ] Gizmos render correctly

### What "Done" Looks Like

Enemies can detect the player through sight and hearing. States query `EnemyPerception` for target information rather than embedding their own raycasts. The perception system respects archetype configuration.

---

## Phase 6 — Core AI States

### Goal
Implement the fundamental enemy states using the generic state machine.

### What Already Exists
- `StateMachine<TState, TContext>` from Phase 1
- `EnemyContext` with all needed data
- `EnemyPerception` for target queries

### What Will Be Added

```
Scripts/AI/Enemy/States/
├── EnemyInactiveState.cs
├── EnemyIdleState.cs
├── EnemyPatrolState.cs
├── EnemyChaseState.cs
├── EnemyAttackState.cs
├── EnemyHurtState.cs
└── EnemyDeadState.cs
```

### What is Explicitly NOT Being Changed
- States do NOT deal damage directly
- States do NOT perform raycasts (query perception)
- State machine infrastructure unchanged

### Concrete Implementation Steps

#### Step 6.1: Create EnemyInactiveState

```csharp
// Scripts/AI/Enemy/States/EnemyInactiveState.cs

/// <summary>
/// Enemy is inactive/disabled.
/// </summary>
public class EnemyInactiveState : BaseState<EnemyContext>
{
    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.enabled = false;
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.enabled = true;
        ctx.NavAgent.isStopped = false;
    }
}
```

#### Step 6.2: Create EnemyIdleState

```csharp
// Scripts/AI/Enemy/States/EnemyIdleState.cs
using UnityEngine;

/// <summary>
/// Enemy is idle, no target.
/// Will transition to Patrol or Chase based on archetype and perception.
/// </summary>
public class EnemyIdleState : BaseState<EnemyContext>
{
    private float _idleTimer;
    private const float IdleBeforePatrol = 3f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.ResetPath();
        _idleTimer = 0f;

        // Optional: Play idle animation
        ctx.Animator?.SetBool("IsMoving", false);
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for target
        if (ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Transition to patrol after idle time
        if (ctx.Archetype.CanPatrol)
        {
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= IdleBeforePatrol)
            {
                ctx.Controller.RequestStateChange(EnemyState.Patrol);
            }
        }
    }
}
```

#### Step 6.3: Create EnemyPatrolState

```csharp
// Scripts/AI/Enemy/States/EnemyPatrolState.cs
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy patrols randomly or along waypoints.
/// </summary>
public class EnemyPatrolState : BaseState<EnemyContext>
{
    private const float WaypointReachedDistance = 1.5f;
    private const float PatrolRadius = 15f;
    private const float MaxPatrolTime = 10f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * 0.6f; // Slower patrol speed

        ctx.Animator?.SetBool("IsMoving", true);

        // Pick random patrol destination
        PickNewPatrolPoint(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for target - higher priority
        if (ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Check if reached destination
        if (!ctx.NavAgent.pathPending && ctx.NavAgent.remainingDistance <= WaypointReachedDistance)
        {
            // Go back to idle briefly, then patrol again
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Timeout - pick new point or go idle
        if (ctx.TimeSinceStateEnter > MaxPatrolTime)
        {
            PickNewPatrolPoint(ctx);
        }

        // Check if stuck
        CheckIfStuck(ctx);
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;
    }

    private void PickNewPatrolPoint(EnemyContext ctx)
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;
        randomDirection += ctx.Transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, PatrolRadius, NavMesh.AllAreas))
        {
            ctx.PatrolDestination = hit.position;
            ctx.HasPatrolDestination = true;
            ctx.NavAgent.SetDestination(hit.position);
        }
    }

    private void CheckIfStuck(EnemyContext ctx)
    {
        float distanceMoved = Vector3.Distance(ctx.Transform.position, ctx.LastPosition);
        ctx.LastPosition = ctx.Transform.position;

        if (distanceMoved < 0.1f)
        {
            ctx.StuckTimer += Time.deltaTime;
            if (ctx.StuckTimer > 2f)
            {
                ctx.StuckTimer = 0f;
                PickNewPatrolPoint(ctx);
            }
        }
        else
        {
            ctx.StuckTimer = 0f;
        }
    }
}
```

#### Step 6.4: Create EnemyChaseState

```csharp
// Scripts/AI/Enemy/States/EnemyChaseState.cs
using UnityEngine;

/// <summary>
/// Enemy chases the current target.
/// </summary>
public class EnemyChaseState : BaseState<EnemyContext>
{
    private float _pathUpdateTimer;
    private const float PathUpdateInterval = 0.25f;
    private const float GiveUpTime = 8f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;

        ctx.Animator?.SetBool("IsMoving", true);

        // Initial path to target
        UpdatePath(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        // Lost target - check memory
        if (!ctx.HasTarget)
        {
            // Go to last known position or give up
            if (ctx.TimeSinceTargetSeen > ctx.Archetype.MemoryDuration)
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
                return;
            }
        }

        // Check if in attack range
        if (ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
        {
            ctx.Controller.RequestStateChange(EnemyState.Attack);
            return;
        }

        // Update path periodically
        _pathUpdateTimer += Time.deltaTime;
        if (_pathUpdateTimer >= PathUpdateInterval)
        {
            _pathUpdateTimer = 0f;
            UpdatePath(ctx);
        }

        // Face target while moving
        if (ctx.HasTarget)
        {
            Vector3 lookDir = ctx.GetDirectionToTarget();
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                ctx.Transform.rotation = Quaternion.Slerp(
                    ctx.Transform.rotation,
                    targetRot,
                    ctx.Archetype.TurnSpeed * Time.deltaTime * Mathf.Deg2Rad
                );
            }
        }

        // Give up if chasing too long without progress
        if (ctx.TimeSinceStateEnter > GiveUpTime && ctx.GetDistanceToTarget() > ctx.Archetype.SightRange)
        {
            ctx.CurrentTarget = null;
            ctx.Controller.RequestStateChange(EnemyState.Idle);
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.Animator?.SetBool("IsMoving", false);
    }

    private void UpdatePath(EnemyContext ctx)
    {
        Vector3 destination = ctx.HasTarget
            ? ctx.CurrentTarget.position
            : ctx.LastKnownTargetPosition;

        ctx.NavAgent.SetDestination(destination);
    }
}
```

#### Step 6.5: Create EnemyAttackState

```csharp
// Scripts/AI/Enemy/States/EnemyAttackState.cs
using UnityEngine;

/// <summary>
/// Enemy executes an attack ability.
/// Does NOT deal damage directly - triggers animation/ability system.
/// </summary>
public class EnemyAttackState : BaseState<EnemyContext>
{
    private enum AttackPhase { Windup, Execute, Recovery }
    private AttackPhase _currentPhase;
    private float _phaseTimer;
    private EnemyAbilityData _currentAbility;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;

        // Select ability
        _currentAbility = ctx.Archetype.PrimaryAbility;
        if (_currentAbility == null)
        {
            Debug.LogWarning($"[EnemyAttackState] No primary ability for {ctx.Archetype.DisplayName}");
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Start windup
        _currentPhase = AttackPhase.Windup;
        _phaseTimer = 0f;
        ctx.IsAbilityInProgress = true;
        ctx.AbilityStartTime = Time.time;

        // Trigger animation
        if (!string.IsNullOrEmpty(_currentAbility.AnimationTrigger))
        {
            ctx.Animator?.SetTrigger(_currentAbility.AnimationTrigger);
        }

        Debug.Log($"[EnemyAttackState] Starting attack: {_currentAbility.DisplayName}");
    }

    public override void Update(EnemyContext ctx)
    {
        _phaseTimer += Time.deltaTime;

        switch (_currentPhase)
        {
            case AttackPhase.Windup:
                UpdateWindup(ctx);
                break;
            case AttackPhase.Execute:
                UpdateExecute(ctx);
                break;
            case AttackPhase.Recovery:
                UpdateRecovery(ctx);
                break;
        }
    }

    private void UpdateWindup(EnemyContext ctx)
    {
        // Track target during windup if allowed
        if (_currentAbility.TrackTargetDuringWindup && ctx.HasTarget)
        {
            FaceTarget(ctx);
        }

        // Transition to execute
        if (_phaseTimer >= _currentAbility.WindupTime)
        {
            _currentPhase = AttackPhase.Execute;
            _phaseTimer = 0f;

            // Actual damage is dealt via animation event or ability executor
            // This state just manages timing
            ctx.Controller.GetComponent<EnemyAbilityExecutor>()?.ExecuteAbility(_currentAbility);
        }
    }

    private void UpdateExecute(EnemyContext ctx)
    {
        // Brief execution window (damage happens via ability executor)
        if (_phaseTimer >= 0.1f)
        {
            _currentPhase = AttackPhase.Recovery;
            _phaseTimer = 0f;
            ctx.LastAttackTime = Time.time;
        }
    }

    private void UpdateRecovery(EnemyContext ctx)
    {
        if (_phaseTimer >= _currentAbility.RecoveryTime)
        {
            // Attack complete - decide next action
            if (ctx.HasTarget && ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
            {
                // Attack again
                ctx.Controller.RequestStateChange(EnemyState.Attack);
            }
            else if (ctx.HasTarget)
            {
                // Chase to close distance
                ctx.Controller.RequestStateChange(EnemyState.Chase);
            }
            else
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
            }
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.IsAbilityInProgress = false;
        ctx.NavAgent.isStopped = false;
    }

    private void FaceTarget(EnemyContext ctx)
    {
        if (!ctx.HasTarget) return;

        Vector3 lookDir = ctx.GetDirectionToTarget();
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            ctx.Transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}
```

#### Step 6.6: Create EnemyHurtState

```csharp
// Scripts/AI/Enemy/States/EnemyHurtState.cs
using UnityEngine;

/// <summary>
/// Enemy reacts to being hit (stagger).
/// </summary>
public class EnemyHurtState : BaseState<EnemyContext>
{
    private const float StaggerDuration = 0.5f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;

        // Interrupt any ability in progress
        ctx.IsAbilityInProgress = false;

        // Play hurt animation
        ctx.Animator?.SetTrigger("Hurt");

        Debug.Log($"[EnemyHurtState] {ctx.Controller.name} staggered");
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for death
        if (!ctx.Health.IsAlive())
        {
            ctx.Controller.ForceState(EnemyState.Dead);
            return;
        }

        // Check for flee
        if (ctx.Archetype.CanFlee)
        {
            float healthPercent = ctx.Health.GetHealthPercentage() / 100f;
            if (healthPercent <= ctx.Archetype.FleeHealthThreshold)
            {
                ctx.Controller.RequestStateChange(EnemyState.Flee);
                return;
            }
        }

        // Recovery
        if (ctx.TimeSinceStateEnter >= StaggerDuration)
        {
            if (ctx.HasTarget)
            {
                ctx.Controller.RequestStateChange(EnemyState.Chase);
            }
            else
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
            }
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
    }
}
```

#### Step 6.7: Create EnemyDeadState

```csharp
// Scripts/AI/Enemy/States/EnemyDeadState.cs
using UnityEngine;

/// <summary>
/// Enemy death handling.
/// </summary>
public class EnemyDeadState : BaseState<EnemyContext>
{
    private const float DeathAnimationDuration = 2f;
    private const float DestroyDelay = 5f;

    public override void Enter(EnemyContext ctx)
    {
        // Stop all movement
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.enabled = false;

        // Clear target
        ctx.CurrentTarget = null;

        // Play death animation
        ctx.Animator?.SetTrigger("Die");

        // Disable colliders (optional - depends on ragdoll setup)
        var colliders = ctx.Controller.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
                col.enabled = false;
        }

        Debug.Log($"[EnemyDeadState] {ctx.Controller.name} died");

        // Schedule destruction
        ctx.Controller.StartCoroutine(DestroyAfterDelay(ctx));
    }

    private System.Collections.IEnumerator DestroyAfterDelay(EnemyContext ctx)
    {
        yield return new WaitForSeconds(DestroyDelay);

        if (ctx.Controller != null)
        {
            // Unregister from encounter director
            EncounterDirector.Instance?.UnregisterEnemy(ctx.Controller);

            // Destroy
            Object.Destroy(ctx.Controller.gameObject);
        }
    }
}
```

### Validation Checklist

- [ ] All state classes compile
- [ ] Idle state transitions to Patrol/Chase appropriately
- [ ] Patrol state picks random NavMesh points
- [ ] Chase state updates path and transitions to Attack
- [ ] Attack state manages windup/execute/recovery phases
- [ ] Hurt state staggers enemy briefly
- [ ] Dead state disables enemy and schedules destruction
- [ ] States do NOT deal damage directly

### What "Done" Looks Like

Enemies have full lifecycle: spawn → idle → patrol → detect player → chase → attack → hurt → die. All states are data-driven and don't contain hardcoded damage values.

---

## Phase 7 — Combat Abilities & Damage Flow

### Goal
Create the ability execution system that connects AI states to actual damage dealing.

### What Already Exists
- `WeaponHitbox` for trigger-based hit detection
- `RangedWeapon` raycast pattern
- `IDamageable.TakeDamage()` interface
- `HitEffectReceiver` for VFX/SFX

### What Will Be Added

```
Scripts/AI/Enemy/Combat/
├── EnemyAbilityExecutor.cs    → Executes abilities
├── EnemyWeaponHitbox.cs       → Reuses WeaponHitbox pattern
└── EnemyProjectile.cs         → For ranged enemies
```

### What is Explicitly NOT Being Changed
- `IDamageable` interface
- `HealthComponent` implementation
- Existing weapon hit detection patterns

### Concrete Implementation Steps

#### Step 7.1: Create EnemyAbilityExecutor

```csharp
// Scripts/AI/Enemy/Combat/EnemyAbilityExecutor.cs
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Executes enemy abilities, handling damage, VFX, and SFX.
/// Called by attack states - never by AI decision logic.
/// </summary>
public class EnemyAbilityExecutor : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("References")]
    [Tooltip("Transform for melee attack origin")]
    [SerializeField] private Transform meleeOrigin;

    [BoxGroup("References")]
    [Tooltip("Transform for ranged attack origin")]
    [SerializeField] private Transform rangedOrigin;

    [BoxGroup("Melee Settings")]
    [SerializeField] private LayerMask meleeHitLayers;

    [BoxGroup("Ranged Settings")]
    [SerializeField] private LayerMask rangedHitLayers;

    [BoxGroup("Ranged Settings")]
    [SerializeField] private GameObject projectilePrefab;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<IDamageable> hitThisAttack = new();

    private AudioSource _audioSource;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (meleeOrigin == null)
            meleeOrigin = transform;

        if (rangedOrigin == null)
            rangedOrigin = transform;
    }

    /// <summary>
    /// Execute an ability. Called by EnemyAttackState.
    /// </summary>
    public void ExecuteAbility(EnemyAbilityData ability)
    {
        if (ability == null)
        {
            Debug.LogWarning("[EnemyAbilityExecutor] Null ability");
            return;
        }

        hitThisAttack.Clear();

        // Determine ability type based on range
        if (ability.MaxRange <= 3f)
        {
            ExecuteMeleeAbility(ability);
        }
        else
        {
            ExecuteRangedAbility(ability);
        }

        // Play SFX
        if (ability.SfxClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(ability.SfxClip);
        }

        // Spawn VFX
        if (ability.VfxPrefab != null)
        {
            SpawnVFX(ability.VfxPrefab, meleeOrigin.position, meleeOrigin.rotation);
        }
    }

    private void ExecuteMeleeAbility(EnemyAbilityData ability)
    {
        // Sphere overlap for melee hit detection
        Vector3 origin = meleeOrigin.position + meleeOrigin.forward * (ability.MaxRange / 2f);
        float radius = ability.MaxRange / 2f;

        Collider[] hits = Physics.OverlapSphere(origin, radius, meleeHitLayers);

        foreach (var hit in hits)
        {
            // Skip self
            if (hit.transform.root == transform.root)
                continue;

            ProcessHit(hit.gameObject, ability, hit.ClosestPoint(origin));
        }
    }

    private void ExecuteRangedAbility(EnemyAbilityData ability)
    {
        if (projectilePrefab != null)
        {
            // Spawn projectile
            SpawnProjectile(ability);
        }
        else
        {
            // Instant raycast
            ExecuteRaycastAbility(ability);
        }
    }

    private void ExecuteRaycastAbility(EnemyAbilityData ability)
    {
        Vector3 origin = rangedOrigin.position;
        Vector3 direction = rangedOrigin.forward;

        // Aim at target if available
        if (controller.Context?.CurrentTarget != null)
        {
            direction = (controller.Context.CurrentTarget.position - origin).normalized;
        }

        if (Physics.Raycast(origin, direction, out RaycastHit hit, ability.MaxRange, rangedHitLayers))
        {
            ProcessHit(hit.collider.gameObject, ability, hit.point);

            // Spawn impact VFX
            if (ability.VfxPrefab != null)
            {
                SpawnVFX(ability.VfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }
    }

    private void SpawnProjectile(EnemyAbilityData ability)
    {
        Vector3 origin = rangedOrigin.position;
        Vector3 direction = rangedOrigin.forward;

        // Aim at target
        if (controller.Context?.CurrentTarget != null)
        {
            direction = (controller.Context.CurrentTarget.position - origin).normalized;
        }

        GameObject projectileObj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));

        var projectile = projectileObj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(
                ability.BaseDamage,
                ability.DamageType,
                gameObject,
                rangedHitLayers
            );
        }
    }

    private void ProcessHit(GameObject hitObject, EnemyAbilityData ability, Vector3 hitPoint)
    {
        IDamageable damageable = FindDamageable(hitObject);
        if (damageable == null)
            return;

        // Prevent hitting same target twice
        if (hitThisAttack.Contains(damageable))
            return;

        hitThisAttack.Add(damageable);

        // Apply damage through IDamageable (existing system)
        float damage = ability.BaseDamage;

        // Apply archetype damage resistance (if target has one)
        // Note: This uses the EXISTING TakeDamage interface unchanged
        damageable.TakeDamage(damage, ability.DamageType, gameObject);

        // Hit effects
        var hitEffectReceiver = hitObject.GetComponentInParent<HitEffectReceiver>();
        hitEffectReceiver?.PlayHitEffect(hitPoint, (hitPoint - transform.position).normalized);

        Debug.Log($"[EnemyAbilityExecutor] Hit {hitObject.name} for {damage} damage");
    }

    private IDamageable FindDamageable(GameObject obj)
    {
        // Check self, parent, root (same pattern as PlayerCombatManager)
        var damageable = obj.GetComponent<IDamageable>();
        if (damageable != null) return damageable;

        if (obj.transform.parent != null)
        {
            damageable = obj.transform.parent.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        if (obj.transform.root != null && obj.transform.root != obj.transform)
        {
            damageable = obj.transform.root.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        return null;
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var vfx = Instantiate(prefab, position, rotation);
        Destroy(vfx, 3f); // Auto-cleanup
    }

    /// <summary>
    /// Called by animation events for melee attacks with specific timing.
    /// </summary>
    public void AnimationEvent_MeleeHit()
    {
        var ability = controller.Archetype?.PrimaryAbility;
        if (ability != null)
        {
            ExecuteMeleeAbility(ability);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (meleeOrigin == null) return;

        var ability = controller?.Archetype?.PrimaryAbility;
        float range = ability?.MaxRange ?? 2f;

        Gizmos.color = Color.red;
        Vector3 origin = meleeOrigin.position + meleeOrigin.forward * (range / 2f);
        Gizmos.DrawWireSphere(origin, range / 2f);
    }
#endif
}
```

#### Step 7.2: Create EnemyProjectile

```csharp
// Scripts/AI/Enemy/Combat/EnemyProjectile.cs
using UnityEngine;

/// <summary>
/// Projectile fired by ranged enemies.
/// Handles movement, collision, and damage application.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private float _damage;
    private DamageType _damageType;
    private GameObject _source;
    private LayerMask _hitLayers;
    private Rigidbody _rb;
    private bool _hasHit;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = false;
    }

    public void Initialize(float damage, DamageType damageType, GameObject source, LayerMask hitLayers)
    {
        _damage = damage;
        _damageType = damageType;
        _source = source;
        _hitLayers = hitLayers;

        // Set velocity
        _rb.velocity = transform.forward * speed;

        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // Check layer
        if ((_hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Don't hit source
        if (other.transform.root == _source?.transform.root)
            return;

        _hasHit = true;

        // Apply damage
        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage, _damageType, _source);
        }

        // Hit effects
        var hitReceiver = other.GetComponentInParent<HitEffectReceiver>();
        hitReceiver?.PlayHitEffect(transform.position, -transform.forward);

        // Impact VFX
        if (impactVfxPrefab != null)
        {
            var vfx = Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // Impact SFX
        if (impactSfx != null)
        {
            AudioSource.PlayClipAtPoint(impactSfx, transform.position);
        }

        Destroy(gameObject);
    }
}
```

#### Step 7.3: Hook Damage Reception to Hurt State

Add a damage listener to trigger the Hurt state:

```csharp
// Add to EnemyController.cs Initialize() method, after health subscription:

// In Initialize(), add this:
// Subscribe to damage for hurt state trigger
_health.OnHealthChanged += HandleHealthChanged;

// Add this method:
private void HandleHealthChanged(int current, int max)
{
    // Only trigger hurt if not already hurting or dead
    if (currentState != EnemyState.Hurt &&
        currentState != EnemyState.Dead &&
        current < max) // Actually took damage
    {
        // Small chance to stagger (prevents constant interruption)
        if (Random.value < 0.3f)
        {
            RequestStateChange(EnemyState.Hurt);
        }
    }
}
```

### Validation Checklist

- [ ] `EnemyAbilityExecutor` compiles and attaches to enemy
- [ ] Melee abilities use sphere overlap (no embedded raycasts in states)
- [ ] Ranged abilities use raycast OR projectile
- [ ] Damage flows through `IDamageable.TakeDamage()` unchanged
- [ ] Hit effects trigger via `HitEffectReceiver`
- [ ] Animation events can trigger damage
- [ ] Projectiles handle collision correctly

### What "Done" Looks Like

Enemies can deal damage to the player through the existing damage system. AI states request attacks, the ability executor handles the actual damage application. No new damage interfaces.

---

## Phase 8 — Melee Enemies

### Goal
Create a complete melee enemy using all systems built so far.

### What Already Exists
All required systems from previous phases.

### What Will Be Added
- Example `EnemyArchetype` asset for a melee enemy
- Example prefab setup
- Animation event integration

### What is Explicitly NOT Being Changed
- No new code (using existing systems)
- Configuration only

### Concrete Implementation Steps

#### Step 8.1: Create Melee Archetype Asset

1. Right-click in Project: Create → StillOrbit → Enemy → Archetype
2. Name: `Archetype_MeleeGrunt`
3. Configure:

```
Identity:
  Archetype ID: melee_grunt
  Display Name: Grunt
  Description: Basic melee attacker

Stats:
  Max Health: 50
  Damage Type: Flesh
  Damage Resistance: 1.0

Movement:
  Movement Type: Ground
  Move Speed: 5
  Turn Speed: 180

Combat:
  Combat Style: Melee
  Preferred Combat Range: 1.5
  Attack Range: 2.0

Perception:
  Sight Range: 15
  Sight Angle: 120
  Hearing Range: 20
  Memory Duration: 5

Behavior:
  Can Patrol: true
  Can Flee: false
```

#### Step 8.2: Create Melee Ability Asset

1. Right-click: Create → StillOrbit → Enemy → Ability
2. Name: `Ability_MeleeSwipe`
3. Configure:

```
Identity:
  Ability ID: melee_swipe
  Display Name: Swipe

Timing:
  Cooldown: 1.5
  Windup Time: 0.3
  Recovery Time: 0.5

Range:
  Min Range: 0
  Max Range: 2.5

Damage:
  Base Damage: 15
  Damage Type: Generic

Animation:
  Animation Trigger: Attack
  Animation State Name: Attack

Behavior:
  Can Be Interrupted: true
  Track Target During Windup: true
```

#### Step 8.3: Create Enemy Prefab

1. Create empty GameObject: `Enemy_MeleeGrunt`
2. Add components:
   - `NavMeshAgent`
   - `HealthComponent`
   - `EnemyController`
   - `EnemyPerception`
   - `EnemyAbilityExecutor`
   - `HitEffectReceiver`
   - `Capsule Collider` (for physics)
   - `Rigidbody` (kinematic)
   - `Animator`

3. Child structure:
   ```
   Enemy_MeleeGrunt
   ├── Visual (model, animator)
   ├── EyePoint (empty, positioned at head)
   └── AttackOrigin (empty, positioned at hand)
   ```

4. Configure:
   - Assign `Archetype_MeleeGrunt` to EnemyController
   - Set EyePoint as perception eye
   - Set AttackOrigin as melee origin
   - Configure layer masks

#### Step 8.4: Animation Setup

Create Animator Controller with:

```
Parameters:
  - IsMoving (bool)
  - Attack (trigger)
  - Hurt (trigger)
  - Die (trigger)

States:
  - Idle (default)
  - Walk (blend tree or single clip)
  - Attack
  - Hurt
  - Death

Transitions:
  Idle ↔ Walk: IsMoving
  Any → Attack: Attack trigger
  Any → Hurt: Hurt trigger
  Any → Death: Die trigger
```

Add Animation Event on Attack clip at damage frame:
- Function: `AnimationEvent_MeleeHit`

### Validation Checklist

- [ ] Archetype asset created with all values
- [ ] Ability asset created and assigned to archetype
- [ ] Prefab has all required components
- [ ] NavMeshAgent configured
- [ ] Animator has all required parameters
- [ ] Animation events call `AnimationEvent_MeleeHit`
- [ ] Spawn via EncounterDirector works
- [ ] Enemy patrols, detects player, chases, attacks
- [ ] Player takes damage when hit
- [ ] Enemy takes damage and dies

### What "Done" Looks Like

A fully functional melee enemy that patrols, detects the player, chases, attacks, and dies. All through configuration, no custom code.

---

## Phase 9 — Ranged Enemies

### Goal
Create a ranged enemy variant using existing systems.

### What Already Exists
All systems from previous phases, plus `EnemyProjectile`.

### What Will Be Added
- Ranged archetype asset
- Ranged ability asset
- Positioning state for ranged combat

### What is Explicitly NOT Being Changed
- Core state machine
- Damage flow

### Concrete Implementation Steps

#### Step 9.1: Add Positioning State

```csharp
// Scripts/AI/Enemy/States/EnemyPositioningState.cs
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ranged enemy positioning - maintains preferred distance.
/// </summary>
public class EnemyPositioningState : BaseState<EnemyContext>
{
    private float _repositionTimer;
    private const float RepositionInterval = 1f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.Animator?.SetBool("IsMoving", true);
        FindPositioningPoint(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        if (!ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        float distance = ctx.GetDistanceToTarget();
        float preferred = ctx.Archetype.PreferredCombatRange;

        // Check if can attack from current position
        bool inAttackRange = distance >= ctx.Archetype.PrimaryAbility.MinRange &&
                            distance <= ctx.Archetype.PrimaryAbility.MaxRange;

        if (inAttackRange && ctx.CanUsePrimaryAbility() && HasLineOfSight(ctx))
        {
            ctx.Controller.RequestStateChange(EnemyState.Attack);
            return;
        }

        // Reposition periodically
        _repositionTimer += Time.deltaTime;
        if (_repositionTimer >= RepositionInterval)
        {
            _repositionTimer = 0f;
            FindPositioningPoint(ctx);
        }

        // Face target
        FaceTarget(ctx);
    }

    private void FindPositioningPoint(EnemyContext ctx)
    {
        if (!ctx.HasTarget) return;

        Vector3 targetPos = ctx.CurrentTarget.position;
        Vector3 directionFromTarget = (ctx.Transform.position - targetPos).normalized;
        float preferredDist = ctx.Archetype.PreferredCombatRange;

        // Try to maintain preferred distance
        Vector3 idealPos = targetPos + directionFromTarget * preferredDist;

        // Add some lateral offset for variety
        Vector3 lateral = Vector3.Cross(directionFromTarget, Vector3.up) * Random.Range(-3f, 3f);
        idealPos += lateral;

        if (NavMesh.SamplePosition(idealPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            ctx.NavAgent.SetDestination(hit.position);
        }
    }

    private bool HasLineOfSight(EnemyContext ctx)
    {
        if (!ctx.HasTarget) return false;

        Vector3 origin = ctx.Transform.position + Vector3.up;
        Vector3 direction = ctx.CurrentTarget.position - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude))
        {
            return hit.transform.root == ctx.CurrentTarget.root;
        }

        return true;
    }

    private void FaceTarget(EnemyContext ctx)
    {
        if (!ctx.HasTarget) return;

        Vector3 lookDir = ctx.GetDirectionToTarget();
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            ctx.Transform.rotation = Quaternion.Slerp(
                ctx.Transform.rotation,
                targetRot,
                ctx.Archetype.TurnSpeed * Time.deltaTime * Mathf.Deg2Rad
            );
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.Animator?.SetBool("IsMoving", false);
    }
}
```

#### Step 9.2: Update State Transitions

Add to `EnemyController.RegisterStateTransitions()`:

```csharp
// Add Positioning state registration in InitializeStateMachine():
_stateMachine.RegisterState(EnemyState.Positioning, new EnemyPositioningState());

// Update Chase transitions:
_stateMachine.RegisterTransitions(EnemyState.Chase,
    EnemyState.Idle,
    EnemyState.Attack,
    EnemyState.Positioning, // Added
    EnemyState.Hurt,
    EnemyState.Dead);

// Add Positioning transitions:
_stateMachine.RegisterTransitions(EnemyState.Positioning,
    EnemyState.Idle,
    EnemyState.Attack,
    EnemyState.Chase,
    EnemyState.Hurt,
    EnemyState.Dead);
```

#### Step 9.3: Update Chase State for Ranged

Modify `EnemyChaseState.Update()` to handle ranged combat style:

```csharp
// In Update(), before attack range check:
if (ctx.Archetype.CombatStyle == EnemyCombatStyle.Ranged)
{
    // Ranged enemies use positioning state instead
    if (ctx.GetDistanceToTarget() <= ctx.Archetype.SightRange)
    {
        ctx.Controller.RequestStateChange(EnemyState.Positioning);
        return;
    }
}
```

#### Step 9.4: Create Ranged Archetype

```
Identity:
  Archetype ID: ranged_shooter
  Display Name: Shooter

Stats:
  Max Health: 30

Movement:
  Move Speed: 3.5

Combat:
  Combat Style: Ranged
  Preferred Combat Range: 12
  Attack Range: 15

Perception:
  Sight Range: 25
```

#### Step 9.5: Create Ranged Ability

```
Identity:
  Ability ID: ranged_shot
  Display Name: Plasma Shot

Timing:
  Cooldown: 2.0
  Windup Time: 0.5
  Recovery Time: 0.3

Range:
  Min Range: 5
  Max Range: 20

Damage:
  Base Damage: 20
  Damage Type: Energy (add to enum if needed)
```

### Validation Checklist

- [ ] Positioning state compiles
- [ ] Ranged enemy maintains distance
- [ ] Line of sight check works
- [ ] Projectile spawns and travels
- [ ] Projectile damages player on hit
- [ ] Enemy repositions when player closes distance

### What "Done" Looks Like

A ranged enemy that maintains preferred distance, positions for line of sight, and fires projectiles at the player. Uses the same core systems as melee enemies.

---

## Phase 10 — Flying & Special Movement

### Goal
Support flying enemies that ignore ground NavMesh constraints.

### What Already Exists
- `EnemyMovementType.Flying` in archetype
- Ground-based states

### What Will Be Added

```
Scripts/AI/Enemy/Movement/
└── EnemyFlyingMovement.cs
```

### What is Explicitly NOT Being Changed
- Ground enemy behavior
- State machine structure

### Concrete Implementation Steps

#### Step 10.1: Create EnemyFlyingMovement

```csharp
// Scripts/AI/Enemy/Movement/EnemyFlyingMovement.cs
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles flying movement for airborne enemies.
/// Replaces NavMeshAgent movement when MovementType is Flying.
/// </summary>
public class EnemyFlyingMovement : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("Settings")]
    [SerializeField] private float hoverHeight = 3f;

    [BoxGroup("Settings")]
    [SerializeField] private float hoverVariation = 0.5f;

    [BoxGroup("Settings")]
    [SerializeField] private float hoverSpeed = 2f;

    [BoxGroup("Settings")]
    [SerializeField] private float bankAngle = 15f;

    [BoxGroup("Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;

    [BoxGroup("Avoidance")]
    [SerializeField] private float avoidanceDistance = 3f;

    private Vector3 _targetPosition;
    private float _currentHoverOffset;
    private float _hoverPhase;

    public bool HasReachedDestination { get; private set; }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        _hoverPhase = Random.value * Mathf.PI * 2f; // Random start phase
    }

    private void Update()
    {
        if (!controller.IsInitialized)
            return;

        if (controller.Archetype.MovementType != EnemyMovementType.Flying)
            return;

        UpdateHover();
        UpdateMovement();
        UpdateRotation();
    }

    /// <summary>
    /// Set movement destination.
    /// </summary>
    public void SetDestination(Vector3 destination)
    {
        // Adjust for flying height
        _targetPosition = destination + Vector3.up * hoverHeight;
        HasReachedDestination = false;
    }

    /// <summary>
    /// Stop movement.
    /// </summary>
    public void Stop()
    {
        _targetPosition = transform.position;
        HasReachedDestination = true;
    }

    private void UpdateHover()
    {
        // Sine wave hover
        _hoverPhase += Time.deltaTime * hoverSpeed;
        _currentHoverOffset = Mathf.Sin(_hoverPhase) * hoverVariation;
    }

    private void UpdateMovement()
    {
        Vector3 targetWithHover = _targetPosition + Vector3.up * _currentHoverOffset;
        Vector3 direction = targetWithHover - transform.position;
        float distance = direction.magnitude;

        if (distance < 0.5f)
        {
            HasReachedDestination = true;
            return;
        }

        // Obstacle avoidance
        Vector3 avoidance = CalculateAvoidance();
        direction = (direction.normalized + avoidance).normalized;

        // Move
        float speed = controller.Archetype.MoveSpeed;
        transform.position += direction * speed * Time.deltaTime;
    }

    private void UpdateRotation()
    {
        Vector3 velocity = (_targetPosition - transform.position).normalized;

        if (velocity.sqrMagnitude > 0.01f)
        {
            // Face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(velocity);

            // Add banking based on lateral movement
            float lateralSpeed = Vector3.Dot(velocity, transform.right);
            Quaternion bankRotation = Quaternion.Euler(0, 0, -lateralSpeed * bankAngle);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation * bankRotation,
                controller.Archetype.TurnSpeed * Time.deltaTime * Mathf.Deg2Rad
            );
        }
    }

    private Vector3 CalculateAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        int rayCount = 8;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, avoidanceDistance, obstacleLayer))
            {
                float strength = 1f - (hit.distance / avoidanceDistance);
                avoidance -= direction * strength;
            }
        }

        // Vertical avoidance
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, hoverHeight + 1f, obstacleLayer))
        {
            if (groundHit.distance < hoverHeight)
            {
                avoidance += Vector3.up * (hoverHeight - groundHit.distance);
            }
        }

        return avoidance.normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, avoidanceDistance);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _targetPosition);
            Gizmos.DrawWireSphere(_targetPosition, 0.5f);
        }
    }
#endif
}
```

#### Step 10.2: Integrate with States

States need to check movement type and use appropriate system:

```csharp
// Add helper method to EnemyContext:
public void SetDestination(Vector3 destination)
{
    if (Archetype.MovementType == EnemyMovementType.Flying)
    {
        var flyingMovement = Controller.GetComponent<EnemyFlyingMovement>();
        flyingMovement?.SetDestination(destination);
    }
    else
    {
        NavAgent.SetDestination(destination);
    }
}

public void StopMovement()
{
    if (Archetype.MovementType == EnemyMovementType.Flying)
    {
        var flyingMovement = Controller.GetComponent<EnemyFlyingMovement>();
        flyingMovement?.Stop();
    }
    else
    {
        NavAgent.isStopped = true;
        NavAgent.ResetPath();
    }
}
```

#### Step 10.3: Disable NavMeshAgent for Flying

In `EnemyController.Initialize()`:

```csharp
// After NavMeshAgent configuration:
if (archetype.MovementType == EnemyMovementType.Flying)
{
    _navAgent.enabled = false;

    // Ensure flying movement component exists
    var flyingMovement = GetComponent<EnemyFlyingMovement>();
    if (flyingMovement == null)
    {
        flyingMovement = gameObject.AddComponent<EnemyFlyingMovement>();
    }
}
```

### Validation Checklist

- [ ] Flying movement compiles
- [ ] Flying enemies hover at configured height
- [ ] Obstacle avoidance works
- [ ] Banking rotation looks natural
- [ ] States use movement abstraction
- [ ] Ground enemies still use NavMesh

### What "Done" Looks Like

Flying enemies that hover, bank when turning, and avoid obstacles. The state machine works identically for ground and flying enemies.

---

## Phase 11 — Boss Phases

### Goal
Implement phase-driven boss behavior using existing systems.

### What Already Exists
- `BossPhase` struct in `EnemyArchetype`
- `isBoss` flag
- Ability system

### What Will Be Added
- Phase transition logic
- Phase-aware attack state

### What is Explicitly NOT Being Changed
- Boss uses same `EnemyController`
- Same state machine
- Same damage flow

### Concrete Implementation Steps

#### Step 11.1: Add Phase Management to EnemyContext

```csharp
// Add to EnemyContext.cs:

/// <summary>
/// Get the current boss phase data (null if not a boss or in phase 0).
/// </summary>
public BossPhase GetCurrentBossPhaseData()
{
    if (!Archetype.IsBoss || Archetype.BossPhases.Count == 0)
        return null;

    if (CurrentBossPhase <= 0 || CurrentBossPhase > Archetype.BossPhases.Count)
        return null;

    return Archetype.BossPhases[CurrentBossPhase - 1];
}

/// <summary>
/// Check if a phase transition should occur based on current health.
/// </summary>
public bool ShouldTransitionPhase(out int newPhase)
{
    newPhase = CurrentBossPhase;

    if (!Archetype.IsBoss)
        return false;

    float healthPercent = Health.GetHealthPercentage() / 100f;

    // Check each phase threshold
    for (int i = 0; i < Archetype.BossPhases.Count; i++)
    {
        var phase = Archetype.BossPhases[i];

        // Already past this phase
        if (i + 1 <= CurrentBossPhase)
            continue;

        // Health dropped below threshold
        if (healthPercent <= phase.HealthThreshold)
        {
            newPhase = i + 1;
            return true;
        }
    }

    return false;
}

/// <summary>
/// Get current phase abilities (or base abilities if no phase).
/// </summary>
public IReadOnlyList<EnemyAbilityData> GetCurrentAbilities()
{
    var phaseData = GetCurrentBossPhaseData();
    if (phaseData != null && phaseData.PhaseAbilities.Count > 0)
    {
        return phaseData.PhaseAbilities;
    }

    return Archetype.Abilities;
}

/// <summary>
/// Get damage multiplier for current phase.
/// </summary>
public float GetDamageMultiplier()
{
    var phaseData = GetCurrentBossPhaseData();
    return phaseData?.DamageMultiplier ?? 1f;
}

/// <summary>
/// Get speed multiplier for current phase.
/// </summary>
public float GetSpeedMultiplier()
{
    var phaseData = GetCurrentBossPhaseData();
    return phaseData?.SpeedMultiplier ?? 1f;
}
```

#### Step 11.2: Create BossPhaseTransitionState

```csharp
// Scripts/AI/Enemy/States/BossPhaseTransitionState.cs
using UnityEngine;

/// <summary>
/// Special state for boss phase transitions.
/// Handles invulnerability, animations, and arena effects.
/// </summary>
public class BossPhaseTransitionState : BaseState<EnemyContext>
{
    private const float TransitionDuration = 2f;
    private int _targetPhase;

    public override void Enter(EnemyContext ctx)
    {
        // Store target phase
        ctx.ShouldTransitionPhase(out _targetPhase);

        // Stop movement
        ctx.StopMovement();

        // Brief invulnerability
        ctx.Health.SetInvulnerable(true);

        // Play transition animation
        var phaseData = ctx.Archetype.BossPhases[_targetPhase - 1];
        if (!string.IsNullOrEmpty(phaseData.OnEnterTrigger))
        {
            ctx.Animator?.SetTrigger(phaseData.OnEnterTrigger);
        }

        Debug.Log($"[Boss] {ctx.Controller.name} entering phase {_targetPhase}: {phaseData.PhaseName}");

        // Broadcast phase change event
        ctx.Controller.OnBossPhaseChanged?.Invoke(_targetPhase, phaseData);
    }

    public override void Update(EnemyContext ctx)
    {
        if (ctx.TimeSinceStateEnter >= TransitionDuration)
        {
            // Apply phase changes
            ctx.CurrentBossPhase = _targetPhase;
            ctx.PhaseTransitionPending = false;

            // Update speed
            ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * ctx.GetSpeedMultiplier();

            // End invulnerability
            ctx.Health.SetInvulnerable(false);

            // Resume combat
            if (ctx.HasTarget)
            {
                ctx.Controller.RequestStateChange(EnemyState.Chase);
            }
            else
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
            }
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.Health.SetInvulnerable(false);
    }
}
```

#### Step 11.3: Add Phase Check to EnemyController

```csharp
// Add to EnemyController.cs:

// Add event:
public event Action<int, BossPhase> OnBossPhaseChanged;

// Add to Update():
private void Update()
{
    if (!isInitialized || currentState == EnemyState.Dead)
        return;

    // Check for boss phase transition
    if (archetype.IsBoss && currentState != EnemyState.BossPhaseTransition)
    {
        CheckBossPhaseTransition();
    }

    _stateMachine?.Update();
    UpdatePerception();
}

private void CheckBossPhaseTransition()
{
    if (_context.ShouldTransitionPhase(out int newPhase))
    {
        _context.PhaseTransitionPending = true;
        ForceState(EnemyState.BossPhaseTransition);
    }
}

// Add state registration in InitializeStateMachine():
_stateMachine.RegisterState(EnemyState.BossPhaseTransition, new BossPhaseTransitionState());

// Add transitions:
// From any combat state to phase transition
_stateMachine.RegisterTransition(EnemyState.Chase, EnemyState.BossPhaseTransition);
_stateMachine.RegisterTransition(EnemyState.Attack, EnemyState.BossPhaseTransition);
_stateMachine.RegisterTransition(EnemyState.Idle, EnemyState.BossPhaseTransition);

// From phase transition to combat
_stateMachine.RegisterTransition(EnemyState.BossPhaseTransition, EnemyState.Chase);
_stateMachine.RegisterTransition(EnemyState.BossPhaseTransition, EnemyState.Idle);
```

#### Step 11.4: Add to EnemyState Enum

```csharp
// Add to EnemyState.cs:

/// <summary>Boss transitioning between phases.</summary>
BossPhaseTransition
```

#### Step 11.5: Phase-Aware Ability Selection

Update `EnemyAbilityExecutor` to use phase abilities:

```csharp
// Modify ExecuteAbility to respect phase damage multiplier:
public void ExecuteAbility(EnemyAbilityData ability)
{
    // ... existing code ...

    // Apply phase damage multiplier
    float damageMultiplier = controller.Context?.GetDamageMultiplier() ?? 1f;
    float finalDamage = ability.BaseDamage * damageMultiplier;

    // Use finalDamage instead of ability.BaseDamage
}
```

#### Step 11.6: Create Example Boss Archetype

```
Identity:
  Archetype ID: boss_brute
  Display Name: The Brute
  Is Boss: true

Stats:
  Max Health: 500
  Damage Resistance: 0.8

Boss Phases:
  Phase 1:
    Health Threshold: 0.7
    Phase Name: "Enraged"
    Speed Multiplier: 1.3
    Damage Multiplier: 1.2
    On Enter Trigger: "PhaseTransition"

  Phase 2:
    Health Threshold: 0.3
    Phase Name: "Desperate"
    Speed Multiplier: 1.5
    Damage Multiplier: 1.5
    Phase Abilities: [special_slam_ability]
    On Enter Trigger: "PhaseTransition2"
```

### Validation Checklist

- [ ] Boss phase transition state compiles
- [ ] Phase transitions trigger at correct health thresholds
- [ ] Invulnerability during transitions
- [ ] Animation triggers fire
- [ ] Speed/damage multipliers apply
- [ ] Phase-specific abilities activate
- [ ] `OnBossPhaseChanged` event fires

### What "Done" Looks Like

Bosses that change behavior at health thresholds. Phase transitions include brief invulnerability and can trigger arena effects. New abilities unlock per phase. Same core systems as regular enemies.

---

## Phase 12 — Group & Swarm Logic

### Goal
Lightweight coordination for multiple enemies without complex planners.

### What Already Exists
- `EncounterDirector.ActiveEnemies` list

### What Will Be Added

```
Scripts/AI/Group/
├── EnemyGroup.cs              → Coordinates a group
└── GroupTactics.cs            → Tactical utilities
```

### What is Explicitly NOT Being Changed
- Individual enemy AI
- State machine
- Pathfinding

### Concrete Implementation Steps

#### Step 12.1: Create EnemyGroup

```csharp
// Scripts/AI/Group/EnemyGroup.cs
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Lightweight coordination for a group of enemies.
/// Handles attacker limiting, target sharing, and basic tactics.
/// </summary>
public class EnemyGroup : MonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("Maximum simultaneous attackers")]
    [SerializeField] private int maxAttackers = 3;

    [BoxGroup("Settings")]
    [Tooltip("Minimum space between attackers")]
    [SerializeField] private float attackerSpacing = 2f;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<EnemyController> members = new();

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<EnemyController> activeAttackers = new();

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private Transform sharedTarget;

    public IReadOnlyList<EnemyController> Members => members;
    public IReadOnlyList<EnemyController> ActiveAttackers => activeAttackers;
    public Transform SharedTarget => sharedTarget;
    public int MaxAttackers => maxAttackers;

    /// <summary>
    /// Add an enemy to this group.
    /// </summary>
    public void AddMember(EnemyController enemy)
    {
        if (enemy == null || members.Contains(enemy))
            return;

        members.Add(enemy);
        enemy.OnDeath += HandleMemberDeath;
        enemy.OnStateChanged += HandleMemberStateChanged;
    }

    /// <summary>
    /// Remove an enemy from this group.
    /// </summary>
    public void RemoveMember(EnemyController enemy)
    {
        if (enemy == null)
            return;

        members.Remove(enemy);
        activeAttackers.Remove(enemy);
        enemy.OnDeath -= HandleMemberDeath;
        enemy.OnStateChanged -= HandleMemberStateChanged;
    }

    /// <summary>
    /// Request permission to attack. Returns true if allowed.
    /// </summary>
    public bool RequestAttackSlot(EnemyController enemy)
    {
        if (!members.Contains(enemy))
            return true; // Not in group, allow

        // Clean up dead/invalid attackers
        activeAttackers.RemoveAll(e => e == null || !e.IsAlive);

        // Check slot availability
        if (activeAttackers.Count >= maxAttackers)
        {
            return false;
        }

        // Check spacing
        foreach (var attacker in activeAttackers)
        {
            if (attacker == null) continue;

            float distance = Vector3.Distance(
                enemy.transform.position,
                attacker.transform.position
            );

            if (distance < attackerSpacing)
            {
                return false;
            }
        }

        activeAttackers.Add(enemy);
        return true;
    }

    /// <summary>
    /// Release attack slot when done attacking.
    /// </summary>
    public void ReleaseAttackSlot(EnemyController enemy)
    {
        activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Share a target with all group members.
    /// </summary>
    public void ShareTarget(Transform target)
    {
        sharedTarget = target;

        foreach (var member in members)
        {
            if (member == null || !member.IsAlive)
                continue;

            if (member.Context != null && member.Context.CurrentTarget == null)
            {
                member.Context.CurrentTarget = target;

                // Alert perception
                var perception = member.GetComponent<EnemyPerception>();
                if (perception != null && target != null)
                {
                    perception.AlertToPosition(target.position, 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// Broadcast a signal to all members (e.g., boss calling adds).
    /// </summary>
    public void Broadcast(string signal)
    {
        foreach (var member in members)
        {
            if (member == null || !member.IsAlive)
                continue;

            member.ReceiveGroupSignal(signal);
        }
    }

    /// <summary>
    /// Get suggested flanking position for an enemy.
    /// </summary>
    public Vector3 GetFlankingPosition(EnemyController enemy, float radius)
    {
        if (sharedTarget == null)
            return enemy.transform.position;

        // Find unoccupied angle around target
        int memberIndex = members.IndexOf(enemy);
        int totalMembers = members.Count;

        float angleStep = 360f / totalMembers;
        float angle = angleStep * memberIndex;

        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
        return sharedTarget.position + offset;
    }

    private void HandleMemberDeath(EnemyController enemy)
    {
        RemoveMember(enemy);
    }

    private void HandleMemberStateChanged(EnemyState from, EnemyState to)
    {
        // Could add coordination logic here
    }

    private void OnDestroy()
    {
        foreach (var member in members.ToArray())
        {
            if (member != null)
            {
                member.OnDeath -= HandleMemberDeath;
                member.OnStateChanged -= HandleMemberStateChanged;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (sharedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(sharedTarget.position, 1f);

            foreach (var member in members)
            {
                if (member != null)
                {
                    Gizmos.color = activeAttackers.Contains(member) ? Color.red : Color.yellow;
                    Gizmos.DrawLine(member.transform.position, sharedTarget.position);
                }
            }
        }
    }
#endif
}
```

#### Step 12.2: Integrate with Attack State

Modify `EnemyAttackState` to request attack slots:

```csharp
// Add to EnemyAttackState.Enter():
public override void Enter(EnemyContext ctx)
{
    // Check attack slot
    var group = ctx.Controller.GetComponent<EnemyGroupMember>()?.Group;
    if (group != null && !group.RequestAttackSlot(ctx.Controller))
    {
        // No slot available, go back to positioning/chase
        ctx.Controller.RequestStateChange(EnemyState.Chase);
        return;
    }

    // ... existing enter logic ...
}

// Add to EnemyAttackState.Exit():
public override void Exit(EnemyContext ctx)
{
    // Release attack slot
    var group = ctx.Controller.GetComponent<EnemyGroupMember>()?.Group;
    group?.ReleaseAttackSlot(ctx.Controller);

    // ... existing exit logic ...
}
```

#### Step 12.3: Create EnemyGroupMember Component

```csharp
// Scripts/AI/Group/EnemyGroupMember.cs
using UnityEngine;

/// <summary>
/// Marks an enemy as part of a group.
/// </summary>
public class EnemyGroupMember : MonoBehaviour
{
    [SerializeField] private EnemyGroup group;

    public EnemyGroup Group => group;

    public void SetGroup(EnemyGroup newGroup)
    {
        // Leave old group
        group?.RemoveMember(GetComponent<EnemyController>());

        // Join new group
        group = newGroup;
        group?.AddMember(GetComponent<EnemyController>());
    }

    private void Start()
    {
        if (group != null)
        {
            group.AddMember(GetComponent<EnemyController>());
        }
    }

    private void OnDestroy()
    {
        group?.RemoveMember(GetComponent<EnemyController>());
    }
}
```

#### Step 12.4: Add Signal Reception to EnemyController

```csharp
// Add to EnemyController.cs:

/// <summary>
/// Receive a signal from group coordinator.
/// </summary>
public void ReceiveGroupSignal(string signal)
{
    switch (signal)
    {
        case "aggro":
            // Share aggro
            if (_context.CurrentTarget == null)
            {
                _context.CurrentTarget = PlayerManager.Instance?.transform;
                RequestStateChange(EnemyState.Chase);
            }
            break;

        case "retreat":
            RequestStateChange(EnemyState.Flee);
            break;

        case "scatter":
            // Pick random direction
            // Implementation depends on desired behavior
            break;

        default:
            Debug.Log($"[EnemyController] Unknown signal: {signal}");
            break;
    }
}
```

#### Step 12.5: Auto-Group in EncounterDirector

```csharp
// Add to EncounterDirector.cs SpawnEnemy():

private void SpawnEnemy(EnemyArchetype archetype, Vector3 position)
{
    // ... existing spawn code ...

    // Auto-add to encounter group
    if (_currentGroup == null)
    {
        var groupObj = new GameObject("EncounterGroup");
        _currentGroup = groupObj.AddComponent<EnemyGroup>();
    }

    var groupMember = enemyObj.AddComponent<EnemyGroupMember>();
    groupMember.SetGroup(_currentGroup);
}

// Add field:
private EnemyGroup _currentGroup;

// Clean up in EndEncounter():
if (_currentGroup != null)
{
    Destroy(_currentGroup.gameObject);
    _currentGroup = null;
}
```

### Validation Checklist

- [ ] `EnemyGroup` limits simultaneous attackers
- [ ] Spacing is enforced between attackers
- [ ] Target sharing works
- [ ] Flanking positions calculated correctly
- [ ] Signals broadcast to all members
- [ ] Attack slots released on state exit
- [ ] Groups clean up on encounter end

### What "Done" Looks Like

Multiple enemies coordinate without overwhelming the player. Only N enemies attack at once, others position for their turn. Target information shared across the group.

---

## Phase 13 — Integration Testing Scenarios

### Goal
Verify all systems work together correctly.

### Test Scenarios

#### Scenario 1: Basic Melee Combat
```
Setup:
1. Player in open area
2. Spawn single melee enemy 20m away

Expected:
1. Enemy patrols briefly
2. Detects player (enters sight range)
3. Transitions to Chase
4. Closes distance
5. Transitions to Attack at range
6. Player takes damage
7. Kill enemy
8. Enemy plays death animation
9. Enemy removed from director tracking

Verify:
- [ ] State transitions logged correctly
- [ ] Damage logged through IDamageable
- [ ] OnDeath event fires
- [ ] No console errors
```

#### Scenario 2: Ranged Combat with Positioning
```
Setup:
1. Player in area with cover
2. Spawn ranged enemy 30m away

Expected:
1. Enemy detects player
2. Moves to preferred range
3. Checks line of sight
4. Repositions if blocked
5. Fires projectile
6. Player takes damage

Verify:
- [ ] Enemy maintains preferred distance
- [ ] Repositions when player approaches
- [ ] Projectile travels correctly
- [ ] Hit effects play
```

#### Scenario 3: Group Combat
```
Setup:
1. Player in open area
2. Spawn 5 melee enemies

Expected:
1. Only maxAttackers (3) attack simultaneously
2. Others wait for slot
3. When attacker dies/retreats, new enemy takes slot
4. All enemies share target

Verify:
- [ ] Attack slot limiting works
- [ ] No more than N attackers at once
- [ ] Smooth rotation of attackers
```

#### Scenario 4: Boss Phase Transitions
```
Setup:
1. Player with strong weapon
2. Spawn boss enemy

Expected:
1. Boss starts in phase 0
2. At 70% health, transition animation plays
3. Phase 1: increased speed
4. At 30% health, transition animation plays
5. Phase 2: new abilities, faster
6. On death, normal death handling

Verify:
- [ ] Phase transitions at correct thresholds
- [ ] Invulnerability during transitions
- [ ] Multipliers apply correctly
- [ ] New abilities used in later phases
```

#### Scenario 5: Flying Enemy
```
Setup:
1. Player in area with obstacles
2. Spawn flying enemy

Expected:
1. Enemy hovers at configured height
2. Avoids obstacles
3. Banks when turning
4. Attacks from air

Verify:
- [ ] Consistent hover height
- [ ] No ground clipping
- [ ] Smooth movement
- [ ] Attack functionality intact
```

#### Scenario 6: Encounter Spawning
```
Setup:
1. EncounterData with 5-10 enemies, staggered spawn
2. Trigger encounter via debug button

Expected:
1. Enemies spawn at valid NavMesh positions
2. Outside player FOV when possible
3. Staggered over configured interval
4. Director tracks all enemies
5. Encounter ends when all dead

Verify:
- [ ] Spawn positions valid
- [ ] Spawn count within range
- [ ] All enemies tracked
- [ ] Encounter state transitions correctly
```

#### Scenario 7: Perception Edge Cases
```
Setup:
1. Player behind wall
2. Nearby enemy

Expected:
1. Enemy doesn't see through wall
2. Enemy hears player if close enough
3. Goes to last known position
4. Eventually gives up

Verify:
- [ ] Line of sight blocked by geometry
- [ ] Hearing works through walls
- [ ] Memory duration respected
```

### Debug Commands Checklist

```csharp
// Add these debug methods to EncounterDirector:

#if UNITY_EDITOR
[Button("Spawn Test Melee")]
void DebugSpawnMelee() { /* Spawn melee enemy */ }

[Button("Spawn Test Ranged")]
void DebugSpawnRanged() { /* Spawn ranged enemy */ }

[Button("Spawn Test Boss")]
void DebugSpawnBoss() { /* Spawn boss */ }

[Button("Spawn Test Swarm (5)")]
void DebugSpawnSwarm() { /* Spawn 5 enemies */ }

[Button("Kill All")]
void DebugKillAll() { /* Kill all active enemies */ }

[Button("Freeze All AI")]
void DebugFreezeAI() { /* Pause all state machines */ }

[Button("Log All States")]
void DebugLogStates() { /* Log current state of each enemy */ }
#endif
```

### What "Done" Looks Like

All test scenarios pass. No unexpected state transitions. Damage flows correctly through existing systems. Performance acceptable with 10+ enemies.

---

## Phase 14 — Common Pitfalls & Anti-Patterns

### Anti-Pattern 1: State Contains Damage Logic

**Wrong:**
```csharp
public class BadAttackState : BaseState<EnemyContext>
{
    public override void Update(EnemyContext ctx)
    {
        // BAD: Directly dealing damage in state
        var player = PlayerManager.Instance.Health;
        player.TakeDamage(10);
    }
}
```

**Right:**
```csharp
public class GoodAttackState : BaseState<EnemyContext>
{
    public override void Update(EnemyContext ctx)
    {
        // Trigger ability executor
        ctx.Controller.GetComponent<EnemyAbilityExecutor>()?.ExecuteAbility(ability);
    }
}
```

### Anti-Pattern 2: Embedded Raycasts in States

**Wrong:**
```csharp
public class BadChaseState : BaseState<EnemyContext>
{
    public override void Update(EnemyContext ctx)
    {
        // BAD: Raycast in state
        if (Physics.Raycast(...))
        {
            // Found player
        }
    }
}
```

**Right:**
```csharp
public class GoodChaseState : BaseState<EnemyContext>
{
    public override void Update(EnemyContext ctx)
    {
        // Query perception system
        if (ctx.HasTarget)
        {
            // React to target
        }
    }
}
```

### Anti-Pattern 3: Hardcoded Values in Code

**Wrong:**
```csharp
public class BadEnemy : MonoBehaviour
{
    private float health = 100f;  // BAD: Hardcoded
    private float speed = 5f;     // BAD: Hardcoded
}
```

**Right:**
```csharp
// All values from EnemyArchetype ScriptableObject
_health.SetMaxHealth(archetype.MaxHealth);
_navAgent.speed = archetype.MoveSpeed;
```

### Anti-Pattern 4: New Health/Damage Systems

**Wrong:**
```csharp
// Creating parallel damage system
public class EnemyHealth : MonoBehaviour
{
    public void TakeDamage(float amount) { ... }
}
```

**Right:**
```csharp
// Reuse existing HealthComponent
[RequireComponent(typeof(HealthComponent))]
public class EnemyController : MonoBehaviour
{
    // HealthComponent handles everything
}
```

### Anti-Pattern 5: Type-Specific Enemy Scripts

**Wrong:**
```
Scripts/AI/Enemy/
├── MeleeGrunt.cs
├── RangedShooter.cs
├── FlyingDrone.cs
├── BossEnemy.cs
└── ...
```

**Right:**
```
Scripts/AI/Enemy/
└── EnemyController.cs  // One controller, data drives behavior

Data/Archetypes/
├── MeleeGrunt.asset
├── RangedShooter.asset
├── FlyingDrone.asset
└── Boss_Brute.asset
```

### Anti-Pattern 6: Tight Coupling to Player Systems

**Wrong:**
```csharp
// Direct reference to specific player component
var playerHealth = PlayerManager.Instance.Health;
playerHealth.TakeDamage(10);
```

**Right:**
```csharp
// Use interface
var damageable = hitObject.GetComponent<IDamageable>();
damageable?.TakeDamage(damage, damageType, gameObject);
```

### Anti-Pattern 7: Monolithic State Classes

**Wrong:**
```csharp
public class DoEverythingState : BaseState<EnemyContext>
{
    // 500+ lines handling all behaviors
}
```

**Right:**
```csharp
// Focused single-responsibility states
public class ChaseState { }      // Just chasing
public class AttackState { }     // Just attacking
public class PositioningState { } // Just positioning
```

### Anti-Pattern 8: Ignoring Existing Patterns

**Wrong:**
```csharp
// Ignoring companion pattern, doing something completely different
public class EnemyAI : MonoBehaviour
{
    enum Mode { Idle, Active }
    // Different architecture
}
```

**Right:**
```csharp
// Follow companion pattern
public class EnemyController : MonoBehaviour
{
    // Same architecture: context + state machine
    // Same patterns: ScriptableObject config
    // Same subsystem structure
}
```

### Performance Pitfalls

1. **Perception every frame**: Use throttled updates
2. **NavMesh queries every frame**: Cache paths
3. **GetComponent in Update**: Cache references
4. **Allocating in hot paths**: Use object pools
5. **Too many active enemies**: Use LOD for distant enemies

### Debugging Pitfalls

1. **Silent failures**: Always log state transitions
2. **No gizmos**: Add visual debugging
3. **No inspector visibility**: Use Odin's ShowInInspector
4. **Hard to reproduce**: Add debug spawn buttons
5. **No state stepping**: Add pause/step functionality

---

## Phase 15 — Extension Hooks

### Future Enemy Types

#### Adding a New Archetype
1. Create new `EnemyArchetype` asset
2. Create new `EnemyAbilityData` assets
3. Create prefab with required components
4. No code changes required

#### Adding a New Ability Type
1. Extend `EnemyAbilityExecutor` with new method
2. Add ability-type enum if needed
3. Call from existing timing framework

#### Adding a New Movement Type
1. Add to `EnemyMovementType` enum
2. Create new movement component
3. Add case in `EnemyContext.SetDestination()`

### Difficulty Scaling

```csharp
// Add to EnemyArchetype or create DifficultyModifier SO:
[System.Serializable]
public class DifficultyScaling
{
    public float HealthMultiplier = 1f;
    public float DamageMultiplier = 1f;
    public float SpeedMultiplier = 1f;
    public float PerceptionMultiplier = 1f;
}

// Apply in EnemyController.Initialize():
var difficulty = GameSettings.Instance.CurrentDifficulty;
_health.SetMaxHealth((int)(archetype.MaxHealth * difficulty.HealthMultiplier));
_navAgent.speed = archetype.MoveSpeed * difficulty.SpeedMultiplier;
```

### Status Effects

```csharp
// Create status effect system that hooks into existing health:
public interface IStatusEffectReceiver
{
    void ApplyStatusEffect(StatusEffect effect);
    void RemoveStatusEffect(StatusEffectType type);
}

// Implement on EnemyController:
public void ApplyStatusEffect(StatusEffect effect)
{
    switch (effect.Type)
    {
        case StatusEffectType.Slow:
            _navAgent.speed *= effect.Value;
            break;
        case StatusEffectType.DamageOverTime:
            StartCoroutine(ApplyDOT(effect));
            break;
    }
}
```

### Loot System Hook

```csharp
// Add to EnemyDeadState.Enter():
public override void Enter(EnemyContext ctx)
{
    // ... existing death logic ...

    // Spawn loot
    var lootSpawner = ctx.Controller.GetComponent<EnemyLootSpawner>();
    lootSpawner?.SpawnLoot(ctx.Archetype.LootTable);
}
```

### Save/Load Hook

```csharp
// Add to EncounterDirector:
public EncounterSaveData GetSaveData()
{
    return new EncounterSaveData
    {
        EncounterId = currentEncounter?.EncounterId,
        EnemyPositions = activeEnemies.Select(e => e.transform.position).ToList(),
        EnemyHealth = activeEnemies.Select(e => e.Health.CurrentHealth).ToList()
    };
}

public void LoadSaveData(EncounterSaveData data)
{
    // Restore encounter state
}
```

### Network Multiplayer Hook (Future)

```csharp
// States check for authority:
public override void Update(EnemyContext ctx)
{
    if (!ctx.Controller.HasAuthority)
        return; // Only server runs AI

    // ... existing logic ...
}
```

### Custom State Injection

```csharp
// Allow archetypes to specify custom states:
[SerializeField] private string customIdleStateName;

// In Initialize():
if (!string.IsNullOrEmpty(archetype.CustomIdleStateName))
{
    var customState = StateFactory.Create(archetype.CustomIdleStateName);
    _stateMachine.RegisterState(EnemyState.Idle, customState);
}
```

### Event Hooks for External Systems

```csharp
// EnemyController events:
public event Action<EnemyController> OnSpawned;
public event Action<EnemyController> OnDeath;
public event Action<EnemyController, float> OnDamaged;
public event Action<EnemyController, EnemyState, EnemyState> OnStateChanged;
public event Action<int, BossPhase> OnBossPhaseChanged;

// EncounterDirector events:
public event Action<EncounterData> OnEncounterStarted;
public event Action<EncounterData> OnEncounterEnded;
public event Action<EnemyController> OnEnemySpawned;
public event Action<EnemyController> OnEnemyDied;
```

### Companion Integration (Future)

Since companions and enemies share the same state machine base:

```csharp
// Companions could share common states:
// Idle, Chase, Attack could be reused
// Or create shared base implementations:

public class SharedIdleState<TContext> : BaseState<TContext>
    where TContext : class, IHasNavAgent, IHasTarget
{
    // Generic idle behavior
}
```

---

## Appendix A: File Structure Summary

```
Assets/Scripts/
├── AI/
│   ├── StateMachine/
│   │   ├── IState.cs
│   │   ├── BaseState.cs
│   │   └── StateMachine.cs
│   ├── Enemy/
│   │   ├── EnemyController.cs
│   │   ├── EnemyContext.cs
│   │   ├── EnemyState.cs
│   │   ├── Data/
│   │   │   ├── EnemyArchetype.cs
│   │   │   ├── EnemyAbilityData.cs
│   │   │   ├── EnemyMovementType.cs
│   │   │   └── EnemyCombatStyle.cs
│   │   ├── States/
│   │   │   ├── EnemyInactiveState.cs
│   │   │   ├── EnemyIdleState.cs
│   │   │   ├── EnemyPatrolState.cs
│   │   │   ├── EnemyChaseState.cs
│   │   │   ├── EnemyPositioningState.cs
│   │   │   ├── EnemyAttackState.cs
│   │   │   ├── EnemyHurtState.cs
│   │   │   ├── EnemyFleeState.cs
│   │   │   ├── EnemyDeadState.cs
│   │   │   └── BossPhaseTransitionState.cs
│   │   ├── Combat/
│   │   │   ├── EnemyAbilityExecutor.cs
│   │   │   └── EnemyProjectile.cs
│   │   └── Movement/
│   │       └── EnemyFlyingMovement.cs
│   ├── Perception/
│   │   ├── IPerceivable.cs
│   │   ├── PerceptionTarget.cs
│   │   └── EnemyPerception.cs
│   └── Group/
│       ├── EnemyGroup.cs
│       └── EnemyGroupMember.cs
├── Encounters/
│   ├── EncounterDirector.cs
│   ├── EncounterType.cs
│   └── EncounterData.cs
├── Player/
│   └── PlayerPerceivable.cs (NEW)
└── ... (existing unchanged)
```

---

## Appendix B: Quick Reference

### State Transitions

```
Inactive → Idle
Idle → Patrol, Chase, Hurt, Dead
Patrol → Idle, Chase, Hurt, Dead
Chase → Idle, Attack, Positioning, Hurt, Dead
Positioning → Idle, Attack, Chase, Hurt, Dead
Attack → Idle, Chase, Hurt, Dead
Hurt → Idle, Chase, Flee, Dead
Flee → Idle, Dead
BossPhaseTransition → Chase, Idle
```

### Damage Flow

```
Enemy Attack → EnemyAbilityExecutor
  → Melee: OverlapSphere
  → Ranged: Raycast OR Projectile
    → Find IDamageable
    → damageable.TakeDamage(damage, type, source)
      → HealthComponent.TakeDamage()
        → OnHealthChanged event
        → OnDeath event (if dead)
```

### Perception Flow

```
EnemyPerception.Update() [throttled]
  → Physics.OverlapSphere (target detection)
  → For each target:
    → CheckSight (range, angle, LOS)
    → CheckHearing (range, noise)
  → Update PerceptionTarget data
  → SelectPrimaryTarget
  → Update EnemyContext
  → States query context.HasTarget, etc.
```

### Boss Phase Flow

```
EnemyController.Update()
  → CheckBossPhaseTransition()
    → context.ShouldTransitionPhase()
      → Check health vs thresholds
    → If true: ForceState(BossPhaseTransition)

BossPhaseTransitionState.Enter()
  → Stop movement
  → Set invulnerable
  → Play animation
  → Fire OnBossPhaseChanged

BossPhaseTransitionState.Update()
  → Wait for duration
  → Apply phase changes
  → End invulnerability
  → Return to combat
```

---

## Appendix C: Checklist for New Enemy Type

- [ ] Create `EnemyArchetype` ScriptableObject
- [ ] Fill in all stats (health, speed, perception)
- [ ] Create `EnemyAbilityData` for each ability
- [ ] Assign abilities to archetype
- [ ] Create prefab from template
- [ ] Add all required components
- [ ] Configure NavMeshAgent
- [ ] Set up Animator with required parameters
- [ ] Add animation events for attacks
- [ ] Configure perception eye point
- [ ] Configure attack origins
- [ ] Set up HitEffectReceiver
- [ ] Test spawn via EncounterDirector
- [ ] Verify all state transitions
- [ ] Test damage dealing
- [ ] Test taking damage
- [ ] Test death

---

*End of Implementation Guide*
