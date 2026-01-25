/// <summary>
/// Categories of damage for the combat system.
/// Used to determine effectiveness of weapons/tools against different targets.
/// </summary>
public enum DamageType
{
    /// <summary>Default damage type, no special modifiers</summary>
    Generic,

    /// <summary>Effective against trees and wooden structures</summary>
    Wood,

    /// <summary>Effective against rocks, stone, and ore</summary>
    Rock,

    /// <summary>Effective against creatures and players</summary>
    Flesh
}
