using UnityEngine;

/// <summary>
/// Enemy is idle, no target.
/// Will transition to Patrol or Chase based on archetype and perception.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyIdleState : BaseState<EnemyContext>
{
    private const float IdleBeforePatrol = 3f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;
        ctx.NavAgent.ResetPath();
        ctx.IdleTimer = 0f;

        // Optional: Play idle animation
        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", false);
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for target
        if (ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Transition to patrol after idle time
        if (ctx.Archetype.CanPatrol)
        {
            ctx.IdleTimer += Time.deltaTime;
            if (ctx.IdleTimer >= IdleBeforePatrol)
            {
                ctx.Controller.RequestStateChange(EnemyState.Patrol);
            }
        }
    }
}
