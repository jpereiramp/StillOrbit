using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player health with a slider and text label.
/// </summary>
public class HealthPanel : UIPanel
{
    [Header("References")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("(Optional) Text displaying current and max health in format 'current / max'")]
    [SerializeField] private TextMeshProUGUI healthText;

    private HealthComponent healthComponent;

    private void Start()
    {
        // Get health component from player
        if (PlayerManager.Instance != null)
        {
            healthComponent = PlayerManager.Instance.HealthComponent;
        }

        if (healthComponent == null)
        {
            Debug.LogWarning("[HealthPanel] No HealthComponent found on player. Panel will not update.");
            return;
        }

        // Subscribe to health changes
        healthComponent.OnHealthChanged += UpdateHealthDisplay;

        // Initialize display
        UpdateHealthDisplay(healthComponent.CurrentHealth, healthComponent.MaxHealth);
    }

    private void OnDestroy()
    {
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged -= UpdateHealthDisplay;
        }
    }

    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        // Update slider (0 to 1 normalized)
        if (healthSlider != null)
        {
            healthSlider.value = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        }

        // Update text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}
