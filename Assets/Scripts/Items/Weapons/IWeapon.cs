using UnityEngine;

public interface IWeapon
{
    WeaponData WeaponData { get; }

    /// <summary>
    /// Get damage for a specific damage type
    /// </summary>
    /// <param name="targetType">The type of damage to calculate</param>
    /// <returns>Damage amount as a float</returns>
    float GetDamage(DamageType targetType);

    /// <summary>
    /// Get the range of the weapon
    /// </summary>
    /// <returns>Effective weapon range</returns>
    float GetRange();

    /// <summary>
    /// Called when the weapon successfully hits a target
    /// </summary>
    void NotifyHit();
}