using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Optional component for held item prefabs to customize how they're positioned in hand.
/// If not present, default values from PlayerEquipmentController are used.
/// </summary>
public class HeldItemBehaviour : MonoBehaviour
{
    [BoxGroup("Hold Position")]
    [SerializeField]
    private Vector3 holdOffset = Vector3.zero;

    [BoxGroup("Hold Position")]
    [SerializeField]
    private Vector3 holdRotation = Vector3.zero;

    public Vector3 HoldOffset => holdOffset;
    public Vector3 HoldRotation => holdRotation;

    #if UNITY_EDITOR
    [Button("Reset to Origin"), BoxGroup("Hold Position")]
    private void ResetPosition()
    {
        holdOffset = Vector3.zero;
        holdRotation = Vector3.zero;
    }
    #endif
}
