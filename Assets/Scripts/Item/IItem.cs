public interface IItem
{
    string Name { get; }
    IItemAction PrimaryAction { get; }
}
