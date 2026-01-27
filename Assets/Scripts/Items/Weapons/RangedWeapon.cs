using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Item))]
public class RangedWeapon : MonoBehaviour, IUsable, IWeapon
{
    [Header("Data Reference")]
    [SerializeField] private RangedWeaponData weaponData;

    [Header("Fire Point")]
    [SerializeField] private Transform firePoint;  // Where raycast originates

    [Header("Audio")]
    [SerializeField] private AudioClip fireSFX;
    [SerializeField] private AudioClip emptySFX;
    [SerializeField] private AudioClip reloadSFX;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFire;
    [SerializeField] private UnityEvent onHit;
    [SerializeField] private UnityEvent onReloadStart;
    [SerializeField] private UnityEvent onReloadComplete;
    [SerializeField] private UnityEvent onEmpty;

    [Header("Layers")]
    [SerializeField] private LayerMask hitLayers = ~0;

    // Runtime state
    private int currentAmmo;
    private float lastFireTime;
    private bool isReloading;
    private float reloadStartTime;

    private Transform ownerTransform;

    // Events
    public event Action<int, int> OnAmmoChanged;  // (current, max)
    public event Action OnReloadStarted;
    public event Action OnReloadCompleted;

    // Public Accessors
    public bool CanUse => !isReloading && HasAmmo && CooldownElapsed;
    private bool HasAmmo => !weaponData.UseAmmo || currentAmmo > 0;
    private bool CooldownElapsed => Time.time >= lastFireTime + weaponData.FireInterval;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => weaponData.ClipSize;
    public bool IsReloading => isReloading;
    public WeaponData WeaponData => weaponData;

    private void Awake()
    {
        currentAmmo = weaponData != null ? weaponData.ClipSize : 0;

        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    private void Update()
    {
        if (isReloading && Time.time >= reloadStartTime + weaponData.ReloadTime)
        {
            CompleteReload();
        }
    }

    public UseResult Use(GameObject user)
    {
        if (weaponData == null)
            return UseResult.Failed;

        ownerTransform = user.transform;

        // Handle reloading
        if (isReloading)
            return UseResult.Failed;

        // Check ammo
        if (weaponData.UseAmmo && currentAmmo <= 0)
        {
            // Play empty sound
            PlaySound(emptySFX);
            onEmpty?.Invoke();
            return UseResult.Failed;
        }

        // Check cooldown
        if (!CooldownElapsed)
            return UseResult.Failed;

        // Fire!

        return UseResult.Success;
    }

    private void Fire()
    {
        lastFireTime = Time.time;

        // Consume ammo
        if (weaponData.UseAmmo)
        {
            currentAmmo--;
            OnAmmoChanged?.Invoke(currentAmmo, weaponData.ClipSize);
        }

        // Fire event and SFX
        PlaySound(fireSFX);
        onFire?.Invoke();

        // Perform raycast(s)
        for (int i = 0; i < weaponData.PelletsPerShot; i++)
        {
            PerformRaycast();
        }
    }

    private void PerformRaycast()
    {
        Vector3 origin = firePoint.position;
        Vector3 direction = GetFireDirection();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, weaponData.MaxRange, hitLayers))
        {
            ProcessHit(hit);
        }

        // Debug visualization
        Debug.DrawRay(origin, direction * weaponData.MaxRange, Color.red, 0.1f);
    }

    private Vector3 GetFireDirection()
    {
        Vector3 baseDirection = firePoint.forward;

        if (weaponData.SpreadAngle <= 0f)
        {
            return baseDirection;
        }

        // Apply random spread within cone
        float spreadRad = weaponData.SpreadAngle * Mathf.Deg2Rad;
        float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
        float randomRadius = UnityEngine.Random.Range(0f, Mathf.Tan(spreadRad));

        Vector3 perpendicular = Vector3.Cross(baseDirection, Vector3.up).normalized;
        if (perpendicular.sqrMagnitude < 0.01f)
        {
            perpendicular = Vector3.Cross(baseDirection, Vector3.right).normalized;
        }
        Vector3 perpendicular2 = Vector3.Cross(baseDirection, perpendicular).normalized;

        Vector3 offset = perpendicular * Mathf.Cos(randomAngle) * randomRadius +
                       perpendicular2 * Mathf.Sin(randomAngle) * randomRadius;

        return (baseDirection + offset).normalized;
    }

    private void ProcessHit(RaycastHit hit)
    {
        // Find IDamageable on hit object
        IDamageable damageable = hit.collider.GetComponent<IDamageable>();
        if (damageable == null)
        {
            damageable = hit.collider.GetComponentInParent<IDamageable>();
        }

        if (damageable != null)
        {
            // Determine damage type from target if possible
            DamageType damageType = DamageType.Flesh;  // Default for ranged

            // Check if target specifies a damage type
            var damageReceiver = hit.collider.GetComponent<IDamageTypeProvider>();
            if (damageReceiver != null)
            {
                damageType = damageReceiver.DamageType;
            }

            float damage = weaponData.GetDamage(damageType);
            damageable.TakeDamage(damage, damageType, ownerTransform.gameObject);

            NotifyHit();
        }

        // Always try to play hit effects
        var hitEffects = hit.collider.GetComponent<HitEffectReceiver>();
        if (hitEffects == null)
        {
            hitEffects = hit.collider.GetComponentInParent<HitEffectReceiver>();
        }
        hitEffects?.PlayHitEffect(hit.point, hit.normal);
    }

    public void NotifyHit()
    {
        onHit?.Invoke();
    }

    public float GetDamage(DamageType targetType)
    {
        return weaponData != null ? weaponData.GetDamage(targetType) : 0f;
    }

    public float GetRange()
    {
        return weaponData != null ? weaponData.MaxRange : 0f;
    }

    /// <summary>
    /// Start reloading. Can be called externally (e.g., by reload input).
    /// </summary>
    public void StartReload()
    {
        if (isReloading) return;
        if (currentAmmo >= weaponData.ClipSize) return;  // Already full

        isReloading = true;
        reloadStartTime = Time.time;

        PlaySound(reloadSFX);
        onReloadStart?.Invoke();
        OnReloadStarted?.Invoke();
    }

    private void CompleteReload()
    {
        isReloading = false;
        currentAmmo = weaponData.ClipSize;

        onReloadComplete?.Invoke();
        OnReloadCompleted?.Invoke();
        OnAmmoChanged?.Invoke(currentAmmo, weaponData.ClipSize);
    }

    /// <summary>
    /// Force reload cancellation (e.g., when unequipped).
    /// </summary>
    public void CancelReload()
    {
        isReloading = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, firePoint.position, sfxVolume);
        }
    }

    private void OnDisable()
    {
        // Cancel reload when unequipped
        CancelReload();
    }
}