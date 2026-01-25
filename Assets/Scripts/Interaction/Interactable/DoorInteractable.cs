using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    private bool isOpen = false;

    public bool CanInteract(InteractionContext context) => true;

    public void Interact(InteractionContext context)
    {
        isOpen = !isOpen;
        Debug.Log(isOpen ? "Door opened." : "Door closed.");

        // Play animation, SFX, VFX...
    }
}