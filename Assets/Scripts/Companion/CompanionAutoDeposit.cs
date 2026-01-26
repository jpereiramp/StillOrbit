using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles automatic resource depositing after idle timeout.
/// Finds nearest depot, navigates to it, deposits resources, then returns.
///
/// <para><b>Extension Points:</b></para>
/// <list type="bullet">
///   <item><see cref="OnAutoDepositStarted"/> - Subscribe to react when auto-deposit begins</item>
///   <item><see cref="OnAutoDepositCompleted"/> - Subscribe to react when deposit cycle finishes</item>
///   <item><see cref="OnNoDepotFound"/> - Subscribe to handle no-depot scenarios (e.g., show UI warning)</item>
///   <item><see cref="FindNearestDepot"/> - Override in subclass for custom depot selection (priority, type filtering)</item>
/// </list>
///
/// <para><b>Integration:</b></para>
/// <list type="bullet">
///   <item>Uses <see cref="BuildingRegistry"/> for depot discovery</item>
///   <item>Subscribes to <see cref="BuildingRegistry.OnBuildingRemoved"/> for depot destruction handling</item>
///   <item>Works with any <see cref="IResourceStorage"/> implementation</item>
/// </list>
/// </summary>
public class CompanionAutoDeposit : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private CompanionCoreController controller;

    [BoxGroup("References")]
    [SerializeField] private CompanionInventory inventory;

    [BoxGroup("References")]
    [SerializeField] private CompanionMovementController movement;

    [BoxGroup("References")]
    [SerializeField] private CompanionInteractionHandler interaction;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private float idleTimer = 0f;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isAutoDepositTriggered = false;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private IResourceStorage targetDepot;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Transform targetDepotTransform;

    private CompanionData data;

    // Events
    public event Action OnAutoDepositStarted;
    public event Action OnAutoDepositCompleted;
    public event Action OnNoDepotFound;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CompanionCoreController>();
        if (inventory == null) inventory = GetComponent<CompanionInventory>();
        if (movement == null) movement = GetComponent<CompanionMovementController>();
        if (interaction == null) interaction = GetComponent<CompanionInteractionHandler>();
    }

    private void Start()
    {
        if (controller != null)
        {
            data = controller.Data;

            // Subscribe to events
            controller.OnStateChanged += HandleStateChanged;
        }

        if (interaction != null)
        {
            interaction.OnResourcesDeposited += HandleResourcesDeposited;
        }

        if (movement != null)
        {
            movement.OnDestinationReached += HandleDestinationReached;
        }

        // Phase 8: Subscribe to BuildingRegistry events for depot destruction handling
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRemoved += HandleBuildingRemoved;
        }
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnStateChanged -= HandleStateChanged;
        }

        if (interaction != null)
        {
            interaction.OnResourcesDeposited -= HandleResourcesDeposited;
        }

        if (movement != null)
        {
            movement.OnDestinationReached -= HandleDestinationReached;
        }

        // Phase 8: Unsubscribe from BuildingRegistry events
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
        }
    }

    private void FixedUpdate()
    {
        if (controller == null || !controller.IsActive) return;

        // Check for state-specific updates
        switch (controller.CurrentState)
        {
            case CompanionState.FollowingPlayer:
                UpdateIdleTimer();
                break;

            case CompanionState.MovingToDepot:
                // Phase 8: Validate depot during navigation
                ValidateTargetDepot();
                break;
        }
    }

    private void UpdateIdleTimer()
    {
        // Only count down if we have resources to deposit
        if (inventory == null || !inventory.HasAnyResources())
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;

        float threshold = data?.IdleTimeBeforeAutoDeposit ?? 5f;

        if (idleTimer >= threshold && !isAutoDepositTriggered)
        {
            TriggerAutoDeposit();
        }
    }

    /// <summary>
    /// Reset the idle timer (called when player interacts).
    /// </summary>
    public void ResetIdleTimer()
    {
        idleTimer = 0f;
        isAutoDepositTriggered = false;
    }

    /// <summary>
    /// Start the auto-deposit sequence.
    /// </summary>
    public void TriggerAutoDeposit()
    {
        if (isAutoDepositTriggered) return;
        if (inventory == null || !inventory.HasAnyResources()) return;

        // Find nearest depot
        IResourceStorage depot = FindNearestDepot();

        if (depot == null)
        {
            Debug.LogWarning("[CompanionAutoDeposit] No depot found within range");
            OnNoDepotFound?.Invoke();
            ResetIdleTimer();
            return;
        }

        // Get depot transform
        targetDepot = depot;
        targetDepotTransform = (depot as MonoBehaviour)?.transform;

        if (targetDepotTransform == null)
        {
            Debug.LogError("[CompanionAutoDeposit] Depot has no transform");
            ResetIdleTimer();
            return;
        }

        isAutoDepositTriggered = true;
        OnAutoDepositStarted?.Invoke();

        // Change state and start moving
        controller.RequestStateChange(CompanionState.MovingToDepot);
        movement?.SetDestination(targetDepotTransform.position);

        Debug.Log($"[CompanionAutoDeposit] Moving to depot at {targetDepotTransform.position}");
    }

    /// <summary>
    /// Cancel auto-deposit and return to following.
    /// </summary>
    public void CancelAutoDeposit()
    {
        if (!isAutoDepositTriggered) return;

        isAutoDepositTriggered = false;
        targetDepot = null;
        targetDepotTransform = null;

        movement?.Stop();
        controller.RequestStateChange(CompanionState.FollowingPlayer);

        ResetIdleTimer();
        Debug.Log("[CompanionAutoDeposit] Auto-deposit cancelled");
    }

    /// <summary>
    /// Find the nearest valid depot within search radius.
    /// Override this method to implement custom depot selection logic
    /// (e.g., prioritize by resource type, prefer certain depot types, filter by capacity).
    /// </summary>
    /// <returns>The nearest <see cref="IResourceStorage"/> or null if none found within range.</returns>
    protected virtual IResourceStorage FindNearestDepot()
    {
        float searchRadius = data?.DepotSearchRadius ?? 50f;

        // Use BuildingRegistry to find nearest storage
        var depot = BuildingRegistry.Instance?.FindNearest<IResourceStorage>(transform.position);

        if (depot == null) return null;

        // Check if within search radius
        var depotTransform = (depot as MonoBehaviour)?.transform;
        if (depotTransform == null) return null;

        float distance = Vector3.Distance(transform.position, depotTransform.position);
        if (distance > searchRadius) return null;

        return depot;
    }

    private void HandleStateChanged(CompanionState previous, CompanionState current)
    {
        // Reset timer when entering follow state
        if (current == CompanionState.FollowingPlayer)
        {
            ResetIdleTimer();
        }

        // Handle being called while depositing
        if (current == CompanionState.BeingCalled && isAutoDepositTriggered)
        {
            CancelAutoDeposit();
        }
    }

    private void HandleResourcesDeposited(int amount)
    {
        // Player deposited resources, reset timer
        ResetIdleTimer();
    }

    private void HandleDestinationReached()
    {
        // Check if we're in the right state
        if (controller.CurrentState == CompanionState.MovingToDepot)
        {
            PerformDeposit();
        }
        else if (controller.CurrentState == CompanionState.ReturningToPlayer)
        {
            FinishReturn();
        }
    }

    #region Phase 8: BuildingRegistry Integration

    /// <summary>
    /// Handle when a building is removed from the registry.
    /// If our target depot was destroyed, find an alternative or cancel.
    /// </summary>
    private void HandleBuildingRemoved(Building building)
    {
        // Check if our target depot was removed
        if (!isAutoDepositTriggered || targetDepot == null) return;

        var targetMono = targetDepot as MonoBehaviour;
        if (targetMono == null) return;

        if (targetMono.gameObject == building.gameObject)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Target depot was destroyed!");
            RetryOrCancel();
        }
    }

    /// <summary>
    /// Validate that the target depot still exists and is operational.
    /// Called each frame during MovingToDepot state.
    /// </summary>
    private void ValidateTargetDepot()
    {
        // Ensure depot reference still exists
        if (targetDepot == null || targetDepotTransform == null)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Target depot became null");
            RetryOrCancel();
            return;
        }

        // Check if depot GameObject is still active
        var targetMono = targetDepot as MonoBehaviour;
        if (targetMono == null || !targetMono.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Target depot is no longer active");
            RetryOrCancel();
            return;
        }

        // Check if depot is still operational (via Building component)
        var building = targetDepotTransform.GetComponent<Building>();
        if (building != null && !building.IsOperational)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Target depot is not operational");
            RetryOrCancel();
        }
    }

    /// <summary>
    /// Try to find an alternative depot, or cancel auto-deposit if none available.
    /// </summary>
    private void RetryOrCancel()
    {
        // Clear current target
        var previousDepot = targetDepot;
        targetDepot = null;
        targetDepotTransform = null;

        // Try to find another depot
        IResourceStorage newDepot = FindNearestDepot();

        if (newDepot != null && newDepot != previousDepot)
        {
            targetDepot = newDepot;
            targetDepotTransform = (newDepot as MonoBehaviour)?.transform;

            if (targetDepotTransform != null)
            {
                movement?.SetDestination(targetDepotTransform.position);
                Debug.Log($"[CompanionAutoDeposit] Redirecting to alternative depot at {targetDepotTransform.position}");
                return;
            }
        }

        // No alternative found, cancel and return to player
        Debug.Log("[CompanionAutoDeposit] No alternative depot found, cancelling auto-deposit");
        CancelAutoDeposit();
    }

    #endregion

    private void PerformDeposit()
    {
        if (targetDepot == null || inventory == null)
        {
            Debug.LogWarning("[CompanionAutoDeposit] Cannot deposit: missing depot or inventory");
            ReturnToPlayer();
            return;
        }

        controller.RequestStateChange(CompanionState.Depositing);

        // Transfer resources
        int deposited = inventory.TransferAllTo(targetDepot);

        Debug.Log($"[CompanionAutoDeposit] Deposited {deposited} resources at depot");

        // Small delay then return (could add animation here)
        Invoke(nameof(ReturnToPlayer), 0.5f);
    }

    private void ReturnToPlayer()
    {
        targetDepot = null;
        targetDepotTransform = null;
        isAutoDepositTriggered = false;

        if (controller.TargetPlayerTransform == null)
        {
            controller.RequestStateChange(CompanionState.Idle);
            return;
        }

        controller.RequestStateChange(CompanionState.ReturningToPlayer);
        movement?.SetDestination(controller.TargetPlayerTransform.position);

        Debug.Log("[CompanionAutoDeposit] Returning to player");
    }

    private void FinishReturn()
    {
        controller.RequestStateChange(CompanionState.FollowingPlayer);
        OnAutoDepositCompleted?.Invoke();

        Debug.Log("[CompanionAutoDeposit] Auto-deposit complete, now following");
    }

#if UNITY_EDITOR
    [Button("Trigger Auto-Deposit"), BoxGroup("Debug")]
    private void DebugTriggerAutoDeposit()
    {
        if (Application.isPlaying)
        {
            TriggerAutoDeposit();
        }
    }

    [Button("Cancel Auto-Deposit"), BoxGroup("Debug")]
    private void DebugCancelAutoDeposit()
    {
        if (Application.isPlaying)
        {
            CancelAutoDeposit();
        }
    }

    [Button("Reset Idle Timer"), BoxGroup("Debug")]
    private void DebugResetTimer()
    {
        ResetIdleTimer();
    }

    [ShowInInspector, BoxGroup("Debug"), ReadOnly]
    private float TimeUntilAutoDeposit
    {
        get
        {
            if (data == null) return -1;
            return Mathf.Max(0, data.IdleTimeBeforeAutoDeposit - idleTimer);
        }
    }
#endif
}