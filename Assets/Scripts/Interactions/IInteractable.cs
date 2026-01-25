using UnityEngine;

/// <summary>
/// Interface for any world object that can be interacted with.
/// This is the most generic interaction type (doors, levers, buttons, NPCs, etc.)
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Display name shown in interaction prompts (e.g., "Open Door", "Talk to Bob")
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Whether this object can currently be interacted with.
    /// </summary>
    bool CanInteract(GameObject interactor);

    /// <summary>
    /// Perform the interaction. Called when player presses interact while looking at this.
    /// </summary>
    void Interact(GameObject interactor);
}
