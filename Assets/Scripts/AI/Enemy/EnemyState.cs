// Scripts/AI/Enemy/EnemyState.cs

/// <summary>
/// Possible states for enemies.
/// States are implemented as IState classes, not embedded logic.
/// </summary>
public enum EnemyState
{
    /// <summary>Inactive/not yet initialized.</summary>
    Inactive,

    /// <summary>Idle, no target, not moving.</summary>
    Idle,

    /// <summary>Patrolling along waypoints or randomly.</summary>
    Patrol,

    /// <summary>Investigating a noise or last known position.</summary>
    Investigate,

    /// <summary>Actively chasing a target.</summary>
    Chase,

    /// <summary>Positioning for attack.</summary>
    Positioning,

    /// <summary>Executing an attack ability.</summary>
    Attack,

    /// <summary>Reacting to being hit (stagger).</summary>
    Hurt,

    /// <summary>Fleeing from danger.</summary>
    Flee,

    /// <summary>Boss transitioning between phases.</summary>
    BossPhaseTransition,

    /// <summary>Dead, playing death animation.</summary>
    Dead
}