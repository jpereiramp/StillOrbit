using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles enemy perception (sight and hearing).
/// States should use this component rather than performing their own raycasts.
/// </summary>
public class EnemyPerception : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("Configuration")]
    [Tooltip("Transform to raycast from (usually head/eyes)")]
    [SerializeField] private Transform eyePoint;

    [BoxGroup("Configuration")]
    [SerializeField] private LayerMask sightBlockingLayers;

    [BoxGroup("Configuration")]
    [SerializeField] private LayerMask targetLayers;

    [BoxGroup("Performance")]
    [Tooltip("Perception update rate (times per second)")]
    [SerializeField] private float updateRate = 10f;

    [BoxGroup("Performance")]
    [Tooltip("Max targets to track simultaneously")]
    [SerializeField] private int maxTrackedTargets = 5;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private PerceptionTarget primaryTarget;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private readonly List<PerceptionTarget> trackedTargets = new();

    private float _lastUpdateTime;
    private float _updateInterval;
    private bool _archetypeValuesCached;

    // Cached archetype values
    private float _sightRange;
    private float _sightAngle;
    private float _hearingRange;
    private float _memoryDuration;

    // Public accessors
    public PerceptionTarget PrimaryTarget => primaryTarget;
    public IReadOnlyList<PerceptionTarget> TrackedTargets => trackedTargets;
    public bool HasTarget => primaryTarget != null && primaryTarget.IsInMemory(_memoryDuration);
    public Transform TargetTransform => primaryTarget?.Transform;
    public Vector3 LastKnownTargetPosition => primaryTarget?.LastKnownPosition ?? transform.position;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        if (eyePoint == null)
            eyePoint = transform;
    }

    private void Start()
    {
        _updateInterval = 1f / updateRate;

        if (controller.Archetype != null)
        {
            CacheArchetypeValues();
        }
    }

    private void Update()
    {
        if (!controller.IsInitialized)
            return;

        // Cache archetype values on first update after initialization
        // This handles both inspector-assigned and dynamically spawned enemies
        if (!_archetypeValuesCached && controller.Archetype != null)
        {
            CacheArchetypeValues();
        }

        if (!_archetypeValuesCached)
            return;

        // Throttled perception updates
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = Time.time;
            UpdatePerception();
        }
    }

    private void CacheArchetypeValues()
    {
        var archetype = controller.Archetype;
        _sightRange = archetype.SightRange;
        _sightAngle = archetype.SightAngle;
        _hearingRange = archetype.HearingRange;
        _memoryDuration = archetype.MemoryDuration;
        _archetypeValuesCached = true;
    }

    private void UpdatePerception()
    {
        // Find potential targets
        var colliders = Physics.OverlapSphere(
            transform.position,
            Mathf.Max(_sightRange, _hearingRange),
            targetLayers
        );

        // Update tracked targets
        foreach (var col in colliders)
        {
            var perceivable = col.GetComponentInParent<IPerceivable>();
            if (perceivable == null || !perceivable.IsPerceivable)
                continue;

            var target = GetOrCreateTarget(col.transform);
            UpdateTargetPerception(target, perceivable);
        }

        // Clean up stale targets
        CleanupStaleTargets();

        // Select primary target
        SelectPrimaryTarget();

        // Update controller context
        UpdateControllerContext();
    }

    private PerceptionTarget GetOrCreateTarget(Transform targetTransform)
    {
        var existing = trackedTargets.Find(t => t.Transform == targetTransform);
        if (existing != null)
            return existing;

        if (trackedTargets.Count >= maxTrackedTargets)
        {
            // Remove oldest
            float oldestTime = float.MaxValue;
            PerceptionTarget oldest = null;
            foreach (var t in trackedTargets)
            {
                float perceiveTime = Mathf.Max(t.LastSeenTime, t.LastHeardTime);
                if (perceiveTime < oldestTime)
                {
                    oldestTime = perceiveTime;
                    oldest = t;
                }
            }
            if (oldest != null)
                trackedTargets.Remove(oldest);
        }

        var newTarget = new PerceptionTarget { Transform = targetTransform };
        trackedTargets.Add(newTarget);
        return newTarget;
    }

    private void UpdateTargetPerception(PerceptionTarget target, IPerceivable perceivable)
    {
        Vector3 targetPos = perceivable.PerceptionPosition;
        float distance = Vector3.Distance(transform.position, targetPos);
        target.Distance = distance;
        target.Priority = perceivable.TargetPriority;

        // Sight check
        target.IsCurrentlyVisible = CheckSight(targetPos, distance);
        if (target.IsCurrentlyVisible)
        {
            target.LastSeenTime = Time.time;
            target.LastKnownPosition = targetPos;
        }

        // Hearing check
        target.IsCurrentlyAudible = CheckHearing(distance, perceivable.NoiseLevel);
        if (target.IsCurrentlyAudible)
        {
            target.LastHeardTime = Time.time;
            target.LastKnownPosition = targetPos;
        }
    }

    private bool CheckSight(Vector3 targetPos, float distance)
    {
        // Range check
        if (distance > _sightRange)
            return false;

        // Angle check
        Vector3 directionToTarget = (targetPos - eyePoint.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToTarget);
        if (angle > _sightAngle / 2f)
            return false;

        // Line of sight check
        Vector3 rayOrigin = eyePoint.position;
        Vector3 rayDirection = targetPos - rayOrigin;

        if (Physics.Raycast(rayOrigin, rayDirection.normalized, out RaycastHit hit, distance, sightBlockingLayers))
        {
            // Check if we hit something before the target
            if (hit.distance < distance - 0.5f)
                return false;
        }

        return true;
    }

    private bool CheckHearing(float distance, float noiseLevel)
    {
        if (distance > _hearingRange)
            return false;

        // Noise level affects effective hearing range
        float effectiveHearingRange = _hearingRange * noiseLevel;
        return distance <= effectiveHearingRange;
    }

    private void CleanupStaleTargets()
    {
        trackedTargets.RemoveAll(t =>
            t.Transform == null ||
            !t.IsInMemory(_memoryDuration)
        );
    }

    private void SelectPrimaryTarget()
    {
        PerceptionTarget best = null;
        float bestScore = float.MinValue;

        foreach (var target in trackedTargets)
        {
            if (!target.IsInMemory(_memoryDuration))
                continue;

            // Score: priority, visibility, distance
            float score = target.Priority * 100f;
            if (target.IsCurrentlyVisible)
                score += 50f;
            if (target.IsCurrentlyAudible)
                score += 25f;
            score -= target.Distance;

            if (score > bestScore)
            {
                bestScore = score;
                best = target;
            }
        }

        primaryTarget = best;
    }

    private void UpdateControllerContext()
    {
        if (controller.Context == null)
            return;

        controller.Context.CurrentTarget = primaryTarget?.Transform;
        controller.Context.LastKnownTargetPosition = primaryTarget?.LastKnownPosition ?? Vector3.zero;
        controller.Context.TimeSinceTargetSeen = primaryTarget?.TimeSincePerceived ?? float.MaxValue;
    }

    /// <summary>
    /// Manually alert this enemy to a position (e.g., from gunshot).
    /// </summary>
    public void AlertToPosition(Vector3 position, float priority = 1f)
    {
        // Create temporary "phantom" target
        if (primaryTarget == null)
        {
            primaryTarget = new PerceptionTarget
            {
                LastKnownPosition = position,
                LastHeardTime = Time.time,
                Priority = (int)priority
            };
        }
        else
        {
            primaryTarget.LastKnownPosition = position;
            primaryTarget.LastHeardTime = Time.time;
        }
    }

    /// <summary>
    /// Clear all perception (e.g., after respawn).
    /// </summary>
    public void ClearPerception()
    {
        trackedTargets.Clear();
        primaryTarget = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && controller?.Archetype == null)
            return;

        var archetype = controller?.Archetype;
        float sightRange = archetype?.SightRange ?? 20f;
        float sightAngle = archetype?.SightAngle ?? 120f;
        float hearingRange = archetype?.HearingRange ?? 15f;

        Vector3 eyePos = eyePoint != null ? eyePoint.position : transform.position;

        // Sight cone
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        DrawViewCone(eyePos, transform.forward, sightAngle, sightRange);

        // Hearing range
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, hearingRange);

        // Primary target
        if (primaryTarget != null)
        {
            Gizmos.color = primaryTarget.IsCurrentlyVisible ? Color.green : Color.yellow;
            Gizmos.DrawLine(eyePos, primaryTarget.LastKnownPosition);
            Gizmos.DrawWireSphere(primaryTarget.LastKnownPosition, 0.5f);
        }
    }

    private void DrawViewCone(Vector3 origin, Vector3 forward, float angle, float range)
    {
        int segments = 20;
        float halfAngle = angle / 2f;

        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

        Gizmos.DrawLine(origin, origin + leftDir * range);
        Gizmos.DrawLine(origin, origin + rightDir * range);

        for (int i = 0; i < segments; i++)
        {
            float t1 = (float)i / segments;
            float t2 = (float)(i + 1) / segments;
            float a1 = Mathf.Lerp(-halfAngle, halfAngle, t1);
            float a2 = Mathf.Lerp(-halfAngle, halfAngle, t2);

            Vector3 p1 = origin + Quaternion.Euler(0, a1, 0) * forward * range;
            Vector3 p2 = origin + Quaternion.Euler(0, a2, 0) * forward * range;

            Gizmos.DrawLine(p1, p2);
        }
    }
#endif
}