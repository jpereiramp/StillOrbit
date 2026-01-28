using UnityEngine;

/// <summary>
/// Interface for objects that can be perceived by enemies.
/// Implement on player, companions, or any detectable entity.
/// </summary>
public interface IPerceivable
{
    /// <summary>World position of this perceivable target.</summary>
    Vector3 PerceptionPosition { get; }

    /// <summary>Is this target currently perceivable (not hidden, etc.).</summary>
    bool IsPerceivable { get; }

    /// <summary>How "loud" this target is (affects hearing detection).</summary>
    float NoiseLevel { get; }

    /// <summary>Priority when multiple targets exist (higher = preferred).</summary>
    int TargetPriority { get; }
}