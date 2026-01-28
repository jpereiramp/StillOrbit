using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays current ammo count for equipped ranged weapon.
/// Automatically shows/hides based on whether a ranged weapon is equipped.
/// </summary>
public class AmmoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private GameObject displayRoot;
    [SerializeField] private Image reloadProgressBar;

    [Header("Settings")]
    [SerializeField] private string ammoFormat = "{0} / {1}";
    [SerializeField] private string reloadingText = "RELOADING...";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowAmmoColor = Color.yellow;
    [SerializeField] private Color emptyColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float lowAmmoThreshold = 0.25f;

    private RangedWeapon currentWeapon;
    private bool isReloading;
    private float reloadStartTime;
    private float reloadDuration;

    private void Update()
    {
        UpdateWeaponReference();
        UpdateReloadProgress();
    }

    private void UpdateWeaponReference()
    {
        RangedWeapon newWeapon = null;

        if (equipmentController != null && equipmentController.EquippedObject != null)
        {
            newWeapon = equipmentController.EquippedObject.GetComponent<RangedWeapon>();
        }

        if (newWeapon != currentWeapon)
        {
            // Unsubscribe from old weapon
            if (currentWeapon != null)
            {
                currentWeapon.OnAmmoChanged -= OnAmmoChanged;
                currentWeapon.OnReloadStarted -= OnReloadStarted;
                currentWeapon.OnReloadCompleted -= OnReloadCompleted;
            }

            currentWeapon = newWeapon;

            // Subscribe to new weapon
            if (currentWeapon != null)
            {
                currentWeapon.OnAmmoChanged += OnAmmoChanged;
                currentWeapon.OnReloadStarted += OnReloadStarted;
                currentWeapon.OnReloadCompleted += OnReloadCompleted;

                if (displayRoot != null)
                    displayRoot.SetActive(true);

                // Initial display
                OnAmmoChanged(currentWeapon.CurrentAmmo, currentWeapon.MaxAmmo);
                isReloading = currentWeapon.IsReloading;
            }
            else
            {
                if (displayRoot != null)
                    displayRoot.SetActive(false);

                isReloading = false;
            }
        }
    }

    private void OnAmmoChanged(int current, int max)
    {
        if (ammoText == null) return;

        if (isReloading)
        {
            ammoText.text = reloadingText;
            ammoText.color = normalColor;
            return;
        }

        ammoText.text = string.Format(ammoFormat, current, max);

        // Color based on ammo level
        if (current <= 0)
        {
            ammoText.color = emptyColor;
        }
        else if (max > 0 && (float)current / max <= lowAmmoThreshold)
        {
            ammoText.color = lowAmmoColor;
        }
        else
        {
            ammoText.color = normalColor;
        }
    }

    private void OnReloadStarted()
    {
        isReloading = true;
        reloadStartTime = Time.time;

        if (currentWeapon != null && currentWeapon.WeaponData is RangedWeaponData rangedData)
        {
            reloadDuration = rangedData.ReloadTime;
        }
        else
        {
            reloadDuration = 1f; // Fallback
        }

        if (ammoText != null)
        {
            ammoText.text = reloadingText;
            ammoText.color = normalColor;
        }

        if (reloadProgressBar != null)
        {
            reloadProgressBar.gameObject.SetActive(true);
            reloadProgressBar.fillAmount = 0f;
        }
    }

    private void OnReloadCompleted()
    {
        isReloading = false;

        if (reloadProgressBar != null)
        {
            reloadProgressBar.gameObject.SetActive(false);
        }

        // Update ammo display
        if (currentWeapon != null)
        {
            OnAmmoChanged(currentWeapon.CurrentAmmo, currentWeapon.MaxAmmo);
        }
    }

    private void UpdateReloadProgress()
    {
        if (!isReloading || reloadProgressBar == null) return;

        float elapsed = Time.time - reloadStartTime;
        float progress = Mathf.Clamp01(elapsed / reloadDuration);
        reloadProgressBar.fillAmount = progress;
    }

    private void OnDestroy()
    {
        // Clean up subscriptions
        if (currentWeapon != null)
        {
            currentWeapon.OnAmmoChanged -= OnAmmoChanged;
            currentWeapon.OnReloadStarted -= OnReloadStarted;
            currentWeapon.OnReloadCompleted -= OnReloadCompleted;
        }
    }
}
