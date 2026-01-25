using UnityEngine;

public class TestNPC : MonoBehaviour, ITalkable
{
    public string DialogueId => "villager_intro";

    public bool CanInteract(InteractionContext context) => true;

    public void Interact(InteractionContext context)
    {
        Debug.Log("Starting dialogue: " + DialogueId);
        // Trigger dialogue system with DialogueId
    }
}
