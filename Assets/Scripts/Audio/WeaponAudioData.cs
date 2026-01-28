using UnityEngine;

/// <summary>
/// Audio configuration for weapons.
/// Allows for randomized sounds and volume control.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon Audio", menuName = "StillOrbit/Audio/Weapon Audio")]
public class WeaponAudioData : ScriptableObject
{
    [Header("Fire Sounds")]
    [Tooltip("Array of fire sounds to randomly choose from")]
    public AudioClip[] fireSounds;
    [Range(0f, 1f)] public float fireVolume = 1f;
    [Range(0f, 0.3f)] public float firePitchVariation = 0.1f;

    [Header("Reload Sounds")]
    public AudioClip reloadStart;
    public AudioClip reloadComplete;
    [Range(0f, 1f)] public float reloadVolume = 1f;

    [Header("Empty/Dry Fire")]
    public AudioClip emptyClick;
    [Range(0f, 1f)] public float emptyVolume = 0.8f;

    [Header("Impact Sounds")]
    [Tooltip("Array of impact sounds to randomly choose from")]
    public AudioClip[] impactSounds;
    [Range(0f, 1f)] public float impactVolume = 1f;

    /// <summary>
    /// Gets a random fire sound from the array.
    /// </summary>
    public AudioClip GetRandomFireSound()
    {
        if (fireSounds == null || fireSounds.Length == 0) return null;
        return fireSounds[Random.Range(0, fireSounds.Length)];
    }

    /// <summary>
    /// Gets a random impact sound from the array.
    /// </summary>
    public AudioClip GetRandomImpactSound()
    {
        if (impactSounds == null || impactSounds.Length == 0) return null;
        return impactSounds[Random.Range(0, impactSounds.Length)];
    }

    /// <summary>
    /// Plays a fire sound at the specified position with pitch variation.
    /// </summary>
    public void PlayFireSound(Vector3 position, AudioSource optionalSource = null)
    {
        AudioClip clip = GetRandomFireSound();
        if (clip == null) return;

        if (optionalSource != null)
        {
            optionalSource.pitch = 1f + Random.Range(-firePitchVariation, firePitchVariation);
            optionalSource.PlayOneShot(clip, fireVolume);
        }
        else
        {
            // Note: PlayClipAtPoint doesn't support pitch variation
            AudioSource.PlayClipAtPoint(clip, position, fireVolume);
        }
    }

    /// <summary>
    /// Plays the empty click sound at the specified position.
    /// </summary>
    public void PlayEmptySound(Vector3 position)
    {
        if (emptyClick != null)
        {
            AudioSource.PlayClipAtPoint(emptyClick, position, emptyVolume);
        }
    }

    /// <summary>
    /// Plays the reload start sound at the specified position.
    /// </summary>
    public void PlayReloadStartSound(Vector3 position)
    {
        if (reloadStart != null)
        {
            AudioSource.PlayClipAtPoint(reloadStart, position, reloadVolume);
        }
    }

    /// <summary>
    /// Plays the reload complete sound at the specified position.
    /// </summary>
    public void PlayReloadCompleteSound(Vector3 position)
    {
        if (reloadComplete != null)
        {
            AudioSource.PlayClipAtPoint(reloadComplete, position, reloadVolume);
        }
    }

    /// <summary>
    /// Plays a random impact sound at the specified position.
    /// </summary>
    public void PlayImpactSound(Vector3 position)
    {
        AudioClip clip = GetRandomImpactSound();
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, impactVolume);
        }
    }
}
