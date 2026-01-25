using UnityEngine;

public class ItemInstance : IItem
{
    public ItemDefinition Definition { get; }
    public int StackCount { get; private set; }
    public int Durability { get; private set; }

    private readonly IItemAction primaryAction;

    public ItemInstance(ItemDefinition definition, int amount)
    {
        Definition = definition;
        StackCount = Mathf.Clamp(amount, 1, definition.MaxStack);

        if (definition.HasDurability)
            Durability = definition.MaxDurability;

        if (definition.PrimaryAction != null)
            primaryAction = definition.PrimaryAction.CreateAction(this);
    }

    public string Name => Definition.DisplayName;
    public IItemAction PrimaryAction => primaryAction;

    // -------------------------
    // Mutations
    // -------------------------

    public void DamageDurability(int amount)
    {
        if (!Definition.HasDurability)
            return;

        Durability = Mathf.Max(0, Durability - amount);
    }

    public void AddToStack(int amount)
    {
        if (!Definition.IsStackable)
            return;

        StackCount = Mathf.Min(
            StackCount + amount,
            Definition.MaxStack);
    }

    public void RemoveFromStack(int amount)
    {
        StackCount = Mathf.Max(StackCount - amount, 0);
    }

    public bool IsBroken =>
        Definition.HasDurability && Durability <= 0;
}
