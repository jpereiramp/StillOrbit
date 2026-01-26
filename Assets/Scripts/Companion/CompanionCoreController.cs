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

        // Start active
        SetActive(true);
    }

    private void Update()
    {
        // Handle inputs
        HandleAllInputs();
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

        currentState = CompanionState.Idle;
        OnStateChanged?.Invoke(CompanionState.Inactive, CompanionState.Idle);
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

        previousState = currentState;
        currentState = CompanionState.Inactive;

        OnStateChanged?.Invoke(previousState, currentState);
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

        // Validate position is on a NavMesh Surfaace
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

    #region State Machine
    /// <summary>
    /// Request a state change. Validates transition and invokes callbacks.
    /// </summary>
    public bool RequestStateChange(CompanionState newState)
    {
        if (currentState == newState) return true;

        // Validate transition
        if (!IsValidTransition(currentState, newState))
        {
            Debug.LogWarning($"[CompanionController] Invalid state transition: {currentState} -> {newState}");
            return false;
        }

        // Exit current state
        OnStateExit(currentState);

        // Change state
        previousState = currentState;
        currentState = newState;

        // Enter new state
        OnStateEnter(newState);

        // Fire event
        OnStateChanged?.Invoke(previousState, currentState);
        Debug.Log($"[CompanionController] State: {previousState} -> {currentState}");

        return true;
    }

    /// <summary>
    /// Force a state change without validation (use sparingly).
    /// </summary>
    public void ForceState(CompanionState newState)
    {
        if (currentState == newState) return;

        OnStateExit(currentState);
        previousState = currentState;
        currentState = newState;
        OnStateEnter(newState);
        OnStateChanged?.Invoke(previousState, currentState);
        Debug.Log($"[CompanionController] State forced: {previousState} -> {currentState}");
    }

    private bool IsValidTransition(CompanionState from, CompanionState to)
    {
        // Define valid transitions
        switch (from)
        {
            case CompanionState.Inactive:
                // Can only go to BeingCalled or Idle from inactive
                return to == CompanionState.BeingCalled || to == CompanionState.Idle;

            case CompanionState.Idle:
                // From idle, can be called, start following, or go to depot
                return to == CompanionState.BeingCalled
                    || to == CompanionState.FollowingPlayer
                    || to == CompanionState.MovingToDepot
                    || to == CompanionState.Inactive;

            case CompanionState.BeingCalled:
                // After being called, go to following
                return to == CompanionState.FollowingPlayer
                    || to == CompanionState.Idle
                    || to == CompanionState.Inactive;

            case CompanionState.FollowingPlayer:
                // While following, can go idle, move to depot, or be called again
                return to == CompanionState.Idle
                    || to == CompanionState.MovingToDepot
                    || to == CompanionState.BeingCalled
                    || to == CompanionState.Inactive;

            case CompanionState.MovingToDepot:
                // Moving to depot leads to depositing, or can be called back
                return to == CompanionState.Depositing
                    || to == CompanionState.BeingCalled
                    || to == CompanionState.FollowingPlayer
                    || to == CompanionState.Inactive;

            case CompanionState.Depositing:
                // After depositing, return to player or follow
                return to == CompanionState.ReturningToPlayer
                    || to == CompanionState.FollowingPlayer
                    || to == CompanionState.BeingCalled
                    || to == CompanionState.Inactive;

            case CompanionState.ReturningToPlayer:
                // After returning, follow or go idle
                return to == CompanionState.FollowingPlayer
                    || to == CompanionState.Idle
                    || to == CompanionState.BeingCalled
                    || to == CompanionState.Inactive;

            default:
                return false;
        }
    }

    private void OnStateEnter(CompanionState state)
    {
        switch (state)
        {
            case CompanionState.Inactive:
                movementController.Stop();
                break;

            case CompanionState.Idle:
                // Stop movement
                movementController.Stop();
                break;

            case CompanionState.BeingCalled:
                // Will be handled by spawn logic
                break;

            case CompanionState.FollowingPlayer:
                movementController.StartFollowingPlayer();
                break;

            case CompanionState.MovingToDepot:
                // Will set destination in auto-deposit logic
                break;

            case CompanionState.Depositing:
                // Will handle in depositing logic
                movementController.Stop();
                break;

            case CompanionState.ReturningToPlayer:
                if (targetPlayerTransform != null)
                {
                    movementController.SetDestination(targetPlayerTransform.position);
                }
                break;
        }
    }

    private void OnStateExit(CompanionState state)
    {
        // Cleanup when leaving a state
        switch (state)
        {
            case CompanionState.BeingCalled:
                // Nothing special
                break;

            case CompanionState.MovingToDepot:
                // Clear depot target if needed
                break;

            case CompanionState.Depositing:
                // Finalize deposit
                break;
        }
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
#endif
}