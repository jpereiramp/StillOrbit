using UnityEngine;

public class ConsumeAction : IItemAction
{
    private readonly ItemInstance item;

    public ConsumeAction(ItemInstance item)
    {
        this.item = item;
    }

    public bool CanExecute(ItemActionContext context)
    {
        if (item == null)
            return false;

        if (item.StackCount <= 0)
            return false;

        // Optional: block consuming while aiming at an interactable
        if (context.Interactable != null)
            return false;

        return true;
    }

    public void Execute(ItemActionContext context)
    {
        // --- Apply effects ---
        ApplyEffects(context.User);

        // --- Consume one unit ---
        item.RemoveFromStack(1);

        // --- Remove item if depleted ---
        if (item.StackCount <= 0)
        {
            context.Inventory.RemoveItem(item);
        }
    }

    private void ApplyEffects(GameObject user)
    {
        // Example:
        // user.GetComponent<Health>()?.Heal(25);
        // user.GetComponent<Hunger>()?.Restore(15);
    }
}
