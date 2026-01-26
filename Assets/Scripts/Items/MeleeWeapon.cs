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

    [BoxGroup("Audio")]
    [Tooltip("Sound played when swinging the weapon (swoosh)")]
    [SerializeField]
    private AudioClip swingSFX;

    [BoxGroup("Audio")]
    [Range(0f, 1f)]
    [SerializeField]
    private float swingSFXVolume = 0.8f;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onAttack;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onHit;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private float lastAttackTime;

    [BoxGroup("Hit Detection")]
    [SerializeField]
    private LayerMask hitLayers;

    public LayerMask HitLayers => hitLayers;
    public bool CanUse => Time.time >= lastAttackTime + GetCooldown();

    public UseResult Use(GameObject user)
    {
        if (!CanUse)
            return UseResult.Failed;

        lastAttackTime = Time.time;

        // Play swing sound (swoosh)
        PlaySwingSFX();

        // Invoke attack event (additional sounds, VFX, etc.)
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

    private void PlaySwingSFX()
    {
        if (swingSFX != null)
        {
            AudioSource.PlayClipAtPoint(swingSFX, transform.position, swingSFXVolume);
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

        return 0f;
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

        return 0f;
    }

    private float GetCooldown()
    {
        float rate = 1f;

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
