using UnityEngine;

public class InteractionDetector
{
    public bool TryGetInteractable(
        IAimTarget aimTarget,
        InteractionContext context,
        out IInteractable interactable)
    {
        interactable = null;

        if (aimTarget == null)
            return false;

        interactable = aimTarget.CurrentTarget
            .GetComponent<IInteractable>();

        if (interactable == null)
            return false;

        return interactable.CanInteract(context);
    }
}
