using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Audio configuration for enemy sound effects.
/// Assigned per EnemyArchetype to define all SFX an enemy type can produce.
/// Supports random clip selection, volume control, and cooldown configuration.
/// </summary>
[CreateAssetMenu(fileName = "New Enemy SFX", menuName = "StillOrbit/Audio/Enemy SFX")]
public class EnemySFXData : ScriptableObject
{
    [Serializable]
    public class SFXEntry
    {
        [Tooltip("Array of clips to randomly choose from.")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        [Tooltip("Playback volume.")]
        public float volume = 1f;

        [Range(0f, 0.3f)]
        [Tooltip("Random pitch variation applied to each play.")]
        public float pitchVariation = 0.05f;

        [Min(0f)]
        [Tooltip("Minimum seconds between plays of this sound category.")]
        public float cooldown;

        /// <summary>
        /// Returns true if at least one clip is assigned.
        /// </summary>
        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>
        /// Returns a random clip from the array, or null if empty.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (!HasClips) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }
    }

    [BoxGroup("Death")]
    [Tooltip("Played once when the enemy dies.")]
    public SFXEntry deathSounds = new() { volume = 1f, cooldown = 0f };

    [BoxGroup("Combat")]
    [Tooltip("Played when the enemy attacks.")]
    public SFXEntry attackSounds = new() { volume = 0.9f, cooldown = 0.5f };

    [BoxGroup("Combat")]
    [Tooltip("Played when the enemy first detects / aggros the player.")]
    public SFXEntry aggroSounds = new() { volume = 0.8f, cooldown = 3f };

    [BoxGroup("Combat")]
    [Tooltip("Played when the enemy takes damage (stagger).")]
    public SFXEntry hurtSounds = new() { volume = 0.9f, cooldown = 0.3f };

    [BoxGroup("Ambient")]
    [Tooltip("Idle vocalizations / ambient sounds while alive.")]
    public SFXEntry idleSounds = new() { volume = 0.5f, cooldown = 5f };
}
