using UnityEngine;

public struct InteractionContext
{
    public GameObject Instigator; // Player or entity initiating the interaction
    public IAimTarget AimTarget;
    public IItem HeldItem;
    public IInventory Inventory;
}