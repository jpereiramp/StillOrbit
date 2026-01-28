using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility methods for group tactical calculations.
/// Used by EnemyGroup and individual enemies for coordination.
/// </summary>
public static class GroupTactics
{
    /// <summary>
    /// Calculate surround positions around a target.
    /// Returns positions evenly distributed in a circle.
    /// </summary>
    public static Vector3[] CalculateSurroundPositions(Vector3 targetPosition, int count, float radius)
    {
        var positions = new Vector3[count];
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            positions[i] = targetPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
        }

        return positions;
    }

    /// <summary>
    /// Find the best surround position for an enemy based on current positions.
    /// Picks the position farthest from other enemies.
    /// </summary>
    public static Vector3 FindBestSurroundPosition(
        Vector3 targetPosition,
        Vector3 candidatePosition,
        IReadOnlyList<EnemyController> otherEnemies,
        float idealRadius)
    {
        // Generate candidate positions
        var positions = CalculateSurroundPositions(targetPosition, 8, idealRadius);

        Vector3 bestPosition = candidatePosition;
        float bestScore = float.MinValue;

        foreach (var pos in positions)
        {
            float score = ScorePosition(pos, candidatePosition, otherEnemies);
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = pos;
            }
        }

        return bestPosition;
    }

    /// <summary>
    /// Score a position based on distance to candidate and distance from other enemies.
    /// Higher score = better position.
    /// </summary>
    private static float ScorePosition(Vector3 position, Vector3 candidatePosition, IReadOnlyList<EnemyController> otherEnemies)
    {
        // Prefer positions closer to candidate's current position (less travel)
        float distanceToCandidate = Vector3.Distance(position, candidatePosition);
        float travelScore = -distanceToCandidate * 0.5f;

        // Prefer positions farther from other enemies (spacing)
        float spacingScore = 0f;
        foreach (var enemy in otherEnemies)
        {
            if (enemy == null)
                continue;

            float distToEnemy = Vector3.Distance(position, enemy.transform.position);
            spacingScore += Mathf.Min(distToEnemy, 5f); // Cap contribution per enemy
        }

        return travelScore + spacingScore;
    }

    /// <summary>
    /// Calculate a flanking position relative to a target and threat direction.
    /// </summary>
    public static Vector3 CalculateFlankPosition(
        Vector3 targetPosition,
        Vector3 threatDirection,
        float flankDistance,
        bool preferLeft)
    {
        // Perpendicular to threat direction
        Vector3 flankDir = preferLeft
            ? Vector3.Cross(Vector3.up, threatDirection).normalized
            : Vector3.Cross(threatDirection, Vector3.up).normalized;

        return targetPosition + flankDir * flankDistance;
    }

    /// <summary>
    /// Determine if an enemy should retreat based on group status.
    /// </summary>
    public static bool ShouldRetreat(EnemyController enemy, EnemyGroup group, float retreatHealthThreshold = 0.3f)
    {
        if (enemy == null || group == null)
            return false;

        // Low health
        float healthPercent = enemy.Health.GetHealthPercentage() / 100f;
        if (healthPercent <= retreatHealthThreshold)
            return true;

        // Last survivor in group
        int aliveCount = 0;
        foreach (var member in group.Members)
        {
            if (member != null && member.IsAlive)
                aliveCount++;
        }

        if (aliveCount <= 1 && healthPercent < 0.5f)
            return true;

        return false;
    }

    /// <summary>
    /// Find a retreat position away from a threat.
    /// </summary>
    public static Vector3 FindRetreatPosition(Vector3 currentPosition, Vector3 threatPosition, float retreatDistance)
    {
        Vector3 awayFromThreat = (currentPosition - threatPosition).normalized;
        return currentPosition + awayFromThreat * retreatDistance;
    }

    /// <summary>
    /// Calculate the centroid of a group of enemies.
    /// </summary>
    public static Vector3 CalculateGroupCentroid(IReadOnlyList<EnemyController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                sum += enemy.transform.position;
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    /// <summary>
    /// Check if enemies are spread too far apart.
    /// </summary>
    public static bool IsGroupScattered(IReadOnlyList<EnemyController> enemies, float maxSpread)
    {
        if (enemies == null || enemies.Count < 2)
            return false;

        Vector3 centroid = CalculateGroupCentroid(enemies);
        float maxSpreadSqr = maxSpread * maxSpread;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            if ((enemy.transform.position - centroid).sqrMagnitude > maxSpreadSqr)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Find the most isolated enemy in a group (farthest from centroid).
    /// </summary>
    public static EnemyController FindMostIsolatedMember(IReadOnlyList<EnemyController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            return null;

        Vector3 centroid = CalculateGroupCentroid(enemies);
        EnemyController mostIsolated = null;
        float maxDist = 0f;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            float dist = Vector3.Distance(enemy.transform.position, centroid);
            if (dist > maxDist)
            {
                maxDist = dist;
                mostIsolated = enemy;
            }
        }

        return mostIsolated;
    }
}
