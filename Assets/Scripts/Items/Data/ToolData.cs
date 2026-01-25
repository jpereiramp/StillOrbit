using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Item data for tools.
/// </summary>
[CreateAssetMenu(fileName = "New Tool", menuName = "StillOrbit/Items/Tool Data")]
public class ToolData : ItemData
{
    [BoxGroup("Tool Stats")]

    [Min(0)]
    [SerializeField] private float damageToTrees = 10f;

    [Min(0)]
    [SerializeField] private float damageToRocks = 10f;

    [BoxGroup("Tool Stats")]
    [Min(0)]
    [SerializeField] private float rate = 1f;

    [BoxGroup("Tool Stats")]
    [Min(0)]
    [SerializeField] private float range = 2f;

    public float DamageToTrees => damageToTrees;
    public float DamageToRocks => damageToRocks;
    public float Rate => rate;
    public float Range => range;
}
