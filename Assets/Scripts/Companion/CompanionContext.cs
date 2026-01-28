using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared context data accessible by all companion states.
/// This is the "blackboard" for the companion's state machine.
/// States should be stateless - all per-instance data lives here.
/// </summary>
public class CompanionContext
{
    // Core References
    public CompanionCoreController Controller { get; }
    public CompanionData Data { get; }
    public Transform Transform { get; }
    public NavMeshAgent NavAgent { get; }

    // Subsystem References
    public CompanionMovementController Movement { get; }
    public CompanionInventory Inventory { get; }
    public CompanionAutoDeposit AutoDeposit { get; }
    public CompanionCallHandler CallHandler { get; }

    // Player Reference
    public Transform PlayerTransform { get; set; }

    // State Timing
    public float StateEnterTime { get; set; }
    public float TimeSinceStateEnter => Time.time - StateEnterTime;

    public CompanionContext(
        CompanionCoreController controller,
        CompanionData data,
        Transform transform,
        NavMeshAgent navAgent,
        CompanionMovementController movement,
        CompanionInventory inventory,
        CompanionAutoDeposit autoDeposit,
        CompanionCallHandler callHandler)
    {
        Controller = controller;
        Data = data;
        Transform = transform;
        NavAgent = navAgent;
        Movement = movement;
        Inventory = inventory;
        AutoDeposit = autoDeposit;
        CallHandler = callHandler;
    }

    /// <summary>
    /// Get distance to player.
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (PlayerTransform == null)
            return float.MaxValue;
        return Vector3.Distance(Transform.position, PlayerTransform.position);
    }

    /// <summary>
    /// Check if companion is within interaction range of player.
    /// </summary>
    public bool IsWithinInteractionRange()
    {
        if (Data == null)
            return false;
        return GetDistanceToPlayer() <= Data.InteractionRange;
    }

    /// <summary>
    /// Reset state timing on state enter.
    /// </summary>
    public void OnStateEnter()
    {
        StateEnterTime = Time.time;
    }
}
