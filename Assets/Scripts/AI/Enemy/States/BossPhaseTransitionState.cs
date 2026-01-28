using UnityEngine;

/// <summary>
/// Special state for boss phase transitions.
/// Handles invulnerability, animations, and phase application.
/// NOTE: This state is STATELESS - all per-enemy data is stored in EnemyContext.
/// </summary>
public class BossPhaseTransitionState : BaseState<EnemyContext>
{
    private const float TransitionDuration = 2f;

    public override void Enter(EnemyContext ctx)
    {
        // Determine target phase
        ctx.ShouldTransitionPhase(out int targetPhase);
        ctx.TargetBossPhase = targetPhase;

        // Stop movement
        ctx.StopMovement();

        // Brief invulnerability during transition
        ctx.Health.SetInvulnerable(true);

        // Play transition animation
        if (ctx.TargetBossPhase > 0 && ctx.TargetBossPhase <= ctx.Archetype.BossPhases.Count)
        {
            var phaseData = ctx.Archetype.BossPhases[ctx.TargetBossPhase - 1];
            if (!string.IsNullOrEmpty(phaseData.OnEnterTrigger) && ctx.Animator != null)
            {
                ctx.Animator.SetTrigger(phaseData.OnEnterTrigger);
            }

            Debug.Log($"[Boss] {ctx.Controller.name} entering phase {ctx.TargetBossPhase}: {phaseData.PhaseName}");
        }
    }

    public override void Update(EnemyContext ctx)
    {
        if (ctx.TimeSinceStateEnter >= TransitionDuration)
        {
            // Apply phase changes
            ctx.CurrentBossPhase = ctx.TargetBossPhase;
            ctx.PhaseTransitionPending = false;

            // Update movement speed for new phase
            float speedMultiplier = ctx.GetSpeedMultiplier();
            if (ctx.Archetype.MovementType != EnemyMovementType.Flying)
            {
                ctx.NavAgent.speed = ctx.Archetype.MoveSpeed * speedMultiplier;
            }

            // End invulnerability
            ctx.Health.SetInvulnerable(false);

            // Broadcast phase change event
            if (ctx.TargetBossPhase > 0 && ctx.TargetBossPhase <= ctx.Archetype.BossPhases.Count)
            {
                var phaseData = ctx.Archetype.BossPhases[ctx.TargetBossPhase - 1];
                ctx.Controller.NotifyBossPhaseChanged(ctx.TargetBossPhase, phaseData);
            }

            // Resume combat
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
        // Ensure invulnerability is removed even if interrupted
        ctx.Health.SetInvulnerable(false);
    }
}
