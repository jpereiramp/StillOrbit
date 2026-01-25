public static class InteractionContextExtensions
{
    public static ItemActionContext ToItemContext(
        this InteractionContext context)
    {
        return new ItemActionContext
        {
            User = context.Instigator,
            Inventory = context.Inventory,
            AimTarget = context.AimTarget,
            Interactable = context.AimTarget?.CurrentTarget
                ?.GetComponent<IInteractable>()
        };
    }
}