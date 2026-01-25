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

    [BoxGroup("Settings")]
    [Tooltip("Maximum raycast distance")]
    [SerializeField]
    private float maxAimDistance = 100f;

    [BoxGroup("Settings")]
    [Tooltip("Layers to hit when aiming. Leave as Everything to hit all layers.")]
    [SerializeField]
    private LayerMask aimLayerMask = ~0; // Default: Everything

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

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, maxAimDistance, aimLayerMask))
        {
            currentAimHitInfo = new AimHitInfo
            {
                HitObject = hitInfo.collider.gameObject,
                HitCollider = hitInfo.collider,
                HitPoint = hitInfo.point,
                HitNormal = hitInfo.normal,
                Distance = hitInfo.distance
            };
        }
        else
        {
            currentAimHitInfo = default;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || playerCameraController == null)
            return;

        var camTransform = playerCameraController.CameraTransform;
        if (camTransform == null)
            return;

        if (currentAimHitInfo.HasHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(camTransform.position, currentAimHitInfo.HitPoint);
            Gizmos.DrawWireSphere(currentAimHitInfo.HitPoint, 0.1f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(camTransform.position, camTransform.forward * maxAimDistance);
        }
    }
#endif
}