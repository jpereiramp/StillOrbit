public interface IItemAction
{
    bool CanExecute(ItemActionContext context);
    void Execute(ItemActionContext context);
}
