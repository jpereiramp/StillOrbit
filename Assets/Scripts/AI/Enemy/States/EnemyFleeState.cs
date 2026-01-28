using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy flees from the current threat when health is low.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyFleeState : BaseState<EnemyContext>
{
    private const float FleeDistance = 20f;
    private const float SafeDistance = 25f;
    private const float MaxFleeTime = 10f;
    private const float PathUpdateInterval = 0.5f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * 1.2f; // Run faster when fleeing
        ctx.PathUpdateTimer = 0f;

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", true);

        // Pick initial flee destination
        PickFleeDestination(ctx);

        Debug.Log($"[EnemyFleeState] {ctx.Controller.name} fleeing!");
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for death
        if (!ctx.Health.IsAlive())
        {
            ctx.Controller.ForceState(EnemyState.Dead);
            return;
        }

        // Check if we've reached safety
        float distanceToThreat = ctx.HasTarget
            ? Vector3.Distance(ctx.Transform.position, ctx.CurrentTarget.position)
            : Vector3.Distance(ctx.Transform.position, ctx.LastKnownTargetPosition);

        if (distanceToThreat >= SafeDistance)
        {
            // We're safe, go back to idle
            ctx.CurrentTarget = null;
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Timeout - stop fleeing after max time
        if (ctx.TimeSinceStateEnter >= MaxFleeTime)
        {
            ctx.CurrentTarget = null;
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Update flee path periodically
        ctx.PathUpdateTimer += Time.deltaTime;
        if (ctx.PathUpdateTimer >= PathUpdateInterval)
        {
            ctx.PathUpdateTimer = 0f;
            PickFleeDestination(ctx);
        }

        // Check if we've reached our flee destination
        if (!ctx.NavAgent.pathPending && ctx.NavAgent.remainingDistance <= 1.5f)
        {
            PickFleeDestination(ctx);
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", false);
    }

    private void PickFleeDestination(EnemyContext ctx)
    {
        Vector3 threatPosition = ctx.HasTarget
            ? ctx.CurrentTarget.position
            : ctx.LastKnownTargetPosition;

        // Direction away from threat
        Vector3 fleeDirection = (ctx.Transform.position - threatPosition).normalized;

        // Add some randomness to avoid predictable fleeing
        fleeDirection = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * fleeDirection;

        Vector3 fleeTarget = ctx.Transform.position + fleeDirection * FleeDistance;

        // Find valid NavMesh position
        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, FleeDistance, NavMesh.AllAreas))
        {
            ctx.NavAgent.SetDestination(hit.position);
        }
        else
        {
            // Fallback: try a random direction
            Vector3 randomDir = Random.insideUnitSphere * FleeDistance;
            randomDir.y = 0;
            fleeTarget = ctx.Transform.position + randomDir;

            if (NavMesh.SamplePosition(fleeTarget, out hit, FleeDistance, NavMesh.AllAreas))
            {
                ctx.NavAgent.SetDestination(hit.position);
            }
        }
    }
}
