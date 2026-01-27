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

    // Public accessors
    public float FireRate => fireRate;
    public int ClipSize => clipSize;
    public float ReloadTime => reloadTime;
    public float MaxRange => maxRange;
    public bool UseAmmo => useAmmo;
    public ItemData AmmoType => ammoType;
    public float SpreadAngle => spreadAngle;
    public int PelletsPerShot => pelletsPerShot;

    /// <summary>
    /// Time between shots in seconds.
    /// </summary>
    public float FireInterval => 1f / fireRate;
}