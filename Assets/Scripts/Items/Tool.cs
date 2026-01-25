using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Behaviour for tool items when held.
/// Attach to the held prefab, not the world prefab.
/// </summary>
public class Tool : MonoBehaviour, IUsable
{
    [BoxGroup("Data")]
    [Tooltip("Optional: link to ToolData for stat values. If null, uses local values.")]
    [SerializeField]
    private WeaponData weaponData;

    [BoxGroup("Local Values")]
    [SerializeField]
    private float damage = 10f;

    [BoxGroup("Local Values")]
    [SerializeField]
    private float attackCooldown = 0.5f;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onAttack;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private float lastAttackTime;

    public bool CanUse => Time.time >= lastAttackTime + GetCooldown();

    public UseResult Use(GameObject user)
    {
        if (!CanUse)
            return UseResult.Failed;

        lastAttackTime = Time.time;

        float weaponDamage = weaponData != null ? weaponData.Damage : damage;

        // TODO: Implement actual attack logic (raycast, hitbox, etc.)
        Debug.Log($"Weapon attack! Damage: {weaponDamage}");

        onAttack?.Invoke();

        return UseResult.Success;
    }

    private float GetCooldown()
    {
        if (weaponData != null && weaponData.AttackRate > 0)
        {
            return 1f / weaponData.AttackRate;
        }
        return attackCooldown;
    }
}