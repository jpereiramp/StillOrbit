
/// <summary>
/// Primary combat behavior pattern.
/// </summary>
public enum EnemyCombatStyle
{
    /// <summary>Close-range attacks only.</summary>
    Melee,

    /// <summary>Ranged attacks, maintains distance.</summary>
    Ranged,

    /// <summary>Mix of melee and ranged based on distance.</summary>
    Hybrid,

    /// <summary>Support role (buffs allies, debuffs player).</summary>
    Support,

    /// <summary>Suicide bomber style.</summary>
    Kamikaze
}