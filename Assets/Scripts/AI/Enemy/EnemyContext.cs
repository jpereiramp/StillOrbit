using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared context data accessible by all enemy states.
/// This is the "blackboard" for the enemy's state machine.
/// States should be stateless - all per-instance data lives here.
/// </summary>
public class EnemyContext
{
    // Core References
    public EnemyController Controller { get; }
    public EnemyArchetype Archetype { get; }
    public Transform Transform { get; }
    public NavMeshAgent NavAgent { get; }
    public Animator Animator { get; }
    public HealthComponent Health { get; }
    public EnemyFlyingMovement FlyingMovement { get; set; }

    // Target Tracking
    public Transform CurrentTarget { get; set; }
    public Vector3 LastKnownTargetPosition { get; set; }
    public float TimeSinceTargetSeen { get; set; }
    public bool HasTarget => CurrentTarget != null;

    // Combat State
    public float LastAttackTime { get; set; }
    public int CurrentAbilityIndex { get; set; }
    public bool IsAbilityInProgress { get; set; }
    public float AbilityStartTime { get; set; }

    // Attack State Data (moved from state to support stateless pattern)
    public int AttackPhase { get; set; } // 0=Windup, 1=Execute, 2=Recovery
    public float AttackPhaseTimer { get; set; }
    public EnemyAbilityData CurrentAbility { get; set; }

    // Idle State Data
    public float IdleTimer { get; set; }

    // Chase State Data
    public float PathUpdateTimer { get; set; }

    // Movement
    public Vector3 PatrolDestination { get; set; }
    public bool HasPatrolDestination { get; set; }
    public float StuckTimer { get; set; }
    public Vector3 LastPosition { get; set; }

    // Boss Phase (if applicable)
    public int CurrentBossPhase { get; set; }
    public bool PhaseTransitionPending { get; set; }
    public int TargetBossPhase { get; set; } // Target phase during transition

    // Utility
    public float StateEnterTime { get; set; }
    public float TimeSinceStateEnter => Time.time - StateEnterTime;

    public EnemyContext(
        EnemyController controller,
        EnemyArchetype archetype,
        Transform transform,
        NavMeshAgent navAgent,
        Animator animator,
        HealthComponent health)
    {
        Controller = controller;
        Archetype = archetype;
        Transform = transform;
        NavAgent = navAgent;
        Animator = animator;
        Health = health;
    }

    /// <summary>
    /// Get distance to current target.
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (CurrentTarget == null)
            return float.MaxValue;
        return Vector3.Distance(Transform.position, CurrentTarget.position);
    }

    /// <summary>
    /// Get direction to current target.
    /// </summary>
    public Vector3 GetDirectionToTarget()
    {
        if (CurrentTarget == null)
            return Transform.forward;
        return (CurrentTarget.position - Transform.position).normalized;
    }

    /// <summary>
    /// Check if currently within attack range.
    /// </summary>
    public bool IsInAttackRange()
    {
        return GetDistanceToTarget() <= Archetype.AttackRange;
    }

    /// <summary>
    /// Check if primary ability is off cooldown.
    /// </summary>
    public bool CanUsePrimaryAbility()
    {
        var ability = Archetype.PrimaryAbility;
        if (ability == null)
            return false;

        return Time.time >= LastAttackTime + ability.Cooldown;
    }

    /// <summary>
    /// Reset state timing on state enter.
    /// </summary>
    public void OnStateEnter()
    {
        StateEnterTime = Time.time;
    }

    #region Movement Abstraction

    /// <summary>
    /// Set movement destination (handles ground vs flying).
    /// </summary>
    public void SetDestination(Vector3 destination)
    {
        if (Archetype.MovementType == EnemyMovementType.Flying && FlyingMovement != null)
        {
            FlyingMovement.SetDestination(destination);
        }
        else
        {
            NavAgent.SetDestination(destination);
        }
    }

    /// <summary>
    /// Stop movement (handles ground vs flying).
    /// </summary>
    public void StopMovement()
    {
        if (Archetype.MovementType == EnemyMovementType.Flying && FlyingMovement != null)
        {
            FlyingMovement.Stop();
        }
        else
        {
            NavAgent.isStopped = true;
            NavAgent.ResetPath();
        }
    }

    /// <summary>
    /// Resume movement (handles ground vs flying).
    /// </summary>
    public void ResumeMovement()
    {
        if (Archetype.MovementType == EnemyMovementType.Flying && FlyingMovement != null)
        {
            FlyingMovement.Resume();
        }
        else
        {
            NavAgent.isStopped = false;
        }
    }

    /// <summary>
    /// Check if movement has reached destination.
    /// </summary>
    public bool HasReachedDestination(float threshold = 1.5f)
    {
        if (Archetype.MovementType == EnemyMovementType.Flying && FlyingMovement != null)
        {
            return FlyingMovement.HasReachedDestination;
        }
        else
        {
            return !NavAgent.pathPending && NavAgent.remainingDistance <= threshold;
        }
    }

    #endregion

    #region Boss Phase Management

    /// <summary>
    /// Get the current boss phase data (null if not a boss or in phase 0).
    /// </summary>
    public BossPhase GetCurrentBossPhaseData()
    {
        if (!Archetype.IsBoss || Archetype.BossPhases.Count == 0)
            return null;

        if (CurrentBossPhase <= 0 || CurrentBossPhase > Archetype.BossPhases.Count)
            return null;

        return Archetype.BossPhases[CurrentBossPhase - 1];
    }

    /// <summary>
    /// Check if a phase transition should occur based on current health.
    /// </summary>
    public bool ShouldTransitionPhase(out int newPhase)
    {
        newPhase = CurrentBossPhase;

        if (!Archetype.IsBoss)
            return false;

        float healthPercent = Health.GetHealthPercentage() / 100f;

        // Check each phase threshold
        for (int i = 0; i < Archetype.BossPhases.Count; i++)
        {
            var phase = Archetype.BossPhases[i];

            // Already past this phase
            if (i + 1 <= CurrentBossPhase)
                continue;

            // Health dropped below threshold
            if (healthPercent <= phase.HealthThreshold)
            {
                newPhase = i + 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get current phase abilities (or base abilities if no phase).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<EnemyAbilityData> GetCurrentAbilities()
    {
        var phaseData = GetCurrentBossPhaseData();
        if (phaseData != null && phaseData.PhaseAbilities.Count > 0)
        {
            return phaseData.PhaseAbilities;
        }

        return Archetype.Abilities;
    }

    /// <summary>
    /// Get damage multiplier for current phase.
    /// </summary>
    public float GetDamageMultiplier()
    {
        var phaseData = GetCurrentBossPhaseData();
        return phaseData?.DamageMultiplier ?? 1f;
    }

    /// <summary>
    /// Get speed multiplier for current phase.
    /// </summary>
    public float GetSpeedMultiplier()
    {
        var phaseData = GetCurrentBossPhaseData();
        return phaseData?.SpeedMultiplier ?? 1f;
    }

    #endregion
}