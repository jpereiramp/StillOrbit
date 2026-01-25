using UnityEngine;

public interface IInteractable
{
    bool CanInteract(InteractionContext context);
    void Interact(InteractionContext context);
}