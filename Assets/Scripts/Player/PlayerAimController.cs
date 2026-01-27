using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Information about what the player is currently aiming at.
/// </summary>
public struct AimHitInfo
{
    public GameObject HitObject;
    public Collider HitCollider;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public float Distance;

    public bool HasHit => HitObject != null;
}

/// <summary>
/// Handles raycasting from the camera to determine what the player is looking at.
/// </summary>
public class PlayerAimController : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    private PlayerCameraController playerCameraController;

    [BoxGroup("Raycast Settings")]
    [Tooltip("Maximum raycast distance")]
    [SerializeField]
    private float maxAimDistance = 100f;

    [BoxGroup("Raycast Settings")]
    [Tooltip("Layers to hit when aiming. Leave as Everything to hit all layers.")]
    [SerializeField]
    private LayerMask aimLayerMask = ~0; // Default: Everything

    [BoxGroup("Raycast Settings")]
    [SerializeField] private float minAimAssistRadius = 0.05f;

    [BoxGroup("Raycast Settings")]
    [SerializeField] private float maxAimAssistRadius = 0.4f;

    [BoxGroup("Raycast Settings")]
    [SerializeField] private float maxAimAssistAngle = 6f; // degrees

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private AimHitInfo currentAimHitInfo;

    public AimHitInfo CurrentAimHitInfo => currentAimHitInfo;

    private void Awake()
    {
        if (playerCameraController == null)
        {
            playerCameraController = GetComponent<PlayerCameraController>();
        }

        SetCursorInteractionEnabled(false);
    }

    private void Update()
    {
        ProcessAiming();
    }

    private void ProcessAiming()
    {
        Transform cameraTransform = playerCameraController.CameraTransform;
        if (cameraTransform == null)
        {
            currentAimHitInfo = default;
            return;
        }

        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;
        Ray ray = new Ray(origin, direction);

        RaycastHit hit;
        bool hasHit = false;

        // 1️⃣ Try precise raycast first (skill-based)
        if (Physics.Raycast(ray, out hit, maxAimDistance, aimLayerMask))
        {
            hasHit = true;
        }
        else
        {
            // 2️⃣ Fallback: forgiving spherecast
            // Distance-scaled radius (small near, bigger far)
            float assistRadius = GetAimAssistRadius(maxAimDistance);

            if (Physics.SphereCast(ray, assistRadius, out hit, maxAimDistance, aimLayerMask))
            {
                // Optional: angle check to prevent off-screen / side snaps
                Vector3 toHit = (hit.point - origin).normalized;
                float angle = Vector3.Angle(direction, toHit);

                if (angle <= maxAimAssistAngle)
                {
                    hasHit = true;
                }
            }
        }

        if (!hasHit)
        {
            currentAimHitInfo = default;
            return;
        }

        currentAimHitInfo = new AimHitInfo
        {
            HitObject = hit.collider.gameObject,
            HitCollider = hit.collider,
            HitPoint = hit.point,
            HitNormal = hit.normal,
            Distance = hit.distance
        };
    }

    private float GetAimAssistRadius(float maxDistance)
    {
        // We don’t know hit distance yet, so assume mid-to-far range
        // You can improve this later by doing a short ray first
        return Mathf.Lerp(minAimAssistRadius, maxAimAssistRadius, 0.7f);
    }

    public void SetCursorInteractionEnabled(bool enabled)
    {
        if (enabled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || playerCameraController == null)
            return;

        Transform camTransform = playerCameraController.CameraTransform;
        if (camTransform == null)
            return;

        Vector3 origin = camTransform.position;
        Vector3 direction = camTransform.forward;

        // 1. Draw central aim ray (always)
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, direction * maxAimDistance);

        // 2. Draw spherecast volume (assist area)
        float assistRadius = Mathf.Lerp(minAimAssistRadius, maxAimAssistRadius, 0.7f);

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);

        Vector3 sphereStart = origin;
        Vector3 sphereEnd = origin + direction * maxAimDistance;

        Gizmos.DrawWireSphere(sphereStart, assistRadius);
        Gizmos.DrawWireSphere(sphereEnd, assistRadius);
        Gizmos.DrawLine(
            sphereStart + camTransform.right * assistRadius,
            sphereEnd + camTransform.right * assistRadius
        );
        Gizmos.DrawLine(
            sphereStart - camTransform.right * assistRadius,
            sphereEnd - camTransform.right * assistRadius
        );
        Gizmos.DrawLine(
            sphereStart + camTransform.up * assistRadius,
            sphereEnd + camTransform.up * assistRadius
        );
        Gizmos.DrawLine(
            sphereStart - camTransform.up * assistRadius,
            sphereEnd - camTransform.up * assistRadius
        );

        // 3. Draw hit feedback
        if (currentAimHitInfo.HasHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, currentAimHitInfo.HitPoint);

            Gizmos.DrawWireSphere(currentAimHitInfo.HitPoint, 0.08f);

            // Hit normal
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(
                currentAimHitInfo.HitPoint,
                currentAimHitInfo.HitNormal * 0.4f
            );
        }
    }
#endif
}