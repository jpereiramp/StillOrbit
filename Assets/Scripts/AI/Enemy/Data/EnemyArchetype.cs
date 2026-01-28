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

    [BoxGroup("Combat")]
    [Tooltip("Chance of being staggered when taking damage")]
    [SerializeField] private float staggerChance = 0.3f;

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

    [BoxGroup("Audio")]
    [Tooltip("SFX configuration for this enemy type. Leave null for silent enemies.")]
    [SerializeField] private EnemySFXData sfxData;

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

    public float StaggerChance => staggerChance;
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
    public EnemySFXData SFXData => sfxData;
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