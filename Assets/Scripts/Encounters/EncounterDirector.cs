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

    // Tracks spawn counts per archetype to enforce MaxCount
    private readonly Dictionary<EnemyArchetype, int> _archetypeSpawnCounts = new();

    [BoxGroup("Settings")]
    [SerializeField] private int maxSpawnAttempts = 30;

    [BoxGroup("Settings")]
    [Tooltip("Base radius for NavMesh sampling. Will be scaled based on spawn distance.")]
    [SerializeField] private float navMeshSampleRadius = 5f;

    [BoxGroup("Settings")]
    [Tooltip("Enable detailed spawn position logging for debugging")]
    [SerializeField] private bool debugSpawnPositions = false;

    [BoxGroup("Debug")]
    [SerializeField] private EncounterData debugEncounterData;

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
        _archetypeSpawnCounts.Clear();

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
                Debug.LogWarning($"[EncounterDirector] Could not find valid spawn position for enemy {i + 1}/{totalToSpawn}. " +
                               $"Enable 'Debug Spawn Positions' on EncounterDirector for details.");
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

        // Calculate total weight, excluding entries that have reached MaxCount
        int totalWeight = 0;
        foreach (var entry in pool)
        {
            if (entry.Archetype == null)
                continue;

            // Check if this archetype has reached its MaxCount
            int currentCount = _archetypeSpawnCounts.GetValueOrDefault(entry.Archetype, 0);
            if (currentCount >= entry.MaxCount)
                continue;

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

            // Skip entries that have reached MaxCount
            int currentCount = _archetypeSpawnCounts.GetValueOrDefault(entry.Archetype, 0);
            if (currentCount >= entry.MaxCount)
                continue;

            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.Archetype;
        }

        // Fallback: find first archetype that hasn't reached MaxCount
        foreach (var entry in pool)
        {
            if (entry.Archetype == null)
                continue;

            int currentCount = _archetypeSpawnCounts.GetValueOrDefault(entry.Archetype, 0);
            if (currentCount < entry.MaxCount)
                return entry.Archetype;
        }

        return null;
    }

    private bool TryFindSpawnPosition(EncounterData data, out Vector3 position)
    {
        position = Vector3.zero;

        if (playerTransform == null)
        {
            if (debugSpawnPositions)
                Debug.LogWarning("[EncounterDirector] TryFindSpawnPosition failed: playerTransform is null");
            return false;
        }

        // Pre-calculate player's NavMesh position once (needed for path validation)
        Vector3 playerNavMeshPos = playerTransform.position;
        bool playerOnNavMesh = true;

        if (data.RequireNavMeshReachable)
        {
            // Sample player position on NavMesh - use generous radius
            if (NavMesh.SamplePosition(playerTransform.position, out NavMeshHit playerHit, 10f, NavMesh.AllAreas))
            {
                playerNavMeshPos = playerHit.position;
            }
            else
            {
                playerOnNavMesh = false;
                if (debugSpawnPositions)
                    Debug.LogWarning($"[EncounterDirector] Player position {playerTransform.position} is not near any NavMesh surface!");
            }
        }

        int fovRejects = 0;
        int navMeshSampleRejects = 0;
        int pathCalcRejects = 0;
        int pathIncompleteRejects = 0;

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
                {
                    fovRejects++;
                    continue;
                }
            }

            // Validate NavMesh
            if (data.RequireNavMeshReachable)
            {
                // Scale sample radius based on spawn distance - farther spawns need larger search
                float scaledSampleRadius = Mathf.Max(navMeshSampleRadius, distance * 0.3f);

                if (!NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, scaledSampleRadius, NavMesh.AllAreas))
                {
                    navMeshSampleRejects++;
                    continue;
                }

                candidatePos = hit.position;

                // Skip path validation if player isn't on NavMesh (would always fail)
                if (!playerOnNavMesh)
                {
                    // Just accept the NavMesh position without path validation
                    position = candidatePos;
                    if (debugSpawnPositions)
                        Debug.Log($"[EncounterDirector] Spawn position found (no path validation - player off NavMesh): {position}");
                    return true;
                }

                // Verify path exists to player's NavMesh position
                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(candidatePos, playerNavMeshPos, NavMesh.AllAreas, path))
                {
                    pathCalcRejects++;
                    continue;
                }

                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    pathIncompleteRejects++;
                    continue;
                }
            }

            position = candidatePos;
            if (debugSpawnPositions)
                Debug.Log($"[EncounterDirector] Spawn position found after {i + 1} attempts: {position}");
            return true;
        }

        if (debugSpawnPositions)
        {
            Debug.LogWarning($"[EncounterDirector] TryFindSpawnPosition failed after {maxSpawnAttempts} attempts. " +
                           $"Rejects - FOV: {fovRejects}, NavMesh Sample: {navMeshSampleRejects}, " +
                           $"Path Calc: {pathCalcRejects}, Path Incomplete: {pathIncompleteRejects}");
            Debug.LogWarning($"[EncounterDirector] Spawn params - Distance: {data.MinSpawnDistance}-{data.MaxSpawnDistance}, " +
                           $"RequireNavMesh: {data.RequireNavMeshReachable}, PreferOutsideFOV: {data.PreferOutsideFOV}");
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

            // Track spawn count for MaxCount enforcement
            _archetypeSpawnCounts.TryGetValue(archetype, out int currentCount);
            _archetypeSpawnCounts[archetype] = currentCount + 1;
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
        StartEncounter(debugEncounterData);
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