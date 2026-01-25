using UnityEngine;

public abstract class ItemActionDefinition : ScriptableObject
{
    public abstract IItemAction CreateAction(ItemInstance instance);
}
