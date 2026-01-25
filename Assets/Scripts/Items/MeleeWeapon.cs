using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Behaviour for melee weapons and tools when held.
/// Handles attack cooldown and delegates hit detection to PlayerCombatManager.
/// Attach to the held prefab, not the world prefab.
/// </summary>
public class MeleeWeapon : MonoBehaviour, IUsable
{
    [BoxGroup("Data")]
    [Tooltip("Link to WeaponData or ToolData for stats. Required.")]
    [SerializeField]
    private ItemData itemData;

    [BoxGroup("Hitbox")]
    [Tooltip("The trigger collider used to detect hits during attack swing")]
    [SerializeField]
    private WeaponHitbox weaponHitbox;

    [BoxGroup("Local Fallbacks")]
    [Tooltip("Used if ItemData doesn't provide damage values")]
    [SerializeField]
    private float fallbackDamage = 10f;

    [BoxGroup("Local Fallbacks")]
    [Tooltip("Used if ItemData doesn't provide attack rate")]
    [SerializeField]
    private float fallbackAttackRate = 1f;

    [BoxGroup("Local Fallbacks")]
    [Tooltip("Used if ItemData doesn't provide range")]
    [SerializeField]
    private float fallbackRange = 2f;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onAttack;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onHit;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private float lastAttackTime;

    public bool CanUse => Time.time >= lastAttackTime + GetCooldown();

    private void Awake()
    {
        // Try to find hitbox if not assigned
        if (weaponHitbox == null)
        {
            weaponHitbox = GetComponentInChildren<WeaponHitbox>();
        }

        // Ensure hitbox starts disabled
        if (weaponHitbox != null)
        {
            weaponHitbox.SetActive(false);
        }
    }

    public UseResult Use(GameObject user)
    {
        if (!CanUse)
            return UseResult.Failed;

        lastAttackTime = Time.time;

        // Invoke attack event (sounds, VFX, etc.)
        onAttack?.Invoke();

        // Delegate to combat manager - it will trigger animation and call back to enable/disable hitbox
        if (PlayerCombatManager.Instance != null)
        {
            PlayerCombatManager.Instance.PerformMeleeAttack(this);
        }
        else
        {
            Debug.LogWarning("[MeleeWeapon] PlayerCombatManager.Instance is null, cannot perform attack");
        }

        return UseResult.Success;
    }

    /// <summary>
    /// Enables the weapon hitbox for hit detection. Called by PlayerCombatManager during attack animation.
    /// </summary>
    public void EnableHitbox()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.SetActive(true);
            Debug.Log("[MeleeWeapon] Hitbox enabled");
        }
        else
        {
            Debug.LogWarning("[MeleeWeapon] No WeaponHitbox assigned");
        }
    }

    /// <summary>
    /// Disables the weapon hitbox. Called by PlayerCombatManager when attack animation ends.
    /// </summary>
    public void DisableHitbox()
    {
        if (weaponHitbox != null)
        {
            weaponHitbox.SetActive(false);
            Debug.Log("[MeleeWeapon] Hitbox disabled");
        }
    }

    /// <summary>
    /// Called when the weapon hits something. Invokes the onHit event.
    /// </summary>
    public void NotifyHit()
    {
        onHit?.Invoke();
    }

    /// <summary>
    /// Gets damage value for a specific damage type from the item data.
    /// </summary>
    public float GetDamage(DamageType targetType)
    {
        if (itemData is WeaponData weaponData)
        {
            return weaponData.GetDamage(targetType);
        }

        if (itemData is ToolData toolData)
        {
            return toolData.GetDamage(targetType);
        }

        return fallbackDamage;
    }

    /// <summary>
    /// Gets the attack range.
    /// </summary>
    public float GetRange()
    {
        if (itemData is WeaponData weaponData)
        {
            return weaponData.Range;
        }

        if (itemData is ToolData toolData)
        {
            return toolData.Range;
        }

        return fallbackRange;
    }

    private float GetCooldown()
    {
        float rate = fallbackAttackRate;

        if (itemData is WeaponData weaponData && weaponData.AttackRate > 0)
        {
            rate = weaponData.AttackRate;
        }
        else if (itemData is ToolData toolData && toolData.Rate > 0)
        {
            rate = toolData.Rate;
        }

        return rate > 0 ? 1f / rate : 1f;
    }
}
