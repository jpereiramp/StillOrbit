using UnityEngine;

/// <summary>
/// Enemy chases the current target.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyChaseState : BaseState<EnemyContext>
{
    private const float PathUpdateInterval = 0.25f;
    private const float GiveUpTime = 8f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;
        ctx.PathUpdateTimer = 0f;

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", true);

        // Initial path to target
        UpdatePath(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        // Lost target - check memory
        if (!ctx.HasTarget)
        {
            // Go to last known position or give up
            if (ctx.TimeSinceTargetSeen > ctx.Archetype.MemoryDuration)
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
                return;
            }
        }

        // Check if in attack range
        if (ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
        {
            ctx.Controller.RequestStateChange(EnemyState.Attack);
            return;
        }

        // Update path periodically
        ctx.PathUpdateTimer += Time.deltaTime;
        if (ctx.PathUpdateTimer >= PathUpdateInterval)
        {
            ctx.PathUpdateTimer = 0f;
            UpdatePath(ctx);
        }

        // Face target while moving
        if (ctx.HasTarget)
        {
            Vector3 lookDir = ctx.GetDirectionToTarget();
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                ctx.Transform.rotation = Quaternion.Slerp(
                    ctx.Transform.rotation,
                    targetRot,
                    ctx.Archetype.TurnSpeed * Time.deltaTime * Mathf.Deg2Rad
                );
            }
        }

        // Give up if chasing too long without progress
        if (ctx.TimeSinceStateEnter > GiveUpTime && ctx.GetDistanceToTarget() > ctx.Archetype.SightRange)
        {
            ctx.CurrentTarget = null;
            ctx.Controller.RequestStateChange(EnemyState.Idle);
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", false);
    }

    private void UpdatePath(EnemyContext ctx)
    {
        Vector3 destination = ctx.HasTarget
            ? ctx.CurrentTarget.position
            : ctx.LastKnownTargetPosition;

        ctx.NavAgent.SetDestination(destination);
    }
}
