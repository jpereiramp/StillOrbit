using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Trigger collider component for melee weapon hit detection.
/// Attach to a child GameObject of the weapon with a trigger Collider.
/// Reports hits to PlayerCombatManager when active.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    [BoxGroup("Settings")]
    [Tooltip("Layers that can be hit by this weapon")]
    [SerializeField]
    private LayerMask hitLayers = ~0; // Default: Everything

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool isActive;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private Collider hitboxCollider;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        // Ensure collider is set as trigger
        if (hitboxCollider != null && !hitboxCollider.isTrigger)
        {
            Debug.LogWarning($"[WeaponHitbox] Collider on {gameObject.name} is not a trigger. Setting isTrigger = true.");
            hitboxCollider.isTrigger = true;
        }

        // Start disabled
        SetActive(false);
    }

    /// <summary>
    /// Enables or disables hit detection.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = active;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive)
            return;

        // Check layer mask
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Don't hit ourselves (player)
        if (other.transform.root == transform.root)
            return;

        // Report hit to combat manager
        if (PlayerCombatManager.Instance != null)
        {
            PlayerCombatManager.Instance.RegisterHit(other.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isActive ? new Color(1f, 0f, 0f, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.1f);

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
        }
        else if (col is CapsuleCollider capsule)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(capsule.center, capsule.radius);
        }
    }
#endif
}
