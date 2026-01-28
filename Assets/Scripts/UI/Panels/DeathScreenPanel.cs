using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// Death screen UI panel. Shows a countdown timer during the respawn delay.
/// Subscribes to PlayerDeathController events — no polling, no duplicate state.
///
/// Must be a child of the UIManager's Canvas to be auto-discovered.
/// Requires a CanvasGroup (inherited from UIPanel).
/// </summary>
public class DeathScreenPanel : UIPanel
{
    [BoxGroup("UI References")]
    [Tooltip("Text element displaying the countdown timer.")]
    [SerializeField] private TMP_Text timerText;

    [BoxGroup("UI References")]
    [Tooltip("Optional header text (e.g., 'YOU DIED').")]
    [SerializeField] private TMP_Text headerText;

    [BoxGroup("Settings")]
    [Tooltip("Format string for the timer. {0} is replaced with remaining seconds.")]
    [SerializeField] private string timerFormat = "Respawning in {0:F1}s";

    [BoxGroup("Settings")]
    [Tooltip("Header text displayed on death.")]
    [SerializeField] private string deathHeaderText = "YOU DIED";

    private PlayerDeathController _deathController;

    protected override void Awake()
    {
        base.Awake();
        Hide();
    }

    private void Start()
    {
        FindDeathController();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Finds the PlayerDeathController and subscribes to its events.
    /// </summary>
    private void FindDeathController()
    {
        // Try to find via PlayerManager
        if (PlayerManager.Instance != null)
        {
            _deathController = PlayerManager.Instance.GetComponent<PlayerDeathController>();
        }

        // Fallback: search the scene
        if (_deathController == null)
        {
            _deathController = FindAnyObjectByType<PlayerDeathController>();
        }

        if (_deathController == null)
        {
            Debug.LogWarning("[DeathScreenPanel] No PlayerDeathController found. Death screen will not function.");
            return;
        }

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        if (_deathController == null) return;

        _deathController.OnPlayerDied += HandlePlayerDied;
        _deathController.OnRespawnTimerTick += HandleTimerTick;
        _deathController.OnPlayerRespawned += HandlePlayerRespawned;
    }

    private void UnsubscribeFromEvents()
    {
        if (_deathController == null) return;

        _deathController.OnPlayerDied -= HandlePlayerDied;
        _deathController.OnRespawnTimerTick -= HandleTimerTick;
        _deathController.OnPlayerRespawned -= HandlePlayerRespawned;
    }

    /// <summary>
    /// Called when the player dies. Shows the panel with initial timer value.
    /// </summary>
    private void HandlePlayerDied(float respawnDelay)
    {
        if (headerText != null)
        {
            headerText.text = deathHeaderText;
        }

        UpdateTimerDisplay(respawnDelay);
        Show();

        Debug.Log("[DeathScreenPanel] Showing death screen.");
    }

    /// <summary>
    /// Called every frame during the respawn countdown.
    /// Updates the timer display.
    /// </summary>
    private void HandleTimerTick(float remainingSeconds)
    {
        UpdateTimerDisplay(remainingSeconds);
    }

    /// <summary>
    /// Called when the player has respawned. Hides the panel.
    /// </summary>
    private void HandlePlayerRespawned()
    {
        Hide();
        Debug.Log("[DeathScreenPanel] Hiding death screen.");
    }

    /// <summary>
    /// Updates the timer text element with the remaining time.
    /// </summary>
    private void UpdateTimerDisplay(float remainingSeconds)
    {
        if (timerText == null) return;
        timerText.text = string.Format(timerFormat, remainingSeconds);
    }
}
