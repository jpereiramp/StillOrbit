using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// Behaviour for consumable food items when held.
/// Attach to the held prefab, not the world prefab.
/// </summary>
public class Food : MonoBehaviour, IUsable
{
    [BoxGroup("Data")]
    [Tooltip("Optional: link to ConsumableData for stat values. If null, uses local values.")]
    [SerializeField]
    private ConsumableData consumableData;

    [BoxGroup("Local Values")]
    [Tooltip("Used if ConsumableData is not assigned")]
    [SerializeField]
    private int healthRestoration = 20;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onConsumed;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool hasBeenConsumed;

    public bool CanUse => !hasBeenConsumed;

    public UseResult Use(GameObject user)
    {
        if (hasBeenConsumed)
            return UseResult.Failed;

        hasBeenConsumed = true;

        // Get restoration value from data or local
        int healthToRestore = consumableData != null ? consumableData.HealthRestore : healthRestoration;

        // TODO: Apply to actual health system when implemented
        Debug.Log($"Consuming food: restoring {healthToRestore} health to {user.name}");

        onConsumed?.Invoke();

        // Return Consumed so the equipment controller knows to unequip/destroy this
        return UseResult.Consumed;
    }
}