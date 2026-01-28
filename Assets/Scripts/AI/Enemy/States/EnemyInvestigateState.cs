using UnityEngine;

/// <summary>
/// Enemy investigates a noise or last known target position.
/// Transitions from perception alerts or after losing a target.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyInvestigateState : BaseState<EnemyContext>
{
    private const float InvestigateTimeout = 8f;
    private const float ArrivalDistance = 2f;
    private const float LookAroundDuration = 3f;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * 0.7f; // Cautious movement

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", true);

        // Move to last known position
        if (ctx.LastKnownTargetPosition != Vector3.zero)
        {
            ctx.NavAgent.SetDestination(ctx.LastKnownTargetPosition);
        }

        Debug.Log($"[EnemyInvestigateState] {ctx.Controller.name} investigating position");
    }

    public override void Update(EnemyContext ctx)
    {
        // Found target - chase!
        if (ctx.HasTarget)
        {
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Check for death
        if (!ctx.Health.IsAlive())
        {
            ctx.Controller.ForceState(EnemyState.Dead);
            return;
        }

        // Timeout - give up
        if (ctx.TimeSinceStateEnter >= InvestigateTimeout)
        {
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        // Reached investigation point
        if (!ctx.NavAgent.pathPending && ctx.NavAgent.remainingDistance <= ArrivalDistance)
        {
            // Stop and look around
            ctx.NavAgent.isStopped = true;

            if (ctx.Animator != null)
                ctx.Animator.SetBool("IsMoving", false);

            // Look around behavior (rotate slowly)
            float lookProgress = (ctx.TimeSinceStateEnter % LookAroundDuration) / LookAroundDuration;
            float lookAngle = Mathf.Sin(lookProgress * Mathf.PI * 2f) * 90f;
            Quaternion lookRot = Quaternion.Euler(0, lookAngle, 0) * ctx.Transform.rotation;
            ctx.Transform.rotation = Quaternion.Slerp(
                ctx.Transform.rotation,
                lookRot,
                Time.deltaTime * 2f
            );

            // After looking around for a while, go idle
            if (ctx.TimeSinceStateEnter >= LookAroundDuration + 1f)
            {
                ctx.Controller.RequestStateChange(EnemyState.Idle);
            }
        }
    }

    public override void Exit(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = false;
        ctx.NavAgent.speed = ctx.Archetype.MoveSpeed;

        if (ctx.Animator != null)
            ctx.Animator.SetBool("IsMoving", false);
    }
}
