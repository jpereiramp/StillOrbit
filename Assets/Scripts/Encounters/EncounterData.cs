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