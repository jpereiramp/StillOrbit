using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Item data for weapons (swords, clubs, etc.).
/// Weapons are primarily effective against Flesh targets.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "StillOrbit/Items/Weapon Data")]
public class WeaponData : ItemData
{
    [BoxGroup("Weapon Stats")]
    [Min(0)]
    [SerializeField] private float attackRate = 1f;

    [BoxGroup("Weapon Stats")]
    [Min(0)]
    [SerializeField] private float range = 2f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against creatures and players")]
    [Min(0)]
    [SerializeField] private float fleshDamage = 10f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against trees and wooden structures")]
    [Min(0)]
    [SerializeField] private float woodDamage = 2f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against rocks, stone, and ore")]
    [Min(0)]
    [SerializeField] private float rockDamage = 1f;

    public float AttackRate => attackRate;
    public float Range => range;

    /// <summary>
    /// Gets the damage value for a specific target type.
    /// </summary>
    public float GetDamage(DamageType targetType)
    {
        return targetType switch
        {
            DamageType.Flesh => fleshDamage,
            DamageType.Wood => woodDamage,
            DamageType.Rock => rockDamage,
            _ => fleshDamage
        };
    }
}
