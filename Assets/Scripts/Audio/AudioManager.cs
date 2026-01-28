using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Central authority for all music playback and transitions.
/// Singleton — access via AudioManager.Instance.
/// Persists across scene loads.
///
/// Owns two AudioSources (A/B) for crossfading.
/// All music configuration lives in MusicStateConfig (ScriptableObject).
/// Gameplay code requests states via SetMusicState / ForceSetMusicState.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private MusicStateConfig musicConfig;

    [Tooltip("Music state to play when the game starts. Set to None to start silent.")]
    [SerializeField] private MusicState initialState = MusicState.Exploration;

    [Header("Music Sources (auto-created if not assigned)")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;

    /// <summary>
    /// The AudioSource currently playing (or fading in).
    /// </summary>
    private AudioSource _activeSource;

    /// <summary>
    /// The AudioSource currently fading out (or idle).
    /// </summary>
    private AudioSource _inactiveSource;

    /// <summary>
    /// The currently active music state.
    /// </summary>
    private MusicState _currentState = MusicState.None;

    /// <summary>
    /// The state that was active before the current one.
    /// </summary>
    private MusicState _previousState = MusicState.None;

    /// <summary>
    /// Reference to the currently running crossfade coroutine.
    /// Stored so it can be stopped if a new transition begins mid-fade.
    /// </summary>
    private Coroutine _crossfadeCoroutine;

    /// <summary>
    /// Fired when the music state changes.
    /// Parameters: (previousState, newState)
    /// </summary>
    public event Action<MusicState, MusicState> OnMusicStateChanged;

    /// <summary>
    /// The current active music state (read-only).
    /// </summary>
    public MusicState CurrentState => _currentState;

    /// <summary>
    /// The previous music state (read-only).
    /// </summary>
    public MusicState PreviousState => _previousState;

    /// <summary>
    /// The active music state configuration asset.
    /// </summary>
    public MusicStateConfig MusicConfig => musicConfig;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();

        _activeSource = musicSourceA;
        _inactiveSource = musicSourceB;
    }

    private void Start()
    {
        if (initialState != MusicState.None)
        {
            ForceSetMusicState(initialState);
        }
    }

    /// <summary>
    /// Creates AudioSource components if they were not assigned in the Inspector.
    /// Both sources are configured for 2D music playback.
    /// </summary>
    private void EnsureAudioSources()
    {
        if (musicSourceA == null)
        {
            musicSourceA = gameObject.AddComponent<AudioSource>();
            musicSourceA.playOnAwake = false;
            musicSourceA.spatialBlend = 0f;
            musicSourceA.loop = true;
        }

        if (musicSourceB == null)
        {
            musicSourceB = gameObject.AddComponent<AudioSource>();
            musicSourceB.playOnAwake = false;
            musicSourceB.spatialBlend = 0f;
            musicSourceB.loop = true;
        }
    }

    /// <summary>
    /// Request a music state change.
    /// Respects priority: a lower-priority state cannot override a higher-priority one.
    /// If the requested state matches the current state, this is a no-op.
    /// </summary>
    /// <param name="newState">The desired music state.</param>
    /// <returns>True if the state change was accepted.</returns>
    public bool SetMusicState(MusicState newState)
    {
        if (newState == _currentState) return true;

        if (musicConfig == null)
        {
            Debug.LogWarning("[Audio] No MusicStateConfig assigned to AudioManager.");
            return false;
        }

        // Priority check: don't override a higher-priority state
        int currentPriority = musicConfig.GetPriority(_currentState);
        int newPriority = musicConfig.GetPriority(newState);

        if (newPriority < currentPriority)
        {
            Debug.Log($"[Audio] Ignored {newState} (priority {newPriority}) — " +
                      $"current {_currentState} has higher priority ({currentPriority}).");
            return false;
        }

        var entry = musicConfig.GetEntry(newState);

        _previousState = _currentState;
        _currentState = newState;

        PlayTrack(entry);

        Debug.Log($"[Audio] Music state: {_previousState} → {_currentState}");
        OnMusicStateChanged?.Invoke(_previousState, _currentState);
        return true;
    }

    /// <summary>
    /// Forces a music state change, bypassing priority checks.
    /// Use for returning to lower-priority states after a high-priority context ends
    /// (e.g., boss dies → back to exploration).
    /// </summary>
    /// <param name="newState">The desired music state.</param>
    public void ForceSetMusicState(MusicState newState)
    {
        if (newState == _currentState) return;

        if (musicConfig == null)
        {
            Debug.LogWarning("[Audio] No MusicStateConfig assigned to AudioManager.");
            return;
        }

        var entry = musicConfig.GetEntry(newState);

        _previousState = _currentState;
        _currentState = newState;

        PlayTrack(entry);

        Debug.Log($"[Audio] Music state (forced): {_previousState} → {_currentState}");
        OnMusicStateChanged?.Invoke(_previousState, _currentState);
    }

    /// <summary>
    /// Returns to the previous music state, bypassing priority.
    /// Use when the current high-priority context has ended.
    /// </summary>
    public void ReturnToPreviousState()
    {
        ForceSetMusicState(_previousState);
    }

    /// <summary>
    /// Fades out all music and sets the state to None.
    /// </summary>
    public void StopMusic()
    {
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
        }

        _crossfadeCoroutine = StartCoroutine(FadeToSilence(
            musicConfig != null ? musicConfig.defaultFadeDuration : 1f));

        _previousState = _currentState;
        _currentState = MusicState.None;

        Debug.Log("[Audio] Music fading to silence.");
        OnMusicStateChanged?.Invoke(_previousState, _currentState);
    }

    /// <summary>
    /// Plays the track associated with a state entry, crossfading from
    /// whatever is currently playing.
    /// </summary>
    private void PlayTrack(MusicStateConfig.MusicStateEntry entry)
    {
        // Cancel any in-progress crossfade
        if (_crossfadeCoroutine != null)
        {
            StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = null;
        }

        // Null entry or no clip = fade to silence
        if (entry == null || !entry.trackData.IsValid)
        {
            _crossfadeCoroutine = StartCoroutine(FadeToSilence(
                musicConfig.defaultFadeDuration));
            return;
        }

        float fadeDuration = musicConfig.GetFadeDuration(_currentState);

        _crossfadeCoroutine = StartCoroutine(CrossfadeToTrack(
            entry.trackData, fadeDuration));
    }

    /// <summary>
    /// Crossfades from the current active source to a new track on the inactive source.
    /// After the fade completes, the sources are swapped.
    /// </summary>
    private IEnumerator CrossfadeToTrack(MusicTrackData track, float duration)
    {
        // Swap: the old active becomes the fade-out target,
        // the old inactive becomes the fade-in target.
        SwapSources();

        AudioSource fadingOut = _inactiveSource;
        AudioSource fadingIn = _activeSource;

        // Prepare the new track on the fading-in source
        if (track.HasIntro)
        {
            fadingIn.clip = track.introClip;
            fadingIn.loop = false;
        }
        else
        {
            fadingIn.clip = track.clip;
            fadingIn.loop = track.loop;
        }

        fadingIn.volume = 0f;
        fadingIn.Play();

        float startVolumeOut = fadingOut.volume;
        float targetVolumeIn = track.volume;
        float elapsed = 0f;

        // Crossfade
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            fadingOut.volume = Mathf.Lerp(startVolumeOut, 0f, t);
            fadingIn.volume = Mathf.Lerp(0f, targetVolumeIn, t);

            yield return null;
        }

        // Ensure final values
        fadingOut.volume = 0f;
        fadingOut.Stop();
        fadingOut.clip = null;
        fadingIn.volume = targetVolumeIn;

        // If we played an intro, schedule the loop now
        if (track.HasIntro && fadingIn.clip == track.introClip)
        {
            float introRemaining = track.introClip.length - fadingIn.time;
            if (introRemaining > 0f)
            {
                yield return new WaitForSecondsRealtime(introRemaining);
            }

            fadingIn.clip = track.clip;
            fadingIn.loop = track.loop;
            fadingIn.volume = track.volume;
            fadingIn.Play();
        }

        _crossfadeCoroutine = null;
    }

    /// <summary>
    /// Fades the active source to silence without starting a new track.
    /// </summary>
    private IEnumerator FadeToSilence(float duration)
    {
        float startVolume = _activeSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _activeSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        _activeSource.volume = 0f;
        _activeSource.Stop();
        _activeSource.clip = null;

        _crossfadeCoroutine = null;
    }

    /// <summary>
    /// Swaps which source is considered active vs inactive.
    /// Called before starting a crossfade.
    /// </summary>
    private void SwapSources()
    {
        (_activeSource, _inactiveSource) = (_inactiveSource, _activeSource);
    }

#if UNITY_EDITOR
    [BoxGroup("Debug (Runtime)")]
    [ShowInInspector, ReadOnly]
    private MusicState DebugCurrentState => _currentState;

    [BoxGroup("Debug (Runtime)")]
    [ShowInInspector, ReadOnly]
    private MusicState DebugPreviousState => _previousState;

    [BoxGroup("Debug (Runtime)")]
    [ShowInInspector, ReadOnly]
    private string DebugActiveClip => _activeSource != null && _activeSource.clip != null
        ? _activeSource.clip.name : "(none)";

    [BoxGroup("Debug (Runtime)")]
    [ShowInInspector, ReadOnly]
    private float DebugActiveVolume => _activeSource != null ? _activeSource.volume : 0f;

    [BoxGroup("Debug (Runtime)")]
    [ShowInInspector, ReadOnly]
    private bool DebugIsCrossfading => _crossfadeCoroutine != null;
#endif
}
