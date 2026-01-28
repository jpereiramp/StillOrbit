using UnityEngine;

/// <summary>
/// Enemy executes an attack ability.
/// Does NOT deal damage directly - triggers animation/ability system.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class EnemyAttackState : BaseState<EnemyContext>
{
    // Attack phase constants (matching EnemyContext.AttackPhase)
    private const int PhaseWindup = 0;
    private const int PhaseExecute = 1;
    private const int PhaseRecovery = 2;

    public override void Enter(EnemyContext ctx)
    {
        ctx.NavAgent.isStopped = true;

        // Select ability
        ctx.CurrentAbility = ctx.Archetype.PrimaryAbility;
        if (ctx.CurrentAbility == null)
        {
            Debug.LogWarning($"[EnemyAttackState] No primary ability for {ctx.Archetype.DisplayName}");
            ctx.Controller.RequestStateChange(EnemyState.Chase);
            return;
        }

        // Start windup
        ctx.AttackPhase = PhaseWindup;
        ctx.AttackPhaseTimer = 0f;
        ctx.IsAbilityInProgress = true;
        ctx.AbilityStartTime = Time.time;

        // Trigger animation
        if (!string.IsNullOrEmpty(ctx.CurrentAbility.AnimationTrigger) && ctx.Animator != null)
        {
            ctx.Animator.SetTrigger(ctx.CurrentAbility.AnimationTrigger);
        }

        Debug.Log($"[EnemyAttackState] Starting attack: {ctx.CurrentAbility.DisplayName}");
    }

    public override void Update(EnemyContext ctx)
    {
        // Guard against null ability (can happen if Enter failed)
        if (ctx.CurrentAbility == null)
        {
            ctx.Controller.RequestStateChange(EnemyState.Idle);
            return;
        }

        ctx.AttackPhaseTimer += Time.deltaTime;

        switch (ctx.AttackPhase)
        {
            case PhaseWindup:
                UpdateWindup(ctx);
                break;
            case PhaseExecute:
                UpdateExecute(ctx);
                break;
            case PhaseRecovery:
                UpdateRecovery(ctx);
                break;
        }
    }

    private void UpdateWindup(EnemyContext ctx)
    {
        // Track target during windup if allowed
        if (ctx.CurrentAbility.TrackTargetDuringWindup && ctx.HasTarget)
        {
            FaceTarget(ctx);
        }

        // Transition to execute
        if (ctx.AttackPhaseTimer >= ctx.CurrentAbility.WindupTime)
        {
            ctx.AttackPhase = PhaseExecute;
            ctx.AttackPhaseTimer = 0f;

            // Actual damage is dealt via animation event or ability executor
            // This state just manages timing
            ctx.Controller.GetComponent<EnemyAbilityExecutor>()?.ExecuteAbility(ctx.CurrentAbility);
        }
    }

    private void UpdateExecute(EnemyContext ctx)
    {
        // Brief execution window (damage happens via ability executor)
        if (ctx.AttackPhaseTimer >= 0.1f)
        {
            ctx.AttackPhase = PhaseRecovery;
            ctx.AttackPhaseTimer = 0f;
            ctx.LastAttackTime = Time.time;
        }
    }

    private void UpdateRecovery(EnemyContext ctx)
    {
        if (ctx.AttackPhaseTimer >= ctx.CurrentAbility.RecoveryTime)
        {
            // Attack complete - decide next action
            if (ctx.HasTarget && ctx.IsInAttackRange() && ctx.CanUsePrimaryAbility())
            {
                // Attack again
                ctx.Controller.RequestStateChange(EnemyState.Attack);
            }
            else if (ctx.HasTarget)
            {
                // Chase to close distance
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
        ctx.IsAbilityInProgress = false;
        ctx.CurrentAbility = null;
        ctx.NavAgent.isStopped = false;
    }

    private void FaceTarget(EnemyContext ctx)
    {
        if (!ctx.HasTarget) return;

        Vector3 lookDir = ctx.GetDirectionToTarget();
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            ctx.Transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}
