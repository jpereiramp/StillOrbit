using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles all SFX playback for a single enemy instance.
/// Reads configuration from EnemySFXData (assigned via EnemyArchetype).
/// Attach to enemy prefabs alongside EnemyController.
///
/// Listens to EnemyController state changes and HealthComponent events
/// to trigger sounds at the right moments. Enforces per-category cooldowns
/// to prevent sound spam.
/// </summary>
public class EnemyAudioHandler : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [Tooltip("Overrides the archetype's SFX data. Leave null to use archetype default.")]
    [SerializeField] private EnemySFXData sfxDataOverride;

    [BoxGroup("Configuration")]
    [Tooltip("Optional AudioSource for positional audio. Auto-created if not assigned.")]
    [SerializeField] private AudioSource audioSource;

    private EnemyController _controller;
    private HealthComponent _health;
    private EnemySFXData _sfxData;

    // Cooldown tracking: maps SFXEntry reference to last play time
    private readonly Dictionary<EnemySFXData.SFXEntry, float> _cooldowns = new();

    private bool _isDead;

    private void Awake()
    {
        _controller = GetComponent<EnemyController>();
        _health = GetComponent<HealthComponent>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Configure the AudioSource for 3D positional SFX
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 30f;
    }

    private void Start()
    {
        ResolveSFXData();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Resolves which EnemySFXData to use: explicit override or archetype default.
    /// </summary>
    private void ResolveSFXData()
    {
        if (sfxDataOverride != null)
        {
            _sfxData = sfxDataOverride;
            return;
        }

        if (_controller != null && _controller.Archetype != null)
        {
            _sfxData = _controller.Archetype.SFXData;
        }

        if (_sfxData == null)
        {
            Debug.LogWarning($"[EnemyAudio] {name}: No EnemySFXData assigned. SFX will be silent.");
        }
    }

    private void SubscribeToEvents()
    {
        if (_controller != null)
        {
            _controller.OnStateChanged += HandleStateChanged;
            _controller.OnDeath += HandleDeath;
        }

        if (_health != null)
        {
            _health.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_controller != null)
        {
            _controller.OnStateChanged -= HandleStateChanged;
            _controller.OnDeath -= HandleDeath;
        }

        if (_health != null)
        {
            _health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    /// <summary>
    /// Responds to enemy state transitions to trigger context-appropriate SFX.
    /// </summary>
    private void HandleStateChanged(EnemyState from, EnemyState to)
    {
        if (_sfxData == null || _isDead) return;

        switch (to)
        {
            case EnemyState.Chase:
                // Play aggro sound when first entering chase (not re-entering from attack)
                if (from == EnemyState.Idle || from == EnemyState.Patrol || from == EnemyState.Investigate)
                {
                    TryPlaySFX(_sfxData.aggroSounds);
                }
                break;

            case EnemyState.Attack:
                TryPlaySFX(_sfxData.attackSounds);
                break;

            case EnemyState.Hurt:
                TryPlaySFX(_sfxData.hurtSounds);
                break;

            case EnemyState.Idle:
                // Idle vocalizations are handled in Update with cooldown
                break;
        }
    }

    /// <summary>
    /// Handles death: plays death SFX once, guaranteed.
    /// Uses PlayClipAtPoint to ensure the sound completes even if the GameObject is destroyed.
    /// </summary>
    private void HandleDeath(EnemyController controller)
    {
        if (_isDead) return;
        _isDead = true;

        if (_sfxData == null || !_sfxData.deathSounds.HasClips) return;

        AudioClip clip = _sfxData.deathSounds.GetRandomClip();
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, _sfxData.deathSounds.volume);
            Debug.Log($"[EnemyAudio] {name}: Death SFX played.");
        }
    }

    /// <summary>
    /// Reacts to health changes to play hurt sounds on actual damage.
    /// This is a backup path — primary hurt SFX is triggered by the Hurt state.
    /// Only fires if the enemy did NOT enter Hurt state (i.e., stagger was skipped).
    /// </summary>
    private void HandleHealthChanged(int current, int max)
    {
        // Only play if not already handled by state change to Hurt
        if (_sfxData == null || _isDead) return;
        if (_controller != null && _controller.CurrentState == EnemyState.Hurt) return;

        // Damage occurred but no stagger — still play a subtle hit reaction
        if (current < max && current > 0)
        {
            TryPlaySFX(_sfxData.hurtSounds);
        }
    }

    private void Update()
    {
        if (_sfxData == null || _isDead) return;

        // Idle vocalizations: only when in Idle or Patrol states
        if (_controller != null &&
            (_controller.CurrentState == EnemyState.Idle ||
             _controller.CurrentState == EnemyState.Patrol))
        {
            TryPlaySFX(_sfxData.idleSounds);
        }
    }

    /// <summary>
    /// Attempts to play a sound from the given SFX entry, respecting cooldowns.
    /// </summary>
    /// <returns>True if the sound was played.</returns>
    private bool TryPlaySFX(EnemySFXData.SFXEntry entry)
    {
        if (entry == null || !entry.HasClips) return false;

        // Check cooldown
        if (_cooldowns.TryGetValue(entry, out float lastTime))
        {
            if (Time.time - lastTime < entry.cooldown) return false;
        }

        AudioClip clip = entry.GetRandomClip();
        if (clip == null) return false;

        // Apply pitch variation
        audioSource.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
        audioSource.PlayOneShot(clip, entry.volume);

        // Record cooldown
        _cooldowns[entry] = Time.time;

        return true;
    }

#if UNITY_EDITOR
    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private string DebugSFXDataName => _sfxData != null ? _sfxData.name : "(none)";

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool DebugIsDead => _isDead;
#endif
}
