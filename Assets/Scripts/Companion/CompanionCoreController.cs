using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

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

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isActive = true;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private Transform targetPlayerTransform;

    // Events
    public event Action OnCompanionActivated;
    public event Action OnCompanionDeactivated;
    public event Action<Vector3> OnCompanionMoved;

    // Public Accessors
    public CompanionData Data => companionData;
    public bool IsActive => isActive;
    public Transform TargetPlayerTransform => targetPlayerTransform;
    public NavMeshAgent NavAgent => navAgent;
    public Vector3 Position => transform.position;
    public CompanionInventory Inventory => inventory;

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
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = false;
        }

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
        if (navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }

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
#endif
}