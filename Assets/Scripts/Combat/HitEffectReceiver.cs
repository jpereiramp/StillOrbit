using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Defines the hit effects (SFX/VFX) that play when this object is hit.
/// Attach to damageable objects to customize their hit feedback.
/// If not present, PlayerCombatManager uses fallback effects.
/// </summary>
public class HitEffectReceiver : MonoBehaviour
{
    [BoxGroup("Audio")]
    [Tooltip("Sound effect played when this object is hit")]
    [SerializeField]
    private AudioClip hitSound;

    [BoxGroup("Audio")]
    [Tooltip("Volume of the hit sound")]
    [Range(0f, 1f)]
    [SerializeField]
    private float hitSoundVolume = 1f;

    [BoxGroup("Visual")]
    [Tooltip("Particle effect spawned at hit point")]
    [SerializeField]
    private GameObject hitVFXPrefab;

    [BoxGroup("Visual")]
    [Tooltip("How long the VFX lives before being destroyed")]
    [SerializeField]
    private float vfxLifetime = 2f;

    [BoxGroup("Visual")]
    [Tooltip("(Player Only!) Vignette object hit effect")]
    [SerializeField]
    private GameObject vignetteHitEffect;

    /// <summary>
    /// Plays the hit effects at the specified position.
    /// </summary>
    /// <param name="hitPoint">World position where the hit occurred</param>
    /// <param name="hitNormal">Normal of the hit surface (for VFX orientation)</param>
    public void PlayHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        PlayHitSound(hitPoint);
        SpawnHitVFX(hitPoint, hitNormal);
        BlinkDamageVignette();
    }

    /// <summary>
    /// Plays the hit effects at this object's position.
    /// </summary>
    public void PlayHitEffect()
    {
        PlayHitEffect(transform.position, Vector3.up);
    }

    private void BlinkDamageVignette()
    {
        if (vignetteHitEffect == null) return;

        StartCoroutine(BlinkVignetteCoroutine());
    }

    private IEnumerator BlinkVignetteCoroutine()
    {
        vignetteHitEffect.SetActive(true);
        yield return new WaitForSeconds(0.2f);
        vignetteHitEffect.SetActive(false);
    }

    private void PlayHitSound(Vector3 position)
    {
        if (hitSound == null) return;

        AudioSource.PlayClipAtPoint(hitSound, position, hitSoundVolume);
    }

    private void SpawnHitVFX(Vector3 position, Vector3 normal)
    {
        if (hitVFXPrefab == null) return;

        var rotation = Quaternion.LookRotation(normal);
        var vfx = Instantiate(hitVFXPrefab, position, rotation);

        if (vfxLifetime > 0)
        {
            Destroy(vfx, vfxLifetime);
        }
    }

    public bool HasHitSound => hitSound != null;
    public bool HasHitVFX => hitVFXPrefab != null;
}
