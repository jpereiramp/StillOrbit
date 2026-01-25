using UnityEngine;

public struct ItemActionContext
{
    public GameObject User;
    public IAimTarget AimTarget;
    public IInteractable Interactable;
    public IInventory Inventory;
}
