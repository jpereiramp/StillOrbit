public class InteractionResolver
{
    public void Resolve(InteractionContext context)
    {
        // 1️⃣ World interaction always has priority
        if (TryResolveWorldInteraction(context))
            return;

        // 2️⃣ Fallback to held item action
        TryResolveItemAction(context);
    }

    private bool TryResolveWorldInteraction(InteractionContext context)
    {
        if (context.AimTarget == null)
            return false;

        var interactable = context.AimTarget.CurrentTarget
            .GetComponent<IInteractable>();

        if (interactable == null)
            return false;

        if (!interactable.CanInteract(context))
            return false;

        interactable.Interact(context);
        return true;
    }

    private bool TryResolveItemAction(InteractionContext context)
    {
        if (context.HeldItem == null)
            return false;

        if (!context.HeldItem.PrimaryAction.CanExecute(context.ToItemContext()))
            return false;

        context.HeldItem.PrimaryAction.Execute(context.ToItemContext());
        return true;
    }
}
