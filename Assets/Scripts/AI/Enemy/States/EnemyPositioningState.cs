using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy repositions to get a better attack angle or range.
/// Used by ranged enemies or when path to target is blocked.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyPositioningState : BaseState<EnemyContext>
{
    private const float PositioningTimeout = 3f;
    private const float OptimalRangeBuffer = 2f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * 0.8f; // Slightly slower while positioning

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", true);

        // Calculate optimal position
        PickPositioningDestination(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        // Lost target
        if (!ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Check for death
        if (!ctx.Health.IsAlive())
        {
            ctx.Controller.ForceState(EnemyState.Dead);
            return;
        }

        // Timeout - go back to chase
        if (ctx.TimeSinceStateEnter >= PositioningTimeout)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Reached destination or close enough to attack
        if (!ctx.NavAgent.pathPending && ctx.NavAgent.remainingDistance <= 1f)
        {
            // Check if we can attack from here
            if (ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
            {
                ctx.Controller.RequestStateChange(EnemyState.Attack);
            }
            else
            {
                // Position didn't work, try chasing
                ctx.Controller.RequestStateChange(EnemyState.Chase);
            }
            return;
        }

        // Continuously face target while moving
        if (ctx.HasTarget)
        {
            FaceTarget(ctx);
        }

        // If we're now in range, attack
        if (ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
        {
            ctx.Controller.RequestStateChange(EnemyState.Attack);
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", false);
    }

    private void PickPositioningDestination(EnemyContext ctx)
    {
        if (!ctx.HasTarget)
            return;

        Vector3 targetPos = ctx.CurrentTarget.position;
        float optimalRange = ctx.Archetype.AttackRange - OptimalRangeBuffer;

        // Try to find a position at optimal range
        Vector3 directionFromTarget = (ctx.Transform.position - targetPos).normalized;

        // Try multiple angles to find valid NavMesh position
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 rotatedDir = Quaternion.Euler(0, angle, 0) * directionFromTarget;
            Vector3 candidatePos = targetPos + rotatedDir * optimalRange;

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                // Check if this position has line of sight to target
                if (!Physics.Linecast(hit.position + Vector3.up, targetPos + Vector3.up))
                {
                    ctx.NavAgent.SetDestination(hit.position);
                    return;
                }
            }
        }

        // Fallback: just move toward the target
        ctx.NavAgent.SetDestination(targetPos);
    }

    private void FaceTarget(EnemyContext ctx)
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
}
