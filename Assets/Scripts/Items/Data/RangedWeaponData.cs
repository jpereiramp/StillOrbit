using UnityEngine;

[CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "StillOrbit/Items/Weapons/Ranged Weapon")]
public class RangedWeaponData : WeaponData
{
    [Header("Ranged Properties")]
    [SerializeField, Min(0.01f)] private float fireRate = 5f;  // Shots per second
    [SerializeField, Min(1)] private int clipSize = 30;
    [SerializeField, Min(0f)] private float reloadTime = 2f;
    [SerializeField, Min(0f)] private float maxRange = 100f;

    [Header("Ammo")]
    [SerializeField] private bool useAmmo = true;
    [SerializeField] private ItemData ammoType;  // Null = infinite ammo

    [Header("Spread")]
    [SerializeField, Range(0f, 45f)] private float spreadAngle = 0f;  // Degrees
    [SerializeField, Min(1)] private int pelletsPerShot = 1;  // For shotguns

    [Header("Impact Physics")]
    [Tooltip("Force applied to rigidbodies on hit")]
    [SerializeField, Min(0f)] private float impactForce = 10f;
    [Tooltip("How the force is applied")]
    [SerializeField] private ForceMode impactForceMode = ForceMode.Impulse;
    [Tooltip("Radius for explosion force (0 = direct force only)")]
    [SerializeField, Min(0f)] private float explosionRadius = 0f;
    [Tooltip("Upward modifier for explosion force")]
    [SerializeField, Range(0f, 3f)] private float explosionUpwardModifier = 0.5f;

    // Public accessors
    public float FireRate => fireRate;
    public int ClipSize => clipSize;
    public float ReloadTime => reloadTime;
    public float MaxRange => maxRange;
    public bool UseAmmo => useAmmo;
    public ItemData AmmoType => ammoType;
    public float SpreadAngle => spreadAngle;
    public int PelletsPerShot => pelletsPerShot;
    public float ImpactForce => impactForce;
    public ForceMode ImpactForceMode => impactForceMode;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionUpwardModifier => explosionUpwardModifier;
    public bool HasExplosionForce => explosionRadius > 0f;

    /// <summary>
    /// Time between shots in seconds.
    /// </summary>
    public float FireInterval => 1f / fireRate;
}