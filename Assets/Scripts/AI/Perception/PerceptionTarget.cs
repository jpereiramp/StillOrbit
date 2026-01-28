// Scripts/AI/Perception/PerceptionTarget.cs
using UnityEngine;

/// <summary>
/// Data about a perceived target.
/// </summary>
public class PerceptionTarget
{
    public Transform Transform { get; set; }
    public Vector3 LastKnownPosition { get; set; }
    public float LastSeenTime { get; set; }
    public float LastHeardTime { get; set; }
    public bool IsCurrentlyVisible { get; set; }
    public bool IsCurrentlyAudible { get; set; }
    public float Distance { get; set; }
    public int Priority { get; set; }

    /// <summary>
    /// Time since this target was last perceived (seen or heard).
    /// </summary>
    public float TimeSincePerceived => Time.time - Mathf.Max(LastSeenTime, LastHeardTime);

    /// <summary>
    /// Is this target still in memory (within memory duration)?
    /// </summary>
    public bool IsInMemory(float memoryDuration)
    {
        return TimeSincePerceived <= memoryDuration;
    }

    /// <summary>
    /// Is this target actively perceived right now?
    /// </summary>
    public bool IsActivelyPerceived => IsCurrentlyVisible || IsCurrentlyAudible;
}