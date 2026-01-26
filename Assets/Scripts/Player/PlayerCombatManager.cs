using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles combat logic for the player.
/// Triggers attack animations and processes hits detected by weapon hitboxes.
/// </summary>
public class PlayerCombatManager : MonoBehaviour
{
    public static PlayerCombatManager Instance { get; private set; }

    [BoxGroup("References")]
    [SerializeField]
    private PlayerManager playerManager;

    [BoxGroup("Animation")]
    [SerializeField]
    private string meleeAttackTrigger = "MeleeAttack";

    [FoldoutGroup("Fallback Hit Effects")]
    [Tooltip("Default hit sound when target has no HitEffectReceiver")]
    [SerializeField]
    private AudioClip fallbackHitSound;

    [FoldoutGroup("Fallback Hit Effects")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fallbackHitSoundVolume = 1f;

    [FoldoutGroup("Fallback Hit Effects")]
    [Tooltip("Default hit VFX when target has no HitEffectReceiver")]
    [SerializeField]
    private GameObject fallbackHitVFXPrefab;

    [FoldoutGroup("Fallback Hit Effects")]
    [SerializeField]
    private float fallbackVFXLifetime = 2f;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool isAttacking;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private MeleeWeapon currentWeapon;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<IDamageable> hitTargetsThisSwing = new List<IDamageable>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerManager == null)
        {
            playerManager = GetComponent<PlayerManager>();
        }
    }

    /// <summary>
    /// Initiates a melee attack by triggering the animation.
    /// The actual hit detection happens via animation events.
    /// </summary>
    /// <param name="weapon">The melee weapon being used</param>
    /// <returns>True if attack was initiated</returns>
    public bool PerformMeleeAttack(MeleeWeapon weapon)
    {
        if (playerManager == null)
        {
            Debug.LogWarning("[Combat] No PlayerManager assigned");
            return false;
        }

        if (isAttacking)
        {
            Debug.Log("[Combat] Already attacking, ignoring");
            return false;
        }

        if (playerManager.Animator == null)
        {
            Debug.LogWarning("[Combat] No Animator assigned to PlayerManager");
            return false;
        }

        // Store weapon reference for damage calculation later
        currentWeapon = weapon;
        isAttacking = true;
        hitTargetsThisSwing.Clear();

        // Trigger the attack animation
        playerManager.Animator.SetTrigger(meleeAttackTrigger);
        Debug.Log("[Combat] Melee attack animation triggered");

        return true;
    }

    /// <summary>
    /// Called by animation event at the START of the attack swing.
    /// Enables the weapon's hitbox to detect collisions.
    /// </summary>
    public void AnimationEvent_PerformMeleeAttack_Start()
    {
        Debug.Log("[Combat] Attack swing started - enabling hitbox");

        if (currentWeapon != null)
        {
            currentWeapon.EnableHitbox();
        }
    }

    /// <summary>
    /// Called by animation event at the END of the attack swing.
    /// Disables the weapon's hitbox and processes all hits.
    /// </summary>
    public void AnimationEvent_PerformMeleeAttack_End()
    {
        Debug.Log("[Combat] Attack swing ended - disabling hitbox");

        if (currentWeapon != null)
        {
            currentWeapon.DisableHitbox();
        }

        isAttacking = false;
    }

    /// <summary>
    /// Called by weapon hitbox when it detects a hit during the swing.
    /// Applies damage immediately to avoid hitting the same target twice.
    /// </summary>
    public void RegisterHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!isAttacking || currentWeapon == null)
            return;

        IDamageable damageable = FindDamageableInObject(hitObject);
        if (damageable == null)
        {
            Debug.Log($"[Combat] Hit {hitObject.name} but it's not damageable");
            return;
        }

        // Prevent hitting the same target multiple times in one swing
        if (hitTargetsThisSwing.Contains(damageable))
            return;

        hitTargetsThisSwing.Add(damageable);

        // Calculate and apply damage
        DamageType targetType = damageable.DamageType;
        float damage = currentWeapon.GetDamage(targetType);

        damageable.TakeDamage(damage, targetType, playerManager.gameObject);
        currentWeapon.NotifyHit();

        // Play hit effects
        PlayHitEffects(hitObject, hitPoint, hitNormal);

        Debug.Log($"[Combat] Hit {hitObject.name} ({targetType}) for {damage:F1} damage");
    }

    /// <summary>
    /// Plays hit effects on the target. Uses HitEffectReceiver if present, otherwise uses fallback.
    /// </summary>
    private void PlayHitEffects(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Look for HitEffectReceiver on hit object or its hierarchy
        HitEffectReceiver hitEffectReceiver = FindHitEffectReceiverInObject(hitObject);

        if (hitEffectReceiver != null)
        {
            // Use target's custom hit effects
            hitEffectReceiver.PlayHitEffect(hitPoint, hitNormal);
        }
    }

    /// <summary>
    /// Searches for HitEffectReceiver component on the object, its parent, or root.
    /// </summary>
    private HitEffectReceiver FindHitEffectReceiverInObject(GameObject obj)
    {
        if (obj == null) return null;

        // Check self
        var receiver = obj.GetComponent<HitEffectReceiver>();
        if (receiver != null) return receiver;

        // Check parent
        if (obj.transform.parent != null)
        {
            receiver = obj.transform.parent.GetComponent<HitEffectReceiver>();
            if (receiver != null) return receiver;
        }

        // Check root
        if (obj.transform.root != null && obj.transform.root != obj.transform)
        {
            receiver = obj.transform.root.GetComponent<HitEffectReceiver>();
            if (receiver != null) return receiver;
        }

        return null;
    }

    /// <summary>
    /// Searches for IDamageable component on the object, its parent, or root.
    /// </summary>
    private IDamageable FindDamageableInObject(GameObject obj)
    {
        if (obj == null) return null;

        // Check self
        var damageable = obj.GetComponent<IDamageable>();
        if (damageable != null) return damageable;

        // Check parent
        if (obj.transform.parent != null)
        {
            damageable = obj.transform.parent.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        // Check root
        if (obj.transform.root != null && obj.transform.root != obj.transform)
        {
            damageable = obj.transform.root.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        return null;
    }

    public bool IsAttacking => isAttacking;
}
