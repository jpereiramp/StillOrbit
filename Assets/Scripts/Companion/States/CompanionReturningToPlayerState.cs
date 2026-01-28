/// <summary>
/// Companion is returning to player after depositing.
/// Destination is set by CompanionAutoDeposit.
/// </summary>
public class CompanionReturningToPlayerState : BaseState<CompanionContext>
{
    public override void Enter(CompanionContext ctx)
    {
        // Destination is set by AutoDeposit.ReturnToPlayer()
        // State just ensures we're in the correct mode
        if (ctx.PlayerTransform != null)
        {
            ctx.Movement?.SetDestination(ctx.PlayerTransform.position);
        }
    }
}
