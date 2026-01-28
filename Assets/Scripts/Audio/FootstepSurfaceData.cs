using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Maps surface types to footstep audio clips.
/// Surfaces are identified by PhysicsMaterial name for simplicity.
/// Create one asset: Assets/Data/Audio/FootstepSurfaceData.
/// </summary>
[CreateAssetMenu(fileName = "FootstepSurfaceData", menuName = "StillOrbit/Audio/Footstep Surface Data")]
public class FootstepSurfaceData : ScriptableObject
{
    [Serializable]
    public class SurfaceEntry
    {
        [Tooltip("Name of the PhysicsMaterial on the ground collider (case-insensitive match).")]
        public string materialName;

        [Tooltip("Footstep clips for this surface.")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 0.6f;

        [Range(0f, 0.2f)]
        [Tooltip("Random pitch variation per step.")]
        public float pitchVariation = 0.1f;

        /// <summary>
        /// Returns a random clip from the array, or null if empty.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }
    }

    [Header("Default")]
    [Tooltip("Fallback footstep clips used when no surface match is found.")]
    public SurfaceEntry defaultSurface = new() { materialName = "Default", volume = 0.6f };

    [Header("Surfaces")]
    [Tooltip("One entry per distinct surface type.")]
    [ListDrawerSettings(ShowIndexLabels = false)]
    public List<SurfaceEntry> surfaces = new();

    // Runtime lookup cache
    private Dictionary<string, SurfaceEntry> _lookup;

    /// <summary>
    /// Returns the surface entry matching the given PhysicsMaterial name.
    /// Falls back to defaultSurface if no match.
    /// </summary>
    public SurfaceEntry GetSurface(string physicsMaterialName)
    {
        BuildLookupIfNeeded();

        if (!string.IsNullOrEmpty(physicsMaterialName) &&
            _lookup.TryGetValue(physicsMaterialName.ToLowerInvariant(), out var entry))
        {
            return entry;
        }

        return defaultSurface;
    }

    /// <summary>
    /// Returns the surface entry for a given PhysicsMaterial.
    /// Falls back to defaultSurface if the material is null or unrecognized.
    /// </summary>
    public SurfaceEntry GetSurface(PhysicsMaterial material)
    {
        if (material == null) return defaultSurface;
        return GetSurface(material.name);
    }

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<string, SurfaceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var surface in surfaces)
        {
            if (string.IsNullOrEmpty(surface.materialName)) continue;

            string key = surface.materialName.ToLowerInvariant();
            if (!_lookup.ContainsKey(key))
            {
                _lookup[key] = surface;
            }
        }
    }

    private void OnValidate()
    {
        _lookup = null;
    }
}
