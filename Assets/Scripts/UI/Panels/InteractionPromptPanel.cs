using TMPro;
using UnityEngine;

/// <summary>
/// Shows interaction prompts when the player is looking at an interactable object.
/// </summary>
public class InteractionPromptPanel : UIPanel
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI promptText;

    private PlayerInteractionController interactionController;
    private string lastPrompt;

    private void Start()
    {
        // Get interaction controller from player
        if (PlayerManager.Instance != null)
        {
            interactionController = PlayerManager.Instance.InteractionController;
        }

        if (interactionController == null)
        {
            Debug.LogWarning("[InteractionPromptPanel] No PlayerInteractionController found. Panel will not update.");
        }

        // Start hidden
        Hide();
    }

    private void Update()
    {
        if (interactionController == null) return;

        string currentPrompt = interactionController.GetCurrentInteractionPrompt();

        // Only update if prompt changed
        if (currentPrompt == lastPrompt) return;
        lastPrompt = currentPrompt;

        if (string.IsNullOrEmpty(currentPrompt))
        {
            Hide();
        }
        else
        {
            if (promptText != null)
            {
                promptText.text = currentPrompt;
            }
            Show();
        }
    }
}
