using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Central coordinator for companion behavior.
/// Manages state transitions and delegates to subsystems.
///
/// <para><b>Extension Points:</b></para>
/// <list type="bullet">
///   <item><see cref="OnCompanionActivated"/> - Subscribe to react when companion becomes active</item>
///   <item><see cref="OnCompanionDeactivated"/> - Subscribe to react when companion is deactivated</item>
///   <item><see cref="OnStateChanged"/> - Subscribe to track all state transitions</item>
///   <item><see cref="OnCompanionMoved"/> - Subscribe to track teleportation events</item>
/// </list>
///
/// <para><b>State Machine:</b></para>
/// <para>Use <see cref="RequestStateChange"/> for validated transitions or <see cref="ForceState"/> for edge cases.</para>
///
/// <para><b>Multiple Companion Support (Future):</b></para>
/// <para>Create a CompanionManager singleton that tracks all CompanionCoreController instances.
/// Register in Start(), unregister in OnDestroy().</para>
/// </summary>
public class CompanionCoreController : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [Required]
    [SerializeField] private CompanionData companionData;

    [BoxGroup("References")]
    [SerializeField] private Transform visualRoot;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private NavMeshAgent navAgent;

    [BoxGroup("References")]
    [SerializeField] private Collider interactionCollider;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionInventory inventory;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionMovementController movementController;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionCallHandler callHandler;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionAutoDeposit autoDepositController;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isActive = true;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Transform targetPlayerTransform;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private CompanionState currentState = CompanionState.Inactive;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private CompanionState previousState = CompanionState.Inactive;

    // State Machine
    private StateMachine<CompanionState, CompanionContext> _stateMachine;
    private CompanionContext _context;

    // Events
    public event Action OnCompanionActivated;
    public event Action OnCompanionDeactivated;
    public event Action<Vector3> OnCompanionMoved;
    public event Action<CompanionState, CompanionState> OnStateChanged;

    // Public Accessors
    public CompanionData Data => companionData;
    public bool IsActive => isActive;
    public Transform TargetPlayerTransform => targetPlayerTransform;
    public NavMeshAgent NavAgent => navAgent;
    public Vector3 Position => transform.position;
    public CompanionInventory Inventory => inventory;
    public CompanionMovementController MovementController => movementController;
    public CompanionAutoDeposit AutoDepositController => autoDepositController;
    public CompanionCallHandler CallHandler => callHandler;
    public CompanionState CurrentState => currentState;
    public CompanionState PreviousState => previousState;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogError("NavMeshAgent component is missing on CompanionCoreController.");
            return;
        }

        // Apply data settings to NavMeshAgent
        if (companionData != null)
        {
            navAgent.speed = companionData.MoveSpeed;
            navAgent.stoppingDistance = companionData.ArrivalDistance;
        }
        else
        {
            Debug.LogError("CompanionData reference is missing in CompanionCoreController.");
        }
    }

    private void Start()
    {
        // Find player
        targetPlayerTransform = PlayerManager.Instance.transform;
        if (targetPlayerTransform == null)
        {
            Debug.LogError("Player Transform not found for CompanionCoreController.");
            return;
        }

        // Initialize state machine
        InitializeStateMachine();

        // Start active
        SetActive(true);
    }

    private void InitializeStateMachine()
    {
        // Create context
        _context = new CompanionContext(
            this,
            companionData,
            transform,
            navAgent,
            movementController,
            inventory,
            autoDepositController,
            callHandler
        );
        _context.PlayerTransform = targetPlayerTransform;

        // Create state machine
        _stateMachine = new StateMachine<CompanionState, CompanionContext>(_context);

        // Register states
        _stateMachine.RegisterState(CompanionState.Inactive, new CompanionInactiveState());
        _stateMachine.RegisterState(CompanionState.Idle, new CompanionIdleState());
        _stateMachine.RegisterState(CompanionState.BeingCalled, new CompanionBeingCalledState());
        _stateMachine.RegisterState(CompanionState.FollowingPlayer, new CompanionFollowingPlayerState());
        _stateMachine.RegisterState(CompanionState.MovingToDepot, new CompanionMovingToDepotState());
        _stateMachine.RegisterState(CompanionState.Depositing, new CompanionDepositingState());
        _stateMachine.RegisterState(CompanionState.ReturningToPlayer, new CompanionReturningToPlayerState());

        // Register valid transitions
        RegisterStateTransitions();

        // Subscribe to state changes to update our cached state and fire events
        _stateMachine.OnStateChanged += HandleStateMachineStateChanged;

        // Initialize in Inactive state
        _stateMachine.Initialize(CompanionState.Inactive);
    }

    private void RegisterStateTransitions()
    {
        // From Inactive
        _stateMachine.RegisterTransitions(CompanionState.Inactive,
            CompanionState.BeingCalled,
            CompanionState.Idle);

        // From Idle
        _stateMachine.RegisterTransitions(CompanionState.Idle,
            CompanionState.BeingCalled,
            CompanionState.FollowingPlayer,
            CompanionState.MovingToDepot,
            CompanionState.Inactive);

        // From BeingCalled
        _stateMachine.RegisterTransitions(CompanionState.BeingCalled,
            CompanionState.FollowingPlayer,
            CompanionState.Idle,
            CompanionState.Inactive);

        // From FollowingPlayer
        _stateMachine.RegisterTransitions(CompanionState.FollowingPlayer,
            CompanionState.Idle,
            CompanionState.MovingToDepot,
            CompanionState.BeingCalled,
            CompanionState.Inactive);

        // From MovingToDepot
        _stateMachine.RegisterTransitions(CompanionState.MovingToDepot,
            CompanionState.Depositing,
            CompanionState.BeingCalled,
            CompanionState.FollowingPlayer,
            CompanionState.Inactive);

        // From Depositing
        _stateMachine.RegisterTransitions(CompanionState.Depositing,
            CompanionState.ReturningToPlayer,
            CompanionState.FollowingPlayer,
            CompanionState.BeingCalled,
            CompanionState.Inactive);

        // From ReturningToPlayer
        _stateMachine.RegisterTransitions(CompanionState.ReturningToPlayer,
            CompanionState.FollowingPlayer,
            CompanionState.Idle,
            CompanionState.BeingCalled,
            CompanionState.Inactive);
    }

    private void HandleStateMachineStateChanged(CompanionState from, CompanionState to)
    {
        previousState = from;
        currentState = to;
        _context.OnStateEnter();

        OnStateChanged?.Invoke(from, to);
        Debug.Log($"[CompanionController] State: {from} -> {to}");
    }

    private void Update()
    {
        // Handle inputs
        HandleAllInputs();

        // Update state machine
        _stateMachine?.Update();
    }

    private void FixedUpdate()
    {
        _stateMachine?.FixedUpdate();
    }

    private void HandleAllInputs()
    {
        if (PlayerManager.Instance.InputHandler.CallCompanionPressed)
        {
            callHandler.CallCompanion();
            PlayerManager.Instance.InputHandler.CallCompanionPressed = false;
        }
    }

    /// <summary>
    /// Activate the companion. Called when spawned or enabled.
    /// </summary>
    public void Activate()
    {
        if (isActive) return;

        // Retry finding player if needed
        if (targetPlayerTransform == null)
        {
            targetPlayerTransform = PlayerManager.Instance?.transform;
        }

        if (targetPlayerTransform == null)
        {
            Debug.LogError("[CompanionController] Cannot activate: No player found!");
            return;
        }

        isActive = true;

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
        }

        // Update context
        if (_context != null)
        {
            _context.PlayerTransform = targetPlayerTransform;
        }

        // Force to Idle state
        _stateMachine?.ForceState(CompanionState.Idle);

        OnCompanionActivated?.Invoke();

        Debug.Log("[CompanionController] Companion activated");
    }

    /// <summary>
    /// Deactivate the companion. Hides but doesn't destroy.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return;

        isActive = false;

        // Stop movement
        StopNavigation();

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = false;
        }

        // Force to Inactive state
        _stateMachine?.ForceState(CompanionState.Inactive);

        OnCompanionDeactivated?.Invoke();

        Debug.Log("[CompanionController] Companion deactivated");
    }

    /// <summary>
    /// Set companion active state.
    /// </summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Teleport companion to a position (must be on NavMesh).
    /// </summary>
    public bool TeleportTo(Vector3 position)
    {
        if (!isActive) return false;

        // Validate position is on a NavMesh Surface
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[CompanionController] Cannot teleport: position not on NavMesh ({position})");
            return false;
        }

        // Stop current navigation
        StopNavigation();

        // Warp to position
        navAgent.Warp(hit.position);

        OnCompanionMoved?.Invoke(hit.position);
        Debug.Log($"[CompanionController] Teleported to {hit.position}");
        return true;
    }

    /// <summary>
    /// Get distance to player.
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (targetPlayerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, targetPlayerTransform.position);
    }

    /// <summary>
    /// Check if companion is within interaction range of player.
    /// </summary>
    public bool IsWithinInteractionRange()
    {
        if (companionData == null) return false;
        return GetDistanceToPlayer() <= companionData.InteractionRange;
    }

    public void StopNavigation()
    {
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }
    }

    #region State Machine Public API

    /// <summary>
    /// Request a state change. Validates transition and invokes callbacks.
    /// </summary>
    public bool RequestStateChange(CompanionState newState)
    {
        if (_stateMachine == null)
        {
            Debug.LogError("[CompanionController] State machine not initialized");
            return false;
        }

        return _stateMachine.RequestStateChange(newState);
    }

    /// <summary>
    /// Force a state change without validation (use sparingly).
    /// </summary>
    public void ForceState(CompanionState newState)
    {
        if (_stateMachine == null)
        {
            Debug.LogError("[CompanionController] State machine not initialized");
            return;
        }

        _stateMachine.ForceState(newState);
    }

    /// <summary>
    /// Check if a transition is valid.
    /// </summary>
    public bool IsValidTransition(CompanionState from, CompanionState to)
    {
        return _stateMachine?.IsValidTransition(from, to) ?? false;
    }

    /// <summary>
    /// Pause the state machine (useful for cutscenes, menus, etc.)
    /// </summary>
    public void PauseStateMachine()
    {
        _stateMachine?.Pause();
    }

    /// <summary>
    /// Resume the state machine.
    /// </summary>
    public void ResumeStateMachine()
    {
        _stateMachine?.Resume();
    }

    #endregion

#if UNITY_EDITOR
    [Button("Activate"), BoxGroup("Debug")]
    private void DebugActivate()
    {
        if (Application.isPlaying)
        {
            Activate();
        }
    }

    [Button("Deactivate"), BoxGroup("Debug")]
    private void DebugDeactivate()
    {
        if (Application.isPlaying)
        {
            Deactivate();
        }
    }

    [Button("Teleport to Player"), BoxGroup("Debug")]
    private void DebugTeleportToPlayer()
    {
        if (Application.isPlaying && targetPlayerTransform != null)
        {
            TeleportTo(targetPlayerTransform.position + targetPlayerTransform.forward * 3f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (companionData == null) return;

        // Draw interaction range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, companionData.InteractionRange);

        // Draw follow distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, companionData.FollowDistance);
    }

    [Button("To Idle"), BoxGroup("Debug/States")]
    private void DebugToIdle() => RequestStateChange(CompanionState.Idle);

    [Button("To Following"), BoxGroup("Debug/States")]
    private void DebugToFollowing() => RequestStateChange(CompanionState.FollowingPlayer);

    [Button("To MovingToDepot"), BoxGroup("Debug/States")]
    private void DebugToMovingToDepot() => RequestStateChange(CompanionState.MovingToDepot);

    [Button("Pause SM"), BoxGroup("Debug/States")]
    private void DebugPauseSM() => PauseStateMachine();

    [Button("Resume SM"), BoxGroup("Debug/States")]
    private void DebugResumeSM() => ResumeStateMachine();

    [ShowInInspector, BoxGroup("Debug/States"), ReadOnly]
    private bool IsStateMachineInitialized => _stateMachine?.IsInitialized ?? false;

    [ShowInInspector, BoxGroup("Debug/States"), ReadOnly]
    private bool IsStateMachinePaused => _stateMachine?.IsPaused ?? false;
#endif
}
