using UnityEngine;

public class WorldItemPickup : MonoBehaviour, IPickable
{
    [SerializeField] private ItemDefinition item;

    public bool CanInteract(InteractionContext context) => true;

    public void Interact(InteractionContext context)
    {
        context.Inventory.AddItem(item.CreateInstance());
        Destroy(gameObject);
    }

    public IItem PickUp() => item.CreateInstance();
}
