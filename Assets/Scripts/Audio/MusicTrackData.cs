using System;
using UnityEngine;

/// <summary>
/// Configuration for a single music track.
/// Supports looping tracks with an optional non-looping intro segment.
/// </summary>
[Serializable]
public class MusicTrackData
{
    [Tooltip("The main music clip. Loops by default.")]
    public AudioClip clip;

    [Tooltip("Optional intro clip that plays once before the main loop begins.")]
    public AudioClip introClip;

    [Range(0f, 1f)]
    [Tooltip("Target volume for this track.")]
    public float volume = 0.7f;

    [Tooltip("If true, the main clip loops. Set to false for one-shot tracks (e.g., boss intro sting).")]
    public bool loop = true;

    [Min(0f)]
    [Tooltip("Override fade duration for this track. 0 = use global default.")]
    public float fadeOverride;

    /// <summary>
    /// Returns true if this track entry has a valid clip assigned.
    /// </summary>
    public bool IsValid => clip != null;

    /// <summary>
    /// Returns true if this track has an intro segment.
    /// </summary>
    public bool HasIntro => introClip != null;
}
