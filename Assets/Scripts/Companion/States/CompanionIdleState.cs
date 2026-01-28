/// <summary>
/// Companion is idle, not moving, waiting for commands.
/// </summary>
public class CompanionIdleState : BaseState<CompanionContext>
{
    public override void Enter(CompanionContext ctx)
    {
        ctx.Movement?.Stop();
    }
}
