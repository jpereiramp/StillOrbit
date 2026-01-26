using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles all movement and navigation for the companion.
/// Uses NavMeshAgent for pathfinding.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CompanionMovementController : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionCoreController controller;

    [BoxGroup("References")]
    [Required]
    [SerializeField] private NavMeshAgent navAgent;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Vector3 currentDestination;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool hasDestination = false;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private MovementMode currentMode = MovementMode.None;

    public enum MovementMode
    {
        None,
        FollowPlayer,
        MoveToDestination
    }

    // Components
    private CompanionData data;

    // Events
    public event Action OnDestinationReached;
    public event Action OnDestinationUnreachable;

    // Public accessors
    public bool HasDestination => hasDestination;
    public Vector3 CurrentDestination => currentDestination;
    public MovementMode CurrentMode => currentMode;
    public bool IsMoving => navAgent != null && navAgent.velocity.magnitude > 0.1f;
    public float RemainingDistance => navAgent != null ? navAgent.remainingDistance : float.MaxValue;

    private void Start()
    {
        if (controller == null)
        {
            Debug.LogError("CompanionCoreController reference is missing in CompanionMovementController.");
            return;
        }
        data = controller.Data;
    }

    private void Update()
    {
        if (controller == null || !controller.IsActive) return;

        switch (currentMode)
        {
            case MovementMode.FollowPlayer:
                UpdateFollowPlayer();
                break;

            case MovementMode.MoveToDestination:
                UpdateMoveToDestination();
                break;
        }
    }

    /// <summary>
    /// Start following the player, maintaining follow distance.
    /// </summary>
    public void StartFollowingPlayer()
    {
        currentMode = MovementMode.FollowPlayer;
        hasDestination = false;
        Debug.Log("[CompanionMovement] Started following player");
    }

    /// <summary>
    /// Move to a specific world position.
    /// </summary>
    public bool SetDestination(Vector3 destination)
    {
        if (!navAgent.isOnNavMesh)
        {
            Debug.LogWarning("[CompanionMovement] Agent not on NavMesh");
            return false;
        }

        // Validate destination is reachable
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning("[CompanionMovement] Destination not on NavMesh");
            OnDestinationUnreachable?.Invoke();
            return false;
        }

        currentDestination = hit.position;
        hasDestination = true;
        currentMode = MovementMode.MoveToDestination;

        bool pathSet = navAgent.SetDestination(currentDestination);

        if (!pathSet)
        {
            Debug.LogWarning("[CompanionMovement] Failed to set destination path");
            OnDestinationUnreachable?.Invoke();
            return false;
        }

        Debug.Log($"[CompanionMovement] Moving to {currentDestination}");
        return true;
    }

    /// <summary>
    /// Stop all movement immediately.
    /// </summary>
    public void Stop()
    {
        currentMode = MovementMode.None;
        hasDestination = false;

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        Debug.Log("[CompanionMovement] Stopped");
    }

    /// <summary>
    /// Check if companion has arrived at current destination.
    /// </summary>
    public bool HasArrivedAtDestination()
    {
        if (!hasDestination) return false;
        if (navAgent == null) return false;

        // Check if path is complete and we're close enough
        if (!navAgent.pathPending)
        {
            float arrivalDist = data != null ? data.ArrivalDistance : 1.5f;

            if (navAgent.remainingDistance <= arrivalDist)
            {
                if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.01f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if companion is close enough to player.
    /// </summary>
    public bool IsNearPlayer(float threshold = -1f)
    {
        if (controller == null || controller.TargetPlayerTransform == null) return false;

        if (threshold < 0)
        {
            threshold = data != null ? data.FollowDistance : 3f;
        }

        float distance = Vector3.Distance(transform.position, controller.TargetPlayerTransform.position);
        return distance <= threshold;
    }

    private void UpdateFollowPlayer()
    {
        if (controller.TargetPlayerTransform == null) return;

        float followDist = data != null ? data.FollowDistance : 3f;
        float currentDist = Vector3.Distance(transform.position, controller.TargetPlayerTransform.position);

        // Only move if we're too far from player
        if (currentDist > followDist * 1.5f)
        {
            // Calculate position behind player
            Vector3 targetPos = controller.TargetPlayerTransform.position -
                               controller.TargetPlayerTransform.forward * followDist;

            // Validate and set destination
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                if (navAgent.isOnNavMesh)
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }
        else if (currentDist < followDist * 0.5f)
        {
            // Too close, stop
            if (navAgent.isOnNavMesh && navAgent.hasPath)
            {
                Stop();
            }
        }
    }

    private void UpdateMoveToDestination()
    {
        if (!hasDestination) return;

        // Check for arrival
        if (HasArrivedAtDestination())
        {
            hasDestination = false;
            currentMode = MovementMode.None;
            OnDestinationReached?.Invoke();
            Debug.Log("[CompanionMovement] Arrived at destination");
        }

        // Check for path failure
        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            hasDestination = false;
            currentMode = MovementMode.None;
            OnDestinationUnreachable?.Invoke();
            Debug.LogWarning("[CompanionMovement] Path became invalid");
        }
    }

#if UNITY_EDITOR
    [Button("Follow Player"), BoxGroup("Debug")]
    private void DebugFollowPlayer()
    {
        if (Application.isPlaying)
        {
            StartFollowingPlayer();
        }
    }

    [Button("Stop"), BoxGroup("Debug")]
    private void DebugStop()
    {
        if (Application.isPlaying)
        {
            Stop();
        }
    }

    [Button("Move to Random"), BoxGroup("Debug")]
    private void DebugMoveToRandom()
    {
        if (Application.isPlaying)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * 10f;
            randomOffset.y = 0;
            SetDestination(transform.position + randomOffset);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, 0.5f);
        }

        if (controller != null && controller.TargetPlayerTransform != null && data != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(controller.TargetPlayerTransform.position, data.FollowDistance);
        }
    }
#endif
}