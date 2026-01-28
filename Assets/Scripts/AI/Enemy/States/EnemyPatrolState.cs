using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy patrols randomly or along waypoints.
/// </summary>
public class EnemyPatrolState : BaseState<EnemyContext>
{
    private const float WaypointReachedDistance = 1.5f;
    private const float PatrolRadius = 15f;
    private const float MaxPatrolTime = 10f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * 0.6f; // Slower patrol speed

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", true);

        // Pick random patrol destination
        PickNewPatrolPoint(ctx);
    }

    public override void Update(EnemyContext ctx)
    {
        // Check for target - higher priority
        if (ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Check if reached destination
        if (!ctx.NavAgent.pathPending && ctx.NavAgent.remainingDistance <= WaypointReachedDistance)
        {
            // Go back to idle briefly, then patrol again
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Timeout - pick new point or go idle
        if (ctx.TimeSinceStateEnter > MaxPatrolTime)
        {
            PickNewPatrolPoint(ctx);
        }

        // Check if stuck
        CheckIfStuck(ctx);
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;
    }

    private void PickNewPatrolPoint(EnemyContext ctx)
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;
        randomDirection += ctx.Transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, PatrolRadius, NavMesh.AllAreas))
        {
            ctx.PatrolDestination = hit.position;
            ctx.HasPatrolDestination = true;
            ctx.NavAgent.SetDestination(hit.position);
        }
    }

    private void CheckIfStuck(EnemyContext ctx)
    {
        float distanceMoved = Vector3.Distance(ctx.Transform.position, ctx.LastPosition);
        ctx.LastPosition = ctx.Transform.position;

        if (distanceMoved < 0.1f)
        {
            ctx.StuckTimer += Time.deltaTime;
            if (ctx.StuckTimer > 2f)
            {
                ctx.StuckTimer = 0f;
                PickNewPatrolPoint(ctx);
            }
        }
        else
        {
            ctx.StuckTimer = 0f;
        }
    }
}