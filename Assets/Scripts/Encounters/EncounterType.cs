using UnityEngine;

/// <summary>
/// Categories of encounters that can occur.
/// Used by EncounterDirector to select appropriate spawning logic.
/// </summary>
public enum EncounterType
{
    /// <summary>No active encounter.</summary>
    None,

    /// <summary>Random enemies appearing during exploration.</summary>
    RandomInvasion,

    /// <summary>Boss encounter with special rules.</summary>
    BossIncursion,

    /// <summary>Enemies native to a procedural planet.</summary>
    PlanetPopulation,

    /// <summary>Scripted story encounter.</summary>
    Scripted,

    /// <summary>Defensive wave (e.g., base defense).</summary>
    DefenseWave
}