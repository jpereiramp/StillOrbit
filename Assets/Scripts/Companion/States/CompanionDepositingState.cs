/// <summary>
/// Companion is depositing resources at a depot.
/// </summary>
public class CompanionDepositingState : BaseState<CompanionContext>
{
    public override void Enter(CompanionContext ctx)
    {
        ctx.Movement?.Stop();
    }
}
