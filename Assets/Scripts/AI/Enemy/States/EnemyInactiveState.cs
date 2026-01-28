/// <summary>
/// Enemy is inactive/disabled.
/// </summary>
public class EnemyInactiveState : BaseState<EnemyContext>
{
    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.enabled = false;
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.enabled = true;
        ctx.NavAgent.isStopped = false;
    }
}