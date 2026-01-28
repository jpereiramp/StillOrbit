using UnityEngine;

/// <summary>
/// Enemy reacts to being hit (stagger).
/// </summary>
public class EnemyHurtState : BaseState<EnemyContext>
{
    private const float StaggerDuration = 0.5f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;

        // Interrupt any ability in progress
        ctx.IsAbilityInProgress = false;

        // Play hurt animation
        if (ctx.Animator != null)
            ctx.Animator.SetTrigger("Hurt");

        Debug.Log($"[EnemyHurtState] {ctx.Controller.name} staggered");
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for death
        if (!ctx.Health.IsAlive())
        {
            ctx.Controller.ForceState(EnemyState.Dead);
            return;
        }

        // Check for flee
        if (ctx.Archetype.CanFlee)
        {
            float healthPercent = ctx.Health.GetHealthPercentage() / 100f;
            if (healthPercent <= ctx.Archetype.FleeHealthThreshold)
            {
                ctx.Controller.RequestStateChange(EnemyState.Flee);
                return;
            }
        }

        // Recovery
        if (ctx.TimeSinceStateEnter >= StaggerDuration)
        {
            if (ctx.HasTarget)
            {
                ctx.Controller.RequestStateChange(EnemyState.Chase);
            }
            else
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
            }
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
    }
}