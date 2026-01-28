/// <summary>
/// Companion is inactive/hidden.
/// </summary>
public class CompanionInactiveState : BaseState<CompanionContext>
{
    public override void Enter(CompanionContext ctx)
    {
        ctx.Movement?.Stop();
    }
}
