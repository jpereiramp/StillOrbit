using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

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

        // Get HealthComponent from PlayerManager
        HealthComponent healthComponent = PlayerManager.Instance.HealthComponent;

        if (healthComponent != null)
        {
            healthComponent.Heal(healthToRestore);
            Debug.Log($"[Food] Restored {healthToRestore} health to {user.name}");
        }
        else
        {
            Debug.LogWarning($"[Food] No HealthComponent found for {user.name}");
        }

        onConsumed?.Invoke();

        // Return Consumed so the equipment controller knows to unequip/destroy this
        return UseResult.Consumed;
    }
}