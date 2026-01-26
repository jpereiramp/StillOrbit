/// <summary>
/// Possible states for the companion.
/// </summary>
public enum CompanionState
{
    /// <summary>
    /// Companion is inactive/hidden.
    /// </summary>
    Inactive,

    /// <summary>
    /// Companion is idle, not moving, waiting for commands.
    /// </summary>
    Idle,

    /// <summary>
    /// Companion is being summoned by player call.
    /// Spawning/teleporting near player.
    /// </summary>
    BeingCalled,

    /// <summary>
    /// Companion is actively following the player.
    /// Maintains follow distance.
    /// </summary>
    FollowingPlayer,

    /// <summary>
    /// Companion is navigating to a resource depot.
    /// </summary>
    MovingToDepot,

    /// <summary>
    /// Companion is depositing resources at a depot.
    /// </summary>
    Depositing,

    /// <summary>
    /// Companion is returning to player after depositing.
    /// </summary>
    ReturningToPlayer
}