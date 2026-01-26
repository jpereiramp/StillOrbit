using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles automatic resource depositing after idle timeout.
/// Finds nearest depot, navigates to it, deposits resources, then returns.
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
    }

    private void Update()
    {
        if (controller == null || !controller.IsActive) return;

        // Only tick idle timer in FollowingPlayer state
        if (controller.CurrentState == CompanionState.FollowingPlayer)
        {
            UpdateIdleTimer();
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

    private IResourceStorage FindNearestDepot()
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