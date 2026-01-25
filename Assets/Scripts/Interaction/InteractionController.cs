using UnityEngine;

public class InteractionController : MonoBehaviour
{
    [SerializeField] private AimController aimController;
    [SerializeField] private PlayerInventory inventory;

    private readonly InteractionDetector detector = new();
    private readonly InteractionResolver resolver = new();

    // Call this from your existing input system
    public void OnUsePressed()
    {
        aimController.TryGetTarget(out var aimTarget);

        var context = new InteractionContext
        {
            Instigator = gameObject,
            AimTarget = aimTarget,
            HeldItem = inventory.CurrentItem,
            Inventory = inventory
        };

        // Detection (optional but explicit)
        if (detector.TryGetInteractable(
            aimTarget,
            context,
            out var interactable))
        {
            // Resolver will still decide final behavior
        }

        // Final resolution (world interaction OR item action)
        resolver.Resolve(context);
    }
}
