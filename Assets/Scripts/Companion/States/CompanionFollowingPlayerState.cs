/// <summary>
/// Companion is actively following the player.
/// Maintains follow distance.
/// </summary>
public class CompanionFollowingPlayerState : BaseState<CompanionContext>
{
    public override void Enter(CompanionContext ctx)
    {
        ctx.Movement?.StartFollowingPlayer();
    }
}
