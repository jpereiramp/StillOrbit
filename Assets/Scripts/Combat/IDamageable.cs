using UnityEngine;

/// <summary>
/// Interface for any entity that can receive damage.
/// Implement this on HealthComponent, destructible objects, resources, etc.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// The type of damage this entity is vulnerable to.
    /// Used by weapons to determine which damage value to apply.
    /// </summary>
    DamageType DamageType { get; }

    /// <summary>
    /// Apply damage to this entity.
    /// </summary>
    /// <param name="amount">Amount of damage to apply</param>
    /// <param name="damageType">Type of damage being dealt (for logging/effects)</param>
    /// <param name="source">The GameObject that caused the damage</param>
    void TakeDamage(float amount, DamageType damageType, GameObject source);
}
