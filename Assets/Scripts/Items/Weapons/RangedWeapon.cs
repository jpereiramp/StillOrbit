using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Item))]
public class RangedWeapon : MonoBehaviour, IUsable, IWeapon
{
    [Header("Data Reference")]
    [SerializeField] private RangedWeaponData weaponData;

    [Header("Fire Point")]
    [SerializeField] private Transform firePoint;  // Where raycast originates

    [Header("Aim Controller")]
    [SerializeField] private PlayerAimController aimController;  // Reference to player's aim controller

    [Header("Audio")]
    [Tooltip("Optional: Use WeaponAudioData for advanced audio (randomization, pitch variation). If null, uses individual clips below.")]
    [SerializeField] private WeaponAudioData audioData;
    [SerializeField] private AudioSource audioSource; // Optional: for pitch variation support
    [Space]
    [Tooltip("Used if WeaponAudioData is not assigned")]
    [SerializeField] private AudioClip fireSFX;
    [SerializeField] private AudioClip emptySFX;
    [SerializeField] private AudioClip reloadSFX;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Visual Effects")]
    [Tooltip("Prefab spawned at fire point on each shot (can contain particles, lights, etc.)")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashLifetime = 0.5f;

    [Tooltip("Prefab for beam/tracer effect. Must have a LineRenderer component.")]
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private float beamDuration = 0.1f;

    [Tooltip("Prefab spawned at impact point")]
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private float impactVFXLifetime = 2f;

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

        // Try to find AimController if not assigned
        if (aimController == null)
        {
            aimController = FindAnyObjectByType<PlayerAimController>();
        }
    }

    /// <summary>
    /// Set the aim controller reference (called by equipment system when equipped).
    /// </summary>
    public void SetAimController(PlayerAimController controller)
    {
        aimController = controller;
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
        Debug.Log($"[RangedWeapon] Use() called. WeaponData: {(weaponData != null ? weaponData.name : "NULL")}");

        if (weaponData == null)
        {
            Debug.LogError("[RangedWeapon] WeaponData is null!");
            return UseResult.Failed;
        }

        ownerTransform = user.transform;

        // Handle reloading
        if (isReloading)
        {
            Debug.Log("[RangedWeapon] Cannot fire - currently reloading");
            return UseResult.Failed;
        }

        // Check ammo
        if (weaponData.UseAmmo && currentAmmo <= 0)
        {
            Debug.Log("[RangedWeapon] Out of ammo - playing empty click");
            PlayEmptySound();
            onEmpty?.Invoke();
            StartReload(); // Auto-reload when empty
            return UseResult.Failed;
        }

        // Check cooldown
        if (!CooldownElapsed)
        {
            return UseResult.Failed; // Silent fail for cooldown (expected during rapid fire)
        }

        // Fire!
        Debug.Log($"[RangedWeapon] Firing! Ammo: {currentAmmo}/{weaponData.ClipSize}");
        Fire();

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
            Debug.Log($"[RangedWeapon] Ammo consumed. Remaining: {currentAmmo}/{weaponData.ClipSize}");
        }

        // Fire event and SFX
        PlayFireSound();
        onFire?.Invoke();

        // Spawn muzzle flash
        SpawnMuzzleFlash();

        // Perform raycast(s)
        for (int i = 0; i < weaponData.PelletsPerShot; i++)
        {
            PerformRaycast();
        }
    }

    private void PerformRaycast()
    {
        if (aimController == null)
        {
            Debug.LogError($"[RangedWeapon] AimController is null! Cannot perform raycast.");
            return;
        }

        // Get aim direction from PlayerAimController
        aimController.GetAimRay(out Vector3 aimOrigin, out Vector3 aimDirection);

        // Apply weapon spread to the aim direction
        Vector3 direction = ApplySpread(aimDirection);

        // Raycast from fire point (visual origin) but use aim direction
        Vector3 origin = firePoint.position;
        Vector3 endPoint = origin + direction * weaponData.MaxRange;

        Debug.Log($"[RangedWeapon] PerformRaycast - Origin: {origin}, AimDirection: {aimDirection}, WithSpread: {direction}");

        if (Physics.Raycast(origin, direction, out RaycastHit hit, weaponData.MaxRange, hitLayers))
        {
            endPoint = hit.point;
            Debug.Log($"[RangedWeapon] Raycast HIT - HitPoint: {hit.point}, HitObject: {hit.collider.name}");
            ProcessHit(hit);
        }
        else
        {
            Debug.Log($"[RangedWeapon] Raycast MISS - Using max range endpoint");
        }

        // Show beam/tracer if configured
        if (beamPrefab != null)
        {
            ShowBeam(origin, endPoint);
        }

        // Debug visualization (green for actual ray direction)
        Debug.DrawRay(origin, direction * weaponData.MaxRange, Color.green, 1f);
        // Debug line from origin to endpoint (cyan)
        Debug.DrawLine(origin, endPoint, Color.cyan, 1f);
    }

    /// <summary>
    /// Apply weapon spread to the base aim direction.
    /// </summary>
    private Vector3 ApplySpread(Vector3 baseDirection)
    {
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
        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;
        Vector3 forceDirection = (hitPoint - firePoint.position).normalized;

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

        // Apply physics impact force
        ApplyImpactForce(hit, forceDirection);

        // Always try to play hit effects on target
        var hitEffects = hit.collider.GetComponent<HitEffectReceiver>();
        if (hitEffects == null)
        {
            hitEffects = hit.collider.GetComponentInParent<HitEffectReceiver>();
        }

        if (hitEffects != null)
        {
            hitEffects.PlayHitEffect(hitPoint, hitNormal);
        }
        else
        {
            // Spawn generic impact VFX if target has no HitEffectReceiver
            SpawnImpactVFX(hitPoint, hitNormal);
        }
    }

    private void ApplyImpactForce(RaycastHit hit, Vector3 forceDirection)
    {
        if (weaponData.ImpactForce <= 0f) return;

        // Check for explosion force (area effect)
        if (weaponData.HasExplosionForce)
        {
            ApplyExplosionForce(hit.point);
            return;
        }

        // Direct force on the hit object
        Rigidbody hitRigidbody = hit.collider.attachedRigidbody;
        if (hitRigidbody != null && !hitRigidbody.isKinematic)
        {
            // Apply force at the hit point for realistic torque
            Vector3 force = forceDirection * weaponData.ImpactForce;
            hitRigidbody.AddForceAtPosition(force, hit.point, weaponData.ImpactForceMode);

            Debug.Log($"[RangedWeapon] Applied impact force {weaponData.ImpactForce} to {hitRigidbody.name}");
        }
    }

    private void ApplyExplosionForce(Vector3 explosionCenter)
    {
        // Find all colliders in explosion radius
        Collider[] affectedColliders = Physics.OverlapSphere(
            explosionCenter,
            weaponData.ExplosionRadius,
            hitLayers
        );

        HashSet<Rigidbody> processedBodies = new HashSet<Rigidbody>();

        foreach (Collider col in affectedColliders)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;
            if (processedBodies.Contains(rb)) continue; // Prevent applying force multiple times

            processedBodies.Add(rb);

            rb.AddExplosionForce(
                weaponData.ImpactForce,
                explosionCenter,
                weaponData.ExplosionRadius,
                weaponData.ExplosionUpwardModifier,
                weaponData.ImpactForceMode
            );

            Debug.Log($"[RangedWeapon] Applied explosion force to {rb.name}");
        }
    }

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null) return;

        GameObject muzzleFlash = Instantiate(
            muzzleFlashPrefab,
            firePoint.position,
            firePoint.rotation,
            firePoint // Parent to fire point so it follows the weapon
        );

        Destroy(muzzleFlash, muzzleFlashLifetime);
    }

    private void SpawnImpactVFX(Vector3 position, Vector3 normal)
    {
        if (impactVFXPrefab == null) return;

        GameObject impact = Instantiate(impactVFXPrefab, position, Quaternion.LookRotation(normal));
        Destroy(impact, impactVFXLifetime);
    }

    private void ShowBeam(Vector3 start, Vector3 end)
    {
        if (beamPrefab == null) return;

        Debug.Log($"[RangedWeapon] ShowBeam called - Start: {start}, End: {end}");
        Debug.Log($"[RangedWeapon] FirePoint position: {firePoint.position}, forward: {firePoint.forward}");
        Debug.Log($"[RangedWeapon] Direction to end: {(end - start).normalized}");

        // Instantiate at world origin with no rotation to ensure useWorldSpace positions work correctly
        GameObject beamInstance = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        LineRenderer lineRenderer = beamInstance.GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            Debug.Log($"[RangedWeapon] LineRenderer useWorldSpace: {lineRenderer.useWorldSpace}, positionCount: {lineRenderer.positionCount}");

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);

            Debug.Log($"[RangedWeapon] After SetPosition - Pos0: {lineRenderer.GetPosition(0)}, Pos1: {lineRenderer.GetPosition(1)}");
        }
        else
        {
            Debug.LogWarning("[RangedWeapon] Beam prefab is missing LineRenderer component!");
        }

        Destroy(beamInstance, beamDuration);
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

        Debug.Log("[RangedWeapon] Reload started");
        PlayReloadSound();
        onReloadStart?.Invoke();
        OnReloadStarted?.Invoke();
    }

    private void CompleteReload()
    {
        isReloading = false;
        currentAmmo = weaponData.ClipSize;

        Debug.Log($"[RangedWeapon] Reload complete. Ammo: {currentAmmo}/{weaponData.ClipSize}");
        PlayReloadCompleteSound();
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

    private void PlayFireSound()
    {
        if (audioData != null)
        {
            audioData.PlayFireSound(firePoint.position, audioSource);
        }
        else if (fireSFX != null)
        {
            AudioSource.PlayClipAtPoint(fireSFX, firePoint.position, sfxVolume);
        }
    }

    private void PlayEmptySound()
    {
        if (audioData != null)
        {
            audioData.PlayEmptySound(firePoint.position);
        }
        else if (emptySFX != null)
        {
            AudioSource.PlayClipAtPoint(emptySFX, firePoint.position, sfxVolume);
        }
    }

    private void PlayReloadSound()
    {
        if (audioData != null)
        {
            audioData.PlayReloadStartSound(firePoint.position);
        }
        else if (reloadSFX != null)
        {
            AudioSource.PlayClipAtPoint(reloadSFX, firePoint.position, sfxVolume);
        }
    }

    private void PlayReloadCompleteSound()
    {
        if (audioData != null)
        {
            audioData.PlayReloadCompleteSound(firePoint.position);
        }
        // No fallback for reload complete - single clip covers both in simple mode
    }

    private void OnDisable()
    {
        // Cancel reload when unequipped
        CancelReload();
    }
}