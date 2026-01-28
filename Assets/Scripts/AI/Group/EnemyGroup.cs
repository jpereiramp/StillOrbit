using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Lightweight coordination for a group of enemies.
/// Handles attacker limiting, target sharing, and basic tactics.
/// Does NOT override individual AI - just provides coordination hints.
/// </summary>
public class EnemyGroup : MonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("Maximum simultaneous attackers")]
    [SerializeField] private int maxAttackers = 3;

    [BoxGroup("Settings")]
    [Tooltip("Minimum space between attackers")]
    [SerializeField] private float attackerSpacing = 2f;

    [BoxGroup("Settings")]
    [Tooltip("How often to refresh attacker slots (seconds)")]
    [SerializeField] private float slotRefreshInterval = 1f;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<EnemyController> members = new();

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<EnemyController> activeAttackers = new();

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private Transform sharedTarget;

    private float _lastSlotRefresh;

    // Events
    public event Action<EnemyController> OnMemberAdded;
    public event Action<EnemyController> OnMemberRemoved;
    public event Action<Transform> OnTargetChanged;

    // Public accessors
    public IReadOnlyList<EnemyController> Members => members;
    public IReadOnlyList<EnemyController> ActiveAttackers => activeAttackers;
    public Transform SharedTarget => sharedTarget;
    public int MaxAttackers => maxAttackers;
    public int AvailableAttackSlots => Mathf.Max(0, maxAttackers - activeAttackers.Count);

    private void Update()
    {
        // Periodic cleanup of dead/invalid attackers
        if (Time.time - _lastSlotRefresh >= slotRefreshInterval)
        {
            _lastSlotRefresh = Time.time;
            RefreshAttackerSlots();
        }
    }

    /// <summary>
    /// Add an enemy to this group.
    /// </summary>
    public void AddMember(EnemyController enemy)
    {
        if (enemy == null || members.Contains(enemy))
            return;

        members.Add(enemy);
        enemy.OnDeath += HandleMemberDeath;
        enemy.OnStateChanged += HandleMemberStateChanged;

        OnMemberAdded?.Invoke(enemy);
    }

    /// <summary>
    /// Remove an enemy from this group.
    /// </summary>
    public void RemoveMember(EnemyController enemy)
    {
        if (enemy == null)
            return;

        members.Remove(enemy);
        activeAttackers.Remove(enemy);
        enemy.OnDeath -= HandleMemberDeath;
        enemy.OnStateChanged -= HandleMemberStateChanged;

        OnMemberRemoved?.Invoke(enemy);
    }

    /// <summary>
    /// Request permission to attack. Returns true if allowed.
    /// </summary>
    public bool RequestAttackSlot(EnemyController enemy)
    {
        if (!members.Contains(enemy))
            return true; // Not in group, allow

        // Already has a slot
        if (activeAttackers.Contains(enemy))
            return true;

        // Clean up invalid attackers first
        RefreshAttackerSlots();

        // Check slot availability
        if (activeAttackers.Count >= maxAttackers)
        {
            return false;
        }

        // Check spacing with other attackers
        if (!HasSufficientSpacing(enemy))
        {
            return false;
        }

        // Grant slot
        activeAttackers.Add(enemy);
        return true;
    }

    /// <summary>
    /// Release an attack slot (call when attack ends or interrupted).
    /// </summary>
    public void ReleaseAttackSlot(EnemyController enemy)
    {
        activeAttackers.Remove(enemy);
    }

    /// <summary>
    /// Set a shared target for the entire group.
    /// </summary>
    public void SetSharedTarget(Transform target)
    {
        if (sharedTarget == target)
            return;

        sharedTarget = target;
        OnTargetChanged?.Invoke(target);

        // Optionally alert all members
        foreach (var member in members)
        {
            if (member != null && member.Context != null && member.Context.CurrentTarget == null)
            {
                member.Context.CurrentTarget = target;
                if (target != null)
                {
                    member.Context.LastKnownTargetPosition = target.position;
                }
            }
        }
    }

    /// <summary>
    /// Alert all group members to a position (e.g., gunshot heard).
    /// </summary>
    public void AlertGroupToPosition(Vector3 position)
    {
        foreach (var member in members)
        {
            if (member == null)
                continue;

            var perception = member.GetComponent<EnemyPerception>();
            if (perception != null)
            {
                perception.AlertToPosition(position);
            }
        }
    }

    /// <summary>
    /// Get the nearest member to a position.
    /// </summary>
    public EnemyController GetNearestMember(Vector3 position)
    {
        EnemyController nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var member in members)
        {
            if (member == null || !member.IsAlive)
                continue;

            float dist = Vector3.Distance(member.transform.position, position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = member;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all members within a radius.
    /// </summary>
    public List<EnemyController> GetMembersInRadius(Vector3 center, float radius)
    {
        var result = new List<EnemyController>();
        float radiusSqr = radius * radius;

        foreach (var member in members)
        {
            if (member == null || !member.IsAlive)
                continue;

            if ((member.transform.position - center).sqrMagnitude <= radiusSqr)
            {
                result.Add(member);
            }
        }

        return result;
    }

    private void HandleMemberDeath(EnemyController enemy)
    {
        RemoveMember(enemy);
    }

    private void HandleMemberStateChanged(EnemyState from, EnemyState to)
    {
        // If an enemy exits attack state, release their slot
        if (from == EnemyState.Attack && to != EnemyState.Attack)
        {
            // Find which member changed state
            foreach (var attacker in activeAttackers.ToArray())
            {
                if (attacker != null && attacker.CurrentState != EnemyState.Attack)
                {
                    activeAttackers.Remove(attacker);
                }
            }
        }
    }

    private void RefreshAttackerSlots()
    {
        // Remove dead, null, or non-attacking enemies from active attackers
        activeAttackers.RemoveAll(e =>
            e == null ||
            !e.IsAlive ||
            (e.CurrentState != EnemyState.Attack && e.CurrentState != EnemyState.Chase)
        );

        // Also clean up dead members
        members.RemoveAll(e => e == null || !e.IsAlive);
    }

    private bool HasSufficientSpacing(EnemyController candidate)
    {
        if (attackerSpacing <= 0)
            return true;

        foreach (var attacker in activeAttackers)
        {
            if (attacker == null)
                continue;

            float dist = Vector3.Distance(candidate.transform.position, attacker.transform.position);
            if (dist < attackerSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private void OnDestroy()
    {
        // Cleanup subscriptions
        foreach (var member in members.ToArray())
        {
            if (member != null)
            {
                member.OnDeath -= HandleMemberDeath;
                member.OnStateChanged -= HandleMemberStateChanged;
            }
        }
    }

#if UNITY_EDITOR
    [Button("Log Group Status"), BoxGroup("Debug")]
    private void DebugLogStatus()
    {
        Debug.Log($"[EnemyGroup] Members: {members.Count}, Active Attackers: {activeAttackers.Count}/{maxAttackers}");
        Debug.Log($"[EnemyGroup] Shared Target: {sharedTarget?.name ?? "none"}");
    }
#endif
}
