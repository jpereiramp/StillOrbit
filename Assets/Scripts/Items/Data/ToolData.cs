using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Item data for tools (axes, pickaxes, etc.).
/// Tools are primarily effective for harvesting resources.
/// </summary>
[CreateAssetMenu(fileName = "New Tool", menuName = "StillOrbit/Items/Tool Data")]
public class ToolData : ItemData
{
    [BoxGroup("Tool Stats")]
    [Min(0)]
    [SerializeField] private float rate = 1f;

    [BoxGroup("Tool Stats")]
    [Min(0)]
    [SerializeField] private float range = 2f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against trees and wooden structures")]
    [Min(0)]
    [SerializeField] private float woodDamage = 10f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against rocks, stone, and ore")]
    [Min(0)]
    [SerializeField] private float rockDamage = 10f;

    [BoxGroup("Damage Per Type")]
    [Tooltip("Damage against creatures and players (tools are weak against flesh)")]
    [Min(0)]
    [SerializeField] private float fleshDamage = 5f;

    public float Rate => rate;
    public float Range => range;

    /// <summary>
    /// Gets the damage value for a specific target type.
    /// </summary>
    public float GetDamage(DamageType targetType)
    {
        return targetType switch
        {
            DamageType.Wood => woodDamage,
            DamageType.Rock => rockDamage,
            DamageType.Flesh => fleshDamage,
            _ => fleshDamage
        };
    }
}
