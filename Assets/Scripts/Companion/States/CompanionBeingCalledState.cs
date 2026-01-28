/// <summary>
/// Companion is being summoned by player call.
/// Spawning/teleporting near player.
/// The actual spawn logic is handled by CompanionCallHandler.
/// </summary>
public class CompanionBeingCalledState : BaseState<CompanionContext>
{
    // Entry/exit logic handled by CallHandler
    // This state is transient - CallHandler will trigger transition to FollowingPlayer
}
