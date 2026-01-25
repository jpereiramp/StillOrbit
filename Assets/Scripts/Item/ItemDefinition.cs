using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    public Sprite Icon;

    [Header("Usage")]
    public ItemActionDefinition PrimaryAction;

    [Header("Stacking")]
    public bool IsStackable = false;
    public int MaxStack = 1;

    [Header("Durability")]
    public bool HasDurability = false;
    public int MaxDurability = 100;

    public ItemInstance CreateInstance(int amount = 1)
    {
        return new ItemInstance(this, amount);
    }
}
