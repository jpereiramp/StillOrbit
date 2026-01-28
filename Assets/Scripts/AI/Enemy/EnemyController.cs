// Scripts/AI/Enemy/EnemyController.cs
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Central coordinator for enemy behavior.
/// Mirrors CompanionCoreController architecture.
/// All behavior is data-driven via EnemyArchetype.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyController : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [SerializeField] private EnemyArchetype archetype;

    [BoxGroup("References")]
    [SerializeField] private Transform visualRoot;

    [BoxGroup("References")]
    [SerializeField] private Animator animator;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private EnemyState currentState = EnemyState.Inactive;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isInitialized;

    // Components
    private NavMeshAgent _navAgent;
    private HealthComponent _health;

    // State Machine
    private StateMachine<EnemyState, EnemyContext> _stateMachine;
    private EnemyContext _context;

    // Perception (will be added in Phase 5)
    private EnemyPerception _perception;

    // Events
    public event Action<EnemyController> OnDeath;
    public event Action<EnemyState, EnemyState> OnStateChanged;
    public event Action<int, BossPhase> OnBossPhaseChanged;

    // Public Accessors
    public EnemyArchetype Archetype => archetype;
    public EnemyState CurrentState => currentState;
    public bool IsInitialized => isInitialized;
    public bool IsAlive => _health != null && _health.IsAlive();
    public HealthComponent Health => _health;
    public NavMeshAgent NavAgent => _navAgent;
    public EnemyContext Context => _context;
    public Transform CurrentTarget => _context?.CurrentTarget;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _health = GetComponent<HealthComponent>();
        _perception = GetComponent<EnemyPerception>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // Auto-initialize if archetype is assigned in inspector
        if (archetype != null && !isInitialized)
        {
            Initialize(archetype);
        }
    }

    private void Update()
    {
        if (!isInitialized || currentState == EnemyState.Dead)
            return;

        // Check for boss phase transition before state machine update
        if (archetype.IsBoss && currentState != EnemyState.BossPhaseTransition)
        {
            CheckBossPhaseTransition();
        }

        _stateMachine?.Update();

        // Only use fallback perception if EnemyPerception component is not attached
        // EnemyPerception handles its own context updates
        if (_perception == null)
        {
            UpdateFallbackPerception();
        }

        // Always increment time since target seen when no target (for memory duration)
        if (_context.CurrentTarget == null)
        {
            _context.TimeSinceTargetSeen += Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized || currentState == EnemyState.Dead)
            return;

        _stateMachine?.FixedUpdate();
    }

    /// <summary>
    /// Initialize enemy with archetype data.
    /// Called by EncounterDirector after spawning.
    /// </summary>
    public void Initialize(EnemyArchetype archetypeData)
    {
        if (isInitialized)
        {
            Debug.LogWarning($"[EnemyController] {name} already initialized");
            return;
        }

        archetype = archetypeData;

        // Configure NavMeshAgent (disabled for flying enemies)
        if (archetype.MovementType == EnemyMovementType.Flying)
        {
            _navAgent.enabled = false;

            // Ensure flying movement component exists
            var flyingMovement = GetComponent<EnemyFlyingMovement>();
            if (flyingMovement == null)
            {
                flyingMovement = gameObject.AddComponent<EnemyFlyingMovement>();
            }
        }
        else
        {
            _navAgent.speed = archetype.MoveSpeed;
            _navAgent.angularSpeed = archetype.TurnSpeed;
        }

        // Configure Health
        _health.SetMaxHealth(archetype.MaxHealth, true);
        _health.OnDeath += HandleDeath;

        // Create context
        _context = new EnemyContext(
            this,
            archetype,
            transform,
            _navAgent,
            animator,
            _health
        );

        // Set flying movement reference if applicable
        if (archetype.MovementType == EnemyMovementType.Flying)
        {
            _context.FlyingMovement = GetComponent<EnemyFlyingMovement>();
        }

        // Subscribe to health changes
        _health.OnHealthChanged += HandleHealthChanged;

        // Initialize state machine
        InitializeStateMachine();

        isInitialized = true;

        Debug.Log($"[EnemyController] {name} initialized as {archetype.DisplayName}");
    }

    private void InitializeStateMachine()
    {
        _stateMachine = new StateMachine<EnemyState, EnemyContext>(_context);

        // Register all states
        _stateMachine.RegisterState(EnemyState.Inactive, new EnemyInactiveState());
        _stateMachine.RegisterState(EnemyState.Idle, new EnemyIdleState());
        _stateMachine.RegisterState(EnemyState.Patrol, new EnemyPatrolState());
        _stateMachine.RegisterState(EnemyState.Investigate, new EnemyInvestigateState());
        _stateMachine.RegisterState(EnemyState.Chase, new EnemyChaseState());
        _stateMachine.RegisterState(EnemyState.Positioning, new EnemyPositioningState());
        _stateMachine.RegisterState(EnemyState.Attack, new EnemyAttackState());
        _stateMachine.RegisterState(EnemyState.Hurt, new EnemyHurtState());
        _stateMachine.RegisterState(EnemyState.Flee, new EnemyFleeState());
        _stateMachine.RegisterState(EnemyState.BossPhaseTransition, new BossPhaseTransitionState());
        _stateMachine.RegisterState(EnemyState.Dead, new EnemyDeadState());

        // Register transitions
        RegisterStateTransitions();

        // Subscribe to state changes
        _stateMachine.OnStateChanged += HandleStateChanged;

        // Start in Idle
        _stateMachine.Initialize(EnemyState.Idle);
    }

    private void RegisterStateTransitions()
    {
        // From Inactive
        _stateMachine.RegisterTransitions(EnemyState.Inactive,
            EnemyState.Idle);

        // From Idle
        _stateMachine.RegisterTransitions(EnemyState.Idle,
            EnemyState.Patrol,
            EnemyState.Investigate,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Patrol
        _stateMachine.RegisterTransitions(EnemyState.Patrol,
            EnemyState.Idle,
            EnemyState.Investigate,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Investigate
        _stateMachine.RegisterTransitions(EnemyState.Investigate,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Chase
        _stateMachine.RegisterTransitions(EnemyState.Chase,
            EnemyState.Idle,
            EnemyState.Attack,
            EnemyState.Positioning,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Positioning
        _stateMachine.RegisterTransitions(EnemyState.Positioning,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Attack,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Attack
        _stateMachine.RegisterTransitions(EnemyState.Attack,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Positioning,
            EnemyState.Hurt,
            EnemyState.Dead);

        // From Hurt
        _stateMachine.RegisterTransitions(EnemyState.Hurt,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Flee,
            EnemyState.Dead);

        // From Flee
        _stateMachine.RegisterTransitions(EnemyState.Flee,
            EnemyState.Idle,
            EnemyState.Dead);

        // From BossPhaseTransition (exits to combat or idle)
        _stateMachine.RegisterTransitions(EnemyState.BossPhaseTransition,
            EnemyState.Idle,
            EnemyState.Chase,
            EnemyState.Dead);
    }

    private void HandleStateChanged(EnemyState from, EnemyState to)
    {
        currentState = to;
        _context.OnStateEnter();
        OnStateChanged?.Invoke(from, to);

        Debug.Log($"[EnemyController] {name}: {from} -> {to}");
    }

    /// <summary>
    /// Fallback perception when EnemyPerception component is not attached.
    /// Simple distance-based player detection without sight cones or hearing.
    /// </summary>
    private void UpdateFallbackPerception()
    {
        if (_context.CurrentTarget == null)
        {
            var player = PlayerManager.Instance?.transform;
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.position);
                if (distance <= archetype.SightRange)
                {
                    _context.CurrentTarget = player;
                    _context.LastKnownTargetPosition = player.position;
                    _context.TimeSinceTargetSeen = 0f;
                }
            }
        }
        else
        {
            // Update last known position while target is visible
            _context.LastKnownTargetPosition = _context.CurrentTarget.position;
            _context.TimeSinceTargetSeen = 0f;
        }
    }

    private void HandleDeath()
    {
        _stateMachine.ForceState(EnemyState.Dead);
        OnDeath?.Invoke(this);
    }

    private void CheckBossPhaseTransition()
    {
        if (_context.ShouldTransitionPhase(out int newPhase))
        {
            _context.PhaseTransitionPending = true;
            ForceState(EnemyState.BossPhaseTransition);
        }
    }

    /// <summary>
    /// Called by BossPhaseTransitionState to notify listeners of phase change.
    /// </summary>
    public void NotifyBossPhaseChanged(int phase, BossPhase phaseData)
    {
        OnBossPhaseChanged?.Invoke(phase, phaseData);
    }

    /// <summary>
    /// Force kill this enemy (for debug or encounter end).
    /// </summary>
    public void ForceKill()
    {
        if (!IsAlive)
            return;

        _health.TakeDamage(_health.MaxHealth * 10, DamageType.Generic, null);
    }

    /// <summary>
    /// Request a state change.
    /// </summary>
    public bool RequestStateChange(EnemyState newState)
    {
        return _stateMachine?.RequestStateChange(newState) ?? false;
    }

    /// <summary>
    /// Force a state change without validation.
    /// </summary>
    public void ForceState(EnemyState newState)
    {
        _stateMachine?.ForceState(newState);
    }

    private void HandleHealthChanged(int current, int max)
    {
        // Only trigger hurt if not already hurting or dead
        if (currentState != EnemyState.Hurt &&
            currentState != EnemyState.Dead &&
            current < max) // Actually took damage
        {
            // Small chance to stagger (prevents constant interruption)'
            if (UnityEngine.Random.value <= archetype.StaggerChance)
            {
                RequestStateChange(EnemyState.Hurt);
            }
        }
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnHealthChanged -= HandleHealthChanged;
        }

        if (_stateMachine != null)
        {
            _stateMachine.OnStateChanged -= HandleStateChanged;
        }

        // Unregister from encounter director
        EncounterDirector.Instance?.UnregisterEnemy(this);
    }

#if UNITY_EDITOR
    [Button("Force Chase Player"), BoxGroup("Debug")]
    private void DebugChasePlayer()
    {
        if (!Application.isPlaying) return;

        _context.CurrentTarget = PlayerManager.Instance?.transform;
        RequestStateChange(EnemyState.Chase);
    }

    [Button("Force Attack"), BoxGroup("Debug")]
    private void DebugForceAttack()
    {
        if (!Application.isPlaying) return;
        RequestStateChange(EnemyState.Attack);
    }

    [Button("Force Kill"), BoxGroup("Debug")]
    private void DebugForceKill()
    {
        if (!Application.isPlaying) return;
        ForceKill();
    }

    [Button("Log Context"), BoxGroup("Debug")]
    private void DebugLogContext()
    {
        if (_context == null)
        {
            Debug.Log("Context is null");
            return;
        }

        Debug.Log($"Target: {_context.CurrentTarget?.name ?? "none"}");
        Debug.Log($"Distance to target: {_context.GetDistanceToTarget():F2}");
        Debug.Log($"In attack range: {_context.IsInAttackRange()}");
        Debug.Log($"Can use primary ability: {_context.CanUsePrimaryAbility()}");
    }

    private void OnDrawGizmosSelected()
    {
        if (archetype == null) return;

        // Sight range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, archetype.SightRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, archetype.AttackRange);

        // Hearing range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, archetype.HearingRange);

        // Target line
        if (_context?.CurrentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _context.CurrentTarget.position);
        }
    }
#endif
}