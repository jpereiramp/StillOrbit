using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Maps each MusicState to its track data and priority.
/// Create one asset: Assets/Data/Audio/MusicStateConfig.
/// </summary>
[CreateAssetMenu(fileName = "MusicStateConfig", menuName = "StillOrbit/Audio/Music State Config")]
public class MusicStateConfig : ScriptableObject
{
    [Serializable]
    public class MusicStateEntry
    {
        public MusicState state;

        [Tooltip("Higher priority states override lower ones. Boss > Combat > Exploration.")]
        public int priority;

        [BoxGroup("Track")]
        public MusicTrackData trackData;
    }

    [Header("Global Defaults")]
    [Tooltip("Default crossfade duration in seconds when no per-track override is set.")]
    [Range(0.5f, 5f)]
    public float defaultFadeDuration = 1.5f;

    [Header("State Mappings")]
    [Tooltip("One entry per MusicState. Missing entries produce silence for that state.")]
    [ListDrawerSettings(ShowIndexLabels = false)]
    public List<MusicStateEntry> entries = new();

    // Runtime lookup cache
    private Dictionary<MusicState, MusicStateEntry> _lookup;

    /// <summary>
    /// Retrieves the entry for a given music state.
    /// Returns null if no entry is configured (produces silence).
    /// </summary>
    public MusicStateEntry GetEntry(MusicState state)
    {
        BuildLookupIfNeeded();

        _lookup.TryGetValue(state, out var entry);
        return entry;
    }

    /// <summary>
    /// Returns the priority value for a given state.
    /// Returns -1 if the state has no entry.
    /// </summary>
    public int GetPriority(MusicState state)
    {
        var entry = GetEntry(state);
        return entry?.priority ?? -1;
    }

    /// <summary>
    /// Returns the effective fade duration for a state:
    /// the track's override if set, otherwise the global default.
    /// </summary>
    public float GetFadeDuration(MusicState state)
    {
        var entry = GetEntry(state);
        if (entry == null) return defaultFadeDuration;

        float trackOverride = entry.trackData.fadeOverride;
        return trackOverride > 0f ? trackOverride : defaultFadeDuration;
    }

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<MusicState, MusicStateEntry>();
        foreach (var entry in entries)
        {
            if (_lookup.ContainsKey(entry.state))
            {
                Debug.LogWarning($"[Audio] Duplicate MusicState entry for {entry.state} — using first.");
                continue;
            }
            _lookup[entry.state] = entry;
        }
    }

    private void OnValidate()
    {
        // Invalidate cache when Inspector values change
        _lookup = null;
    }
}
