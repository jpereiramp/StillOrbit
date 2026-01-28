# StillOrbit: Audio System Implementation Guide

**Version:** 1.0
**Target:** Unity 6 / C# / Single-player
**Approach:** Iterative, additive implementation — music first, SFX hooks later
**Middleware:** None (no FMOD, no Wwise — Unity-native only)

---

## Table of Contents

1. [Phase 0 — Audio Philosophy & Constraints](#phase-0--audio-philosophy--constraints)
2. [Phase 1 — AudioManager Core](#phase-1--audiomanager-core)
3. [Phase 2 — Music Track & State Data](#phase-2--music-track--state-data)
4. [Phase 3 — Playback & Loop Handling](#phase-3--playback--loop-handling)
5. [Phase 4 — Transitions & Crossfading](#phase-4--transitions--crossfading)
6. [Phase 5 — Priority & Overrides](#phase-5--priority--overrides)
7. [Phase 6 — Gameplay Integration Examples](#phase-6--gameplay-integration-examples)
8. [Phase 7 — Debugging & Common Pitfalls](#phase-7--debugging--common-pitfalls)
9. [Phase 8 — Extension Hooks](#phase-8--extension-hooks-future-systems)

---

## Phase 0 — Audio Philosophy & Constraints

### Goal

Establish the rules, non-negotiables, and mental model before writing a single line of code.

### Design Principles

1. **One authority** — `AudioManager` is the only class that owns `AudioSource` components for music. No other system creates, caches, or manages music playback.

2. **Request, don't command** — Gameplay code says *what mood* it wants (`MusicState.Combat`), never *which clip* to play. The mapping from state to clip lives in data, not in gameplay scripts.

3. **Data-driven** — All track assignments, volumes, fade durations, and priority rankings live in `ScriptableObject` assets. Tuning music never requires opening a C# file.

4. **Graceful by default** — Every transition crossfades. Interruptions are safe. Requesting the same state twice is a no-op. Null clips produce silence, not exceptions.

5. **Boring is good** — The system should be predictable, testable, and invisible to the player. If you notice the audio system, something is wrong.

### What Already Exists

| Component | Location | Relevance |
|-----------|----------|-----------|
| `WeaponAudioData` | `Scripts/Audio/WeaponAudioData.cs` | ScriptableObject pattern for SFX — confirms the project's data-driven audio convention |
| `HitEffectReceiver` | `Scripts/Combat/HitEffectReceiver.cs` | Per-object SFX via `AudioSource.PlayClipAtPoint` — will remain independent |
| `PlayerManager` | `Scripts/Player/PlayerManager.cs` | Singleton pattern — `AudioManager` will follow the same convention |
| `StateMachine<TState, TContext>` | `Scripts/AI/StateMachine/StateMachine.cs` | Generic FSM — proves the project uses enum-based state machines. Music states will follow a simpler variant since music doesn't need validated transitions or contexts |

### What This System Does NOT Handle

- **Positional SFX** — Weapon sounds, hit impacts, footsteps. These stay in their respective systems (`WeaponAudioData`, `HitEffectReceiver`, etc.).
- **UI sounds** — Button clicks, menu swooshes. These will be a separate, simpler concern.
- **Dialogue / VO** — Out of scope entirely.
- **AudioMixer setup** — Not required for Phase 1–6. Extension hook in Phase 8.

### Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                  Gameplay Code                       │
│  (EnemyController, EncounterDirector, Zones, etc.)  │
│                                                      │
│  AudioManager.Instance.SetMusicState(MusicState.X)   │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│                  AudioManager                        │
│  (Singleton MonoBehaviour)                           │
│                                                      │
│  - Owns 2 AudioSources (A/B for crossfade)          │
│  - Reads MusicStateConfig to resolve clips           │
│  - Manages fade coroutines                           │
│  - Tracks current/previous MusicState                │
│  - Enforces priority                                 │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│              MusicStateConfig                        │
│  (ScriptableObject)                                  │
│                                                      │
│  - Maps MusicState enum → MusicTrackData             │
│  - Defines priority per state                        │
│  - Stores global fade duration defaults              │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│              MusicTrackData                          │
│  (Serializable class inside MusicStateConfig)        │
│                                                      │
│  - AudioClip (main loop)                             │
│  - Optional AudioClip (intro)                        │
│  - Volume, fade override, loop flag                  │
└─────────────────────────────────────────────────────┘
```

### Validation Checklist

- [x] No existing music system to conflict with
- [x] Singleton pattern established (`PlayerManager`)
- [x] ScriptableObject data pattern established (`WeaponAudioData`)
- [x] No AudioMixer dependency required
- [x] Existing SFX systems (`WeaponAudioData`, `HitEffectReceiver`) will remain untouched

### What "Done" Looks Like

A shared understanding of what the system is, what it owns, and what it delegates. No code yet.

---

## Phase 1 — AudioManager Core

### Goal

Create the `AudioManager` singleton with two `AudioSource` components for crossfading. No playback logic yet — just the skeleton.

### What Is Implemented

- `AudioManager` MonoBehaviour with singleton access
- Two child `AudioSource` components (Source A and Source B)
- `DontDestroyOnLoad` persistence
- Public API stubs (no implementation yet)

### What Is Intentionally Deferred

- Music state tracking (Phase 2)
- Actual playback (Phase 3)
- Crossfading (Phase 4)
- Priority (Phase 5)

### Code

#### `Assets/Scripts/Audio/AudioManager.cs`

```csharp
using UnityEngine;

/// <summary>
/// Central authority for all music playback and transitions.
/// Singleton — access via AudioManager.Instance.
/// Persists across scene loads.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

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
            musicSourceA.spatialBlend = 0f; // 2D
            musicSourceA.loop = true;
        }

        if (musicSourceB == null)
        {
            musicSourceB = gameObject.AddComponent<AudioSource>();
            musicSourceB.playOnAwake = false;
            musicSourceB.spatialBlend = 0f; // 2D
            musicSourceB.loop = true;
        }
    }

    /// <summary>
    /// Swaps which source is considered active vs inactive.
    /// Called before starting a crossfade.
    /// </summary>
    private void SwapSources()
    {
        (_activeSource, _inactiveSource) = (_inactiveSource, _activeSource);
    }
}
```

### Scene Setup

1. Create an empty GameObject named `AudioManager` in your starting scene.
2. Attach the `AudioManager` component.
3. That's it. The two `AudioSource` components are auto-created at runtime if not manually assigned.

No prefab wiring, no mixer setup, no child objects required.

### Validation Checklist

- [ ] `AudioManager.Instance` is accessible from any script after `Awake`
- [ ] Entering Play Mode creates two `AudioSource` components on the GameObject
- [ ] Loading a new scene does not destroy the `AudioManager`
- [ ] Only one `AudioManager` exists even if the starting scene is re-loaded

### What "Done" Looks Like

An `AudioManager` exists in the scene, persists across loads, and has two silent `AudioSource` components ready for use. No music plays yet.

---

## Phase 2 — Music Track & State Data

### Goal

Define the `MusicState` enum and the `MusicStateConfig` ScriptableObject that maps each state to its track data. After this phase, the system knows *what* to play for each context — but doesn't play it yet.

### What Is Implemented

- `MusicState` enum
- `MusicTrackData` serializable class
- `MusicStateConfig` ScriptableObject
- `AudioManager` references `MusicStateConfig`

### What Is Intentionally Deferred

- Playback logic (Phase 3)
- Transition logic (Phase 4)
- Priority enforcement (Phase 5)

### Code

#### `Assets/Scripts/Audio/MusicState.cs`

```csharp
/// <summary>
/// All possible music contexts in the game.
/// Add new entries as gameplay expands — no code changes required elsewhere
/// as long as a matching entry exists in MusicStateConfig.
/// </summary>
public enum MusicState
{
    None,
    Exploration,
    Combat,
    Boss,
    Calm,
    Base,
    Stealth,
    GameOver
}
```

#### `Assets/Scripts/Audio/MusicTrackData.cs`

```csharp
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
```

#### `Assets/Scripts/Audio/MusicStateConfig.cs`

```csharp
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
```

### Asset Setup

1. Right-click in Project: **Create > StillOrbit > Audio > Music State Config**
2. Name it `MusicStateConfig` and place it in `Assets/Data/Audio/`
3. Add entries for each `MusicState` you currently need:

| State | Priority | Example |
|-------|----------|---------|
| `None` | 0 | (no clip — silence) |
| `Calm` | 10 | Ambient base track |
| `Exploration` | 20 | Open-world track |
| `Stealth` | 30 | Sneaking near enemies |
| `Combat` | 40 | Battle music |
| `Boss` | 50 | Boss encounter |
| `GameOver` | 60 | Death / game over sting (loop = false) |

4. Assign the `MusicStateConfig` asset to the `AudioManager` Inspector field (added below).

### AudioManager Update

Add this field and property to `AudioManager`:

```csharp
[Header("Configuration")]
[SerializeField] private MusicStateConfig musicConfig;

/// <summary>
/// The active music state configuration asset.
/// </summary>
public MusicStateConfig MusicConfig => musicConfig;
```

### Validation Checklist

- [ ] `MusicStateConfig` asset created with at least 3 state entries
- [ ] Each entry has a valid `AudioClip` assigned (except `None`)
- [ ] Priorities are set in ascending order of importance
- [ ] `AudioManager` Inspector shows the `MusicStateConfig` reference
- [ ] `musicConfig.GetEntry(MusicState.Combat)` returns the correct entry at runtime

### What "Done" Looks Like

A ScriptableObject asset exists that maps every music context to its clip, volume, priority, and fade settings. The `AudioManager` holds a reference to it. Still no playback.

---

## Phase 3 — Playback & Loop Handling

### Goal

Make `AudioManager` actually play music. Support looping, one-shot, and intro-then-loop patterns. No crossfading yet — tracks start and stop immediately.

### What Is Implemented

- `SetMusicState()` public API (immediate, no fade)
- Intro → loop scheduling via `PlayScheduled`
- State tracking (`_currentState`, `_previousState`)
- `StopMusic()` API
- `OnMusicStateChanged` event

### What Is Intentionally Deferred

- Crossfading (Phase 4) — for now, new tracks cut in abruptly
- Priority enforcement (Phase 5) — all requests are honored

### Code

Add the following to `AudioManager`:

```csharp
using System;
using UnityEngine;

// --- Add these fields ---

/// <summary>
/// The currently active music state.
/// </summary>
private MusicState _currentState = MusicState.None;

/// <summary>
/// The state that was active before the current one.
/// Used for returning to previous context (e.g., combat ends → back to exploration).
/// </summary>
private MusicState _previousState = MusicState.None;

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

// --- Add these methods ---

/// <summary>
/// Request a music state change.
/// If the requested state matches the current state, this is a no-op.
/// </summary>
/// <param name="newState">The desired music state.</param>
public void SetMusicState(MusicState newState)
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

    Debug.Log($"[Audio] Music state: {_previousState} → {_currentState}");
    OnMusicStateChanged?.Invoke(_previousState, _currentState);
}

/// <summary>
/// Returns to the previous music state.
/// Useful when a temporary state (combat) ends.
/// </summary>
public void ReturnToPreviousState()
{
    SetMusicState(_previousState);
}

/// <summary>
/// Stops all music and sets the state to None.
/// </summary>
public void StopMusic()
{
    _activeSource.Stop();
    _inactiveSource.Stop();
    _previousState = _currentState;
    _currentState = MusicState.None;

    Debug.Log("[Audio] Music stopped.");
    OnMusicStateChanged?.Invoke(_previousState, _currentState);
}

/// <summary>
/// Plays the track associated with a state entry on the active source.
/// Handles intro → loop scheduling when an intro clip is present.
/// </summary>
private void PlayTrack(MusicStateConfig.MusicStateEntry entry)
{
    // Stop whatever is playing
    _activeSource.Stop();
    _inactiveSource.Stop();

    // Null entry or no clip = silence
    if (entry == null || !entry.trackData.IsValid)
    {
        return;
    }

    var track = entry.trackData;

    if (track.HasIntro)
    {
        PlayIntroThenLoop(track);
    }
    else
    {
        _activeSource.clip = track.clip;
        _activeSource.volume = track.volume;
        _activeSource.loop = track.loop;
        _activeSource.Play();
    }
}

/// <summary>
/// Plays a non-looping intro clip, then schedules the main loop
/// to begin seamlessly at the exact sample where the intro ends.
/// Uses the A/B source pair: intro on inactive, loop on active.
/// </summary>
private void PlayIntroThenLoop(MusicTrackData track)
{
    // Play intro on one source
    _inactiveSource.clip = track.introClip;
    _inactiveSource.volume = track.volume;
    _inactiveSource.loop = false;
    _inactiveSource.Play();

    // Schedule the loop to start exactly when the intro ends
    double introDuration = (double)track.introClip.samples / track.introClip.frequency;
    double startTime = AudioSettings.dspTime + introDuration;

    _activeSource.clip = track.clip;
    _activeSource.volume = track.volume;
    _activeSource.loop = track.loop;
    _activeSource.PlayScheduled(startTime);
}
```

### Usage (Immediate — No Fade)

```csharp
// From any gameplay script:
AudioManager.Instance.SetMusicState(MusicState.Combat);

// When combat ends:
AudioManager.Instance.ReturnToPreviousState();

// Silence:
AudioManager.Instance.StopMusic();
```

### Validation Checklist

- [ ] Calling `SetMusicState(MusicState.Exploration)` plays the exploration clip
- [ ] Calling `SetMusicState(MusicState.Exploration)` again while already in Exploration is a no-op
- [ ] Calling `SetMusicState(MusicState.None)` with no clip results in silence (no errors)
- [ ] A track with an intro clip plays the intro once, then loops the main clip seamlessly
- [ ] `ReturnToPreviousState()` after Combat → Exploration returns to Exploration
- [ ] `StopMusic()` silences everything and sets state to `None`
- [ ] `OnMusicStateChanged` fires with correct previous/new values

### What "Done" Looks Like

Music plays in response to state requests. Tracks loop correctly. Intro-then-loop works. Transitions are abrupt (hard cut) — that's expected; crossfading comes next.

---

## Phase 4 — Transitions & Crossfading

### Goal

Replace the hard cuts from Phase 3 with smooth crossfades. The A/B source pattern enables this: fade out the old source while fading in the new one simultaneously.

### What Is Implemented

- Coroutine-based crossfade between sources A and B
- Configurable fade duration (global default + per-track override)
- Safe interruption (starting a new crossfade cancels the previous one)
- Intro-then-loop works with crossfade

### What Is Intentionally Deferred

- Priority enforcement (Phase 5) — still honors all requests

### Code

Replace the `PlayTrack` method and add crossfade logic in `AudioManager`:

```csharp
using System.Collections;
using UnityEngine;

// --- Add this field ---

/// <summary>
/// Reference to the currently running crossfade coroutine.
/// Stored so it can be stopped if a new transition begins mid-fade.
/// </summary>
private Coroutine _crossfadeCoroutine;

// --- Replace PlayTrack with this version ---

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

    AudioSource fadingOut = _inactiveSource;   // was active, now fading out
    AudioSource fadingIn = _activeSource;       // now active, fading in

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
        // Wait for the intro to finish, then start the loop
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
```

Also update `StopMusic` to fade out instead of hard-stopping:

```csharp
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
```

### Why `Time.unscaledDeltaTime`?

Music fades should work even when the game is paused (`Time.timeScale = 0`). Using `unscaledDeltaTime` ensures transitions complete during pause menus, death screens, etc.

### Interruption Safety

If gameplay code calls `SetMusicState` while a crossfade is in progress:

1. The active coroutine is stopped immediately.
2. The source that was fading out may still be at a non-zero volume — that's fine because `SwapSources()` resets roles.
3. The new crossfade begins from whatever volume the current active source is at.

This means rapid state changes (e.g., entering and leaving combat quickly) never stack up or produce audio glitches.

### Validation Checklist

- [ ] Transitioning between two states produces a smooth crossfade (no pop, no silence gap)
- [ ] The fade duration matches the config value (try 2s, verify it takes ~2s)
- [ ] Requesting a new state mid-crossfade cleanly interrupts and starts a new fade
- [ ] `StopMusic()` fades to silence instead of cutting
- [ ] Pausing the game (`Time.timeScale = 0`) does not freeze the fade
- [ ] A track with `fadeOverride = 0.5` uses 0.5s instead of the global default

### What "Done" Looks Like

Every music transition is a smooth crossfade. No pops, no gaps, no stuck audio. Mid-transition interruptions work cleanly.

---

## Phase 5 — Priority & Overrides

### Goal

Enforce a priority hierarchy so that high-priority states (Boss) cannot be overridden by lower-priority requests (Exploration), while still allowing safe return to lower states when the high-priority context ends.

### What Is Implemented

- Priority comparison before state transitions
- `SetMusicState` respects priority (low cannot override high)
- `ForceSetMusicState` bypasses priority (for special cases)
- State stack for returning to the correct previous state

### What Is Intentionally Deferred

- Nothing — the core system is complete after this phase

### Design Decision: Stack vs. Single Previous State

A simple `_previousState` field breaks in this scenario:

1. Player explores → `Exploration`
2. Enters combat → `Combat` (previous = Exploration)
3. Boss appears → `Boss` (previous = Combat)
4. Boss dies → return to... Combat? Exploration?

The answer depends on whether combat is still active. A full stack solves this by letting callers push and pop states explicitly. However, a stack introduces complexity (who pops? what if someone forgets?).

**Decision:** Keep the single `_previousState` for automatic fallback, but provide `ForceSetMusicState` for explicit overrides. Gameplay code is responsible for knowing what state to return to. This is simpler and matches how other systems in the project work (e.g., the companion state machine has `ForceState` for edge cases).

### Code

Update `SetMusicState` and add `ForceSetMusicState` in `AudioManager`:

```csharp
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
```

Also update `ReturnToPreviousState` to use `ForceSetMusicState`:

```csharp
/// <summary>
/// Returns to the previous music state, bypassing priority.
/// Use when the current high-priority context has ended.
/// </summary>
public void ReturnToPreviousState()
{
    ForceSetMusicState(_previousState);
}
```

### Priority Table Convention

Define priorities in increments of 10 to leave room for future states:

| State | Priority | Rationale |
|-------|----------|-----------|
| `None` | 0 | Silence — anything overrides it |
| `Calm` | 10 | Base / safe zone ambient |
| `Exploration` | 20 | Default open-world state |
| `Stealth` | 30 | Slightly elevated tension |
| `Combat` | 40 | Active threat |
| `Boss` | 50 | Highest gameplay priority |
| `GameOver` | 60 | Ultimate override — nothing interrupts this |

### Usage Patterns

```csharp
// --- Entering combat ---
// SetMusicState respects priority. If Boss music is playing,
// this request is silently ignored.
AudioManager.Instance.SetMusicState(MusicState.Combat);

// --- Combat ends ---
// Force is needed because Exploration has lower priority than Combat.
AudioManager.Instance.ForceSetMusicState(MusicState.Exploration);
// OR simply:
AudioManager.Instance.ReturnToPreviousState();

// --- Boss encounter starts ---
// Boss has higher priority than Combat, so this always succeeds.
AudioManager.Instance.SetMusicState(MusicState.Boss);

// --- Boss dies ---
// Force back to whatever makes sense for the gameplay context.
AudioManager.Instance.ForceSetMusicState(MusicState.Exploration);
```

### Validation Checklist

- [ ] While Combat music plays, requesting Exploration is ignored (returns false)
- [ ] While Combat music plays, requesting Boss succeeds (higher priority)
- [ ] `ForceSetMusicState(MusicState.Exploration)` works even during Boss music
- [ ] `ReturnToPreviousState()` crossfades back correctly
- [ ] Priority values in the config asset match the intended hierarchy

### What "Done" Looks Like

The priority system prevents low-importance music from accidentally overriding high-importance music. Explicit `ForceSetMusicState` and `ReturnToPreviousState` allow controlled fallback. The core music system is now functionally complete.

---

## Phase 6 — Gameplay Integration Examples

### Goal

Show concrete examples of how existing gameplay systems trigger music state changes. No new code in `AudioManager` — this phase is about integration patterns.

### Pattern 1: Combat Music via Encounter System

The `EncounterDirector` or equivalent combat-detection system should own the Combat ↔ Exploration transition.

```csharp
// In your combat detection system (e.g., EncounterDirector, CombatZoneTrigger, etc.)

public class CombatMusicTrigger : MonoBehaviour
{
    private bool _inCombat;

    /// <summary>
    /// Called when combat starts (enemies are aggro'd, encounter begins, etc.)
    /// </summary>
    public void OnCombatStarted()
    {
        if (_inCombat) return;
        _inCombat = true;

        AudioManager.Instance.SetMusicState(MusicState.Combat);
    }

    /// <summary>
    /// Called when all enemies are dead or de-aggro'd.
    /// </summary>
    public void OnCombatEnded()
    {
        if (!_inCombat) return;
        _inCombat = false;

        AudioManager.Instance.ReturnToPreviousState();
    }
}
```

### Pattern 2: Zone-Based Music via Triggers

For areas with distinct music (base camp, caves, specific biomes):

```csharp
using UnityEngine;

/// <summary>
/// Attach to a trigger collider to change music when the player enters/exits.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MusicZoneTrigger : MonoBehaviour
{
    [SerializeField] private MusicState zoneState = MusicState.Calm;
    [SerializeField] private MusicState exitState = MusicState.Exploration;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        AudioManager.Instance.SetMusicState(zoneState);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        AudioManager.Instance.ForceSetMusicState(exitState);
    }
}
```

### Pattern 3: Boss Music

```csharp
// In your boss controller or encounter script

public void OnBossEncounterStart()
{
    AudioManager.Instance.SetMusicState(MusicState.Boss);
}

public void OnBossDefeated()
{
    // Don't use ReturnToPreviousState here — previous was probably Combat.
    // Force directly to the desired post-boss state.
    AudioManager.Instance.ForceSetMusicState(MusicState.Exploration);
}
```

### Pattern 4: Game Over / Death

```csharp
// In your death handler or GameManager

public void OnPlayerDeath()
{
    AudioManager.Instance.SetMusicState(MusicState.GameOver);
}

public void OnRespawn()
{
    AudioManager.Instance.ForceSetMusicState(MusicState.Exploration);
}
```

### Pattern 5: Initial Music on Scene Load

```csharp
// In a scene initialization script or GameManager.Start()

private void Start()
{
    AudioManager.Instance.SetMusicState(MusicState.Exploration);
}
```

### Anti-Patterns to Avoid

```csharp
// BAD: Playing clips directly
AudioManager.Instance.GetComponent<AudioSource>().PlayOneShot(combatClip);

// BAD: Checking what clip is playing
if (AudioManager.Instance.CurrentClip == bossMusic) { ... }

// BAD: Setting volume from gameplay code
AudioManager.Instance.SetVolume(0.5f); // Volume is data-driven

// BAD: Multiple systems fighting over the same state
// EnemyA: SetMusicState(Combat)
// EnemyB: SetMusicState(Combat)  ← This is fine (no-op), but...
// EnemyA dies: ReturnToPreviousState() ← Oops, combat ended for ONE enemy!

// GOOD: Use a centralized combat tracker, not per-enemy music calls
```

### Integration Principle

**One system per transition.** Don't have individual enemies triggering Combat music. Have your encounter/combat tracking system do it. The `AudioManager` doesn't know or care what an "enemy" is.

### Validation Checklist

- [ ] Entering a combat encounter crossfades to Combat music
- [ ] Leaving combat crossfades back to the previous state
- [ ] Walking into a base zone plays Calm music
- [ ] Boss encounter correctly overrides Combat music
- [ ] Game Over music plays on player death
- [ ] Respawn restores Exploration music
- [ ] No gameplay script references an `AudioClip` directly

### What "Done" Looks Like

All major gameplay transitions (combat, zones, bosses, death) trigger the correct music state. Gameplay scripts contain zero references to audio clips or volumes.

---

## Phase 7 — Debugging & Common Pitfalls

### Goal

Provide tools and knowledge for diagnosing audio issues quickly.

### Debug Logging

All `AudioManager` state changes are logged with the `[Audio]` prefix. Filter your console with `[Audio]` to see only music transitions:

```
[Audio] Music state: None → Exploration
[Audio] Music state: Exploration → Combat
[Audio] Ignored Exploration (priority 20) — current Combat has higher priority (40)
[Audio] Music state (forced): Combat → Exploration
[Audio] Music fading to silence.
```

### Optional: Inspector Debug Display

Add these to `AudioManager` for at-a-glance state inspection in the Inspector during Play Mode:

```csharp
using Sirenix.OdinInspector;

// Add these fields to AudioManager:

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
```

### Common Pitfalls

#### 1. Music doesn't play after scene load

**Cause:** `AudioManager` was not in the first loaded scene, or `DontDestroyOnLoad` was not called.

**Fix:** Ensure `AudioManager` exists in your startup scene. It persists automatically.

#### 2. Music restarts when entering the same state

**Cause:** Calling `ForceSetMusicState` with the current state (bypasses the no-op check on a version without the guard).

**Fix:** Both `SetMusicState` and `ForceSetMusicState` check `newState == _currentState` and return early. If music still restarts, something is calling `StopMusic()` before re-setting the state.

#### 3. Music doesn't transition — stuck on one track

**Cause:** The current state has higher priority than the requested state.

**Fix:** Check your priority values in `MusicStateConfig`. Use `ForceSetMusicState` if you explicitly want to override.

#### 4. Two overlapping tracks (not crossfading)

**Cause:** A source was not stopped before starting a new track, or a coroutine was not cancelled.

**Fix:** `PlayTrack` always cancels the previous coroutine. If this still happens, check that no other script is calling `Play()` on the `AudioManager`'s `AudioSource` components directly.

#### 5. Crossfade pops or clicks

**Cause:** Fade duration is too short (< 0.1s) or the audio clip has a loud transient at its start.

**Fix:** Use fade durations >= 0.5s. If a specific track still pops, add a short silence pad to the beginning of the audio file.

#### 6. Intro-then-loop has a gap

**Cause:** The intro and loop clips were exported with different sample rates, or there's silence padding at the end of the intro.

**Fix:** Ensure both clips use the same sample rate (e.g., 44100 Hz). Trim trailing silence from the intro clip.

#### 7. `ReturnToPreviousState` goes to the wrong state

**Cause:** Multiple state changes happened, and `_previousState` only tracks the last one.

**Fix:** This is by design. If you need more control, use `ForceSetMusicState` with the explicit target state instead of relying on `ReturnToPreviousState`.

### Validation Checklist

- [ ] Console logs show `[Audio]` entries for every state transition
- [ ] Inspector shows current state, previous state, active clip, and volume at runtime
- [ ] Priority rejections are logged clearly

### What "Done" Looks Like

You can diagnose any music issue by reading the console log or glancing at the Inspector. No guesswork.

---

## Phase 8 — Extension Hooks (Future Systems)

### Goal

Document how the system can be extended without modifying its core. These are **not implemented now** — they're documented so future work has a clear path.

### Extension 1: Volume Settings (Player Preferences)

Add a master music volume that scales all track volumes:

```csharp
// Add to AudioManager
private float _masterMusicVolume = 1f;

public float MasterMusicVolume
{
    get => _masterMusicVolume;
    set
    {
        _masterMusicVolume = Mathf.Clamp01(value);
        // Apply to active source immediately
        if (_activeSource != null && _activeSource.isPlaying)
        {
            var entry = musicConfig.GetEntry(_currentState);
            if (entry != null)
            {
                _activeSource.volume = entry.trackData.volume * _masterMusicVolume;
            }
        }
    }
}
```

The crossfade coroutine would multiply target volume by `_masterMusicVolume`.

### Extension 2: AudioMixer Integration

When you need global volume sliders, ducking, or effects:

1. Create an `AudioMixer` asset with groups: `Master > Music`, `Master > SFX`
2. Assign the Music group to both `AudioSource` components on `AudioManager`
3. Expose the Music group's volume as a parameter
4. Control it via `AudioMixer.SetFloat("MusicVolume", dBValue)`

This requires zero changes to the state/transition logic.

### Extension 3: Dynamic Intensity

For seamless intensity scaling within a single state (e.g., combat tension ramping up):

```csharp
// Concept — not implemented
[Serializable]
public class IntensityLayer
{
    public AudioClip clip;
    [Range(0f, 1f)] public float intensityThreshold;
}

// MusicTrackData gains:
public IntensityLayer[] intensityLayers;

// AudioManager gains:
public void SetMusicIntensity(float intensity) { /* fade layers in/out */ }
```

This would require additional `AudioSource` components and a more sophisticated fade system, but the public API stays simple: `SetMusicIntensity(0.8f)`.

### Extension 4: SFX Manager

SFX can coexist alongside this system without refactoring:

```csharp
// Concept — SFX remains separate from music
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    public void PlaySFX(SFXData data, Vector3 position)
    {
        data.Play(position);
    }
}
```

Existing `WeaponAudioData` and `HitEffectReceiver` would continue to work as-is. The `SFXManager` is optional — it only becomes useful when you want pooled audio sources or global SFX volume control.

### Extension 5: Ambient Layers

For environmental audio (wind, rain, cave echoes) that play alongside music:

- Add a separate pair of `AudioSource` components for ambient
- Use the same crossfade pattern
- Trigger via `AudioManager.SetAmbientState()` or a separate `AmbientManager`

### Extension 6: Event-Based Stingers

For short musical stings (item pickup jingle, quest complete fanfare) that play over the current music without interrupting it:

```csharp
// Concept — add a dedicated one-shot source
public void PlayStinger(AudioClip stinger, float volume = 1f)
{
    _stingerSource.PlayOneShot(stinger, volume);
}
```

This doesn't affect the state machine at all.

### What "Done" Looks Like

Nothing is implemented. But when any of these features are needed, the path forward is clear, and the core system doesn't need to change.

---

## Complete File Listing

When all phases are implemented, the audio system consists of:

```
Assets/Scripts/Audio/
├── AudioManager.cs          (Singleton, crossfade, state management)
├── MusicState.cs            (Enum — one line per state)
├── MusicTrackData.cs        (Serializable class — clip, volume, loop, intro)
├── MusicStateConfig.cs      (ScriptableObject — maps states to tracks + priority)
└── WeaponAudioData.cs       (Existing — unchanged)

Assets/Data/Audio/
└── MusicStateConfig.asset   (One asset — all music assignments)
```

Four new files. One new asset. One new GameObject in the scene.

---

## Quick Reference: Public API

```csharp
// --- State Changes ---
bool  AudioManager.Instance.SetMusicState(MusicState state)    // Priority-aware
void  AudioManager.Instance.ForceSetMusicState(MusicState state) // Bypass priority
void  AudioManager.Instance.ReturnToPreviousState()             // Go back
void  AudioManager.Instance.StopMusic()                         // Fade to silence

// --- Read-Only State ---
MusicState AudioManager.Instance.CurrentState
MusicState AudioManager.Instance.PreviousState

// --- Events ---
AudioManager.Instance.OnMusicStateChanged += (prev, next) => { };
```

That's the entire surface area. Everything else is internal.
