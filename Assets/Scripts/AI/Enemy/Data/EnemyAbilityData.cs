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