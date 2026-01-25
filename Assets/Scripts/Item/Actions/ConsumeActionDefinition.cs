using UnityEngine;

[CreateAssetMenu(menuName = "Items/Actions/Consume")]
public class ConsumeActionDefinition : ItemActionDefinition
{
    public override IItemAction CreateAction(ItemInstance instance)
    {
        return new ConsumeAction(instance);
    }
}
