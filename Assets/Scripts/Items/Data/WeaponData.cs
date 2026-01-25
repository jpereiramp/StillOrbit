using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Item data for weapons.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "StillOrbit/Items/Weapon Data")]
public class WeaponData : ItemData
{
    [BoxGroup("Weapon Stats")]
    [Min(0)]
    [SerializeField] private float damage = 10f;

    [BoxGroup("Weapon Stats")]
    [Min(0)]
    [SerializeField] private float attackRate = 1f;

    [BoxGroup("Weapon Stats")]
    [Min(0)]
    [SerializeField] private float range = 2f;

    public float Damage => damage;
    public float AttackRate => attackRate;
    public float Range => range;
}
