using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Item data for consumable items (food, potions, etc.)
/// </summary>
[CreateAssetMenu(fileName = "New Consumable", menuName = "StillOrbit/Items/Consumable Data")]
public class ConsumableData : ItemData
{
    [BoxGroup("Consumable Effects")]
    [SerializeField] private int healthRestore;

    [BoxGroup("Consumable Effects")]
    [SerializeField] private int hungerRestore;

    [BoxGroup("Consumable Effects")]
    [SerializeField] private int thirstRestore;

    [BoxGroup("Consumable Effects")]
    [Tooltip("If true, item is destroyed after use")]
    [SerializeField] private bool consumeOnUse = true;

    public int HealthRestore => healthRestore;
    public int HungerRestore => hungerRestore;
    public int ThirstRestore => thirstRestore;
    public bool ConsumeOnUse => consumeOnUse;
}
