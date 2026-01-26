using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

public class BuildPlacementValidator : MonoBehaviour
{
    [BoxGroup("Ground Check")]
    [SerializeField] private LayerMask validPlacementLayerMask;

    [BoxGroup("Ground Check")]
    [SerializeField] private float groundCheckDistance = 10f;

    [BoxGroup("Slope")]
    [SerializeField] private float maxSlopeAngle = 30f;

    [BoxGroup("Obstacles")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [BoxGroup("Obstacles")]
    [SerializeField] private float boundsMargin = 0.9f;

    /// <summary>
    /// Detailed result of the placement validation.
    /// </summary>
    public struct ValidationResult
    {
        public bool IsValid;
        public string FailureReason;

        public static ValidationResult Valid() => new ValidationResult { IsValid = true, FailureReason = string.Empty };
        public static ValidationResult Invalid(string reason) => new ValidationResult { IsValid = false, FailureReason = reason };
    }

    public ValidationResult Validate(Vector3 placementPosition, Quaternion rotation, BuildingData building)
    {
        if (building == null || building.BuildingPrefab == null)
        {
            return ValidationResult.Invalid("Building data or prefab is null.");
        }

        // Ground Check
        if (!IsOnGround(placementPosition))
        {
            return ValidationResult.Invalid("Placement position is not on valid ground.");
        }

        // Slope Check
        if (!IsSlopeAcceptable(placementPosition, out float slopeAngle))
        {
            return ValidationResult.Invalid($"Slope angle {slopeAngle}° exceeds maximum allowed angle of {maxSlopeAngle}°.");
        }

        // Obstacle Check
        Bounds bounds = CalculatePrefabBounds(building.BuildingPrefab);
        if (HasObstacleCollision(placementPosition, rotation, bounds))
        {
            return ValidationResult.Invalid("Placement position collides with existing obstacles.");
        }
        return ValidationResult.Valid();
    }

    private bool IsOnGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 5f;
        return Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, validPlacementLayerMask);
    }

    private bool IsSlopeAcceptable(Vector3 position, out float angle)
    {
        angle = 0f;
        Vector3 rayStart = position + Vector3.up * 2f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, validPlacementLayerMask))
        {
            angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle <= maxSlopeAngle;
        }

        return false;
    }

    private bool HasObstacleCollision(Vector3 position, Quaternion rotation, Bounds bounds)
    {
        Vector3 center = position + rotation * bounds.center;
        Vector3 halfExtents = bounds.extents * boundsMargin;

        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, obstacleLayerMask);

        return overlaps.Length > 0;
    }

    private Bounds CalculatePrefabBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // Fallback: 1x1x1 bounds
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        // Make bounds relative to prefab origin
        bounds.center -= prefab.transform.position;

        return bounds;
    }
}