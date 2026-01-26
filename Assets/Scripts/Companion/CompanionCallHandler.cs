using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles the "call companion" behavior.
/// Spawns companion near player, preferring behind/outside FOV.
/// </summary>
public class CompanionCallHandler : MonoBehaviour
{
    [BoxGroup("References")]
    [Required]
    [SerializeField] private CompanionCoreController controller;

    [BoxGroup("Settings")]
    [SerializeField] private int maxSpawnAttempts = 10;

    [BoxGroup("Settings")]
    [Tooltip("If true, teleports instantly. If false, companion navigates to player.")]
    [SerializeField] private bool teleportWhenCalled = true;

    private CompanionData data;

    private void Start()
    {
        if (controller == null)
        {
            Debug.LogError("CompanionCoreController reference is missing in CompanionCallHandler.");
            return;
        }

        data = controller.Data;
    }

    /// <summary>
    /// Call the companion to the player.
    /// </summary>
    public void CallCompanion()
    {
        if (controller == null || controller.TargetPlayerTransform == null)
        {
            Debug.LogWarning("[CompanionCallHandler] Cannot call: Missing controller or player");
            return;
        }

        // If already active and close, just switch to following
        if (controller.IsActive && controller.CurrentState != CompanionState.Inactive)
        {
            if (controller.GetDistanceToPlayer() < (data?.MaxSpawnDistance ?? 10f))
            {
                // Already nearby, just follow
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.Log("[CompanionCallHandler] Companion already nearby, now following");
                return;
            }
        }

        // Start call sequence
        controller.RequestStateChange(CompanionState.BeingCalled);

        if (teleportWhenCalled)
        {
            // Find spawn position and teleport
            Vector3 spawnPos = CalculateSpawnPosition();

            if (controller.TeleportTo(spawnPos))
            {
                controller.Activate();
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.Log($"[CompanionCallHandler] Companion teleported to {spawnPos}");
            }
            else
            {
                // Fallback: spawn at player position
                controller.TeleportTo(controller.TargetPlayerTransform.position);
                controller.Activate();
                controller.RequestStateChange(CompanionState.FollowingPlayer);
                Debug.LogWarning("[CompanionCallHandler] Fallback spawn at player position");
            }
        }
        else
        {
            // Companion navigates to player
            controller.Activate();
            if (controller.MovementController != null)
            {
                controller.MovementController.SetDestination(controller.TargetPlayerTransform.position);
            }
            // Will transition to Following when arrives
        }
    }

    /// <summary>
    /// Calculate a spawn position near player, preferably behind/outside FOV.
    /// </summary>
    private Vector3 CalculateSpawnPosition()
    {
        Transform player = controller.TargetPlayerTransform;

        float minDist = data?.MinSpawnDistance ?? 5f;
        float maxDist = data?.MaxSpawnDistance ?? 10f;
        float angleRange = data?.BehindPlayerAngleRange ?? 120f;
        bool preferBehind = data?.PreferSpawnBehindPlayer ?? true;

        // Try to find position behind player first
        if (preferBehind)
        {
            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                Vector3 candidate = GetBehindPlayerPosition(player, minDist, maxDist, angleRange);

                if (IsValidSpawnPosition(candidate))
                {
                    return candidate;
                }
            }
        }

        // Fallback: try any direction
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float distance = Random.Range(minDist, maxDist);
            float angle = Random.Range(0f, 360f);

            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
            Vector3 candidate = player.position + offset;

            if (IsValidSpawnPosition(candidate))
            {
                return candidate;
            }
        }

        // Last resort: directly behind player at min distance
        Vector3 fallback = player.position - player.forward * minDist;
        if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return player.position;
    }

    private Vector3 GetBehindPlayerPosition(Transform player, float minDist, float maxDist, float angleRange)
    {
        float distance = Random.Range(minDist, maxDist);

        // Angle offset from directly behind (0 = directly behind)
        float angleOffset = Random.Range(-angleRange / 2f, angleRange / 2f);

        // Direction behind player, rotated by offset
        Vector3 behindDir = Quaternion.Euler(0, angleOffset, 0) * -player.forward;

        return player.position + behindDir * distance;
    }

    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if on NavMesh
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        // Check line of sight (optional - don't spawn through walls)
        if (controller.TargetPlayerTransform != null)
        {
            Vector3 toPlayer = controller.TargetPlayerTransform.position - hit.position;

            if (Physics.Raycast(hit.position + Vector3.up, toPlayer.normalized, toPlayer.magnitude - 1f))
            {
                // Something blocking - might still be okay, but prefer open paths
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    [Button("Call Companion"), BoxGroup("Debug")]
    private void DebugCallCompanion()
    {
        if (Application.isPlaying)
        {
            CallCompanion();
        }
    }

    [Button("Show Spawn Candidates"), BoxGroup("Debug")]
    private void DebugShowSpawnCandidates()
    {
        if (Application.isPlaying && controller?.TargetPlayerTransform != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 pos = CalculateSpawnPosition();
                Debug.DrawLine(controller.TargetPlayerTransform.position, pos, Color.yellow, 3f);
                Debug.DrawLine(pos, pos + Vector3.up * 2f, IsValidSpawnPosition(pos) ? Color.green : Color.red, 3f);
            }
        }
    }
#endif
}