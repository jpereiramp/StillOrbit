using Sirenix.OdinInspector;
using UnityEngine;

public class BuildingGhostController : MonoBehaviour
{
    [BoxGroup("Validation")]
    [SerializeField] private BuildPlacementValidator placementValidator;

    [BoxGroup("Settings")]
    [SerializeField] private Material validMaterial;

    [BoxGroup("Settings")]
    [SerializeField] private Material invalidMaterial;

    [BoxGroup("Settings")]
    [SerializeField] private float rotationStep = 15f;

    [BoxGroup("Settings")]
    [SerializeField] private LayerMask groundLayerMask;

    [BoxGroup("Settings")]
    [SerializeField] private float maxRaycastDistance = 200f;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private GameObject currentGhost;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private BuildingData currentBuildingData;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private float currentRotationY;

    // Public Accessors
    public bool HasGhost => currentGhost != null;
    public Vector3 GhostPosition => currentGhost != null ? currentGhost.transform.position : Vector3.zero;
    public Quaternion GhostRotation => Quaternion.Euler(0f, currentRotationY, 0f);
    public BuildingData CurrentBuildingData => currentBuildingData;

    private void Update()
    {
        if (currentGhost == null) return;

        UpdateGhostPosition();
        HandleRotationInput();
        HandlePlacementValidation();
    }

    public void ShowGhost(BuildingData buildingData)
    {
        if (buildingData == null || buildingData.BuildingPrefab == null)
        {
            Debug.LogWarning("Invalid building data provided for ghost.");
            return;
        }

        ClearGhost();

        currentBuildingData = buildingData;
        currentRotationY = 0f;

        currentGhost = Instantiate(buildingData.BuildingPrefab);
        currentGhost.name = buildingData.BuildingPrefab.name + "_Ghost";

        DisableGhostComponents(currentGhost);
        ApplyGhostMaterial(currentGhost);
        SetLayerRecursively(currentGhost, LayerMask.NameToLayer("Ignore Raycast"));

        Debug.Log("Showing ghost for building: " + buildingData.BuildingPrefab.name);
    }

    public void ClearGhost()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }

        currentBuildingData = null;
        currentRotationY = 0f;
    }

    public void RotateGhost()
    {
        currentRotationY = (currentRotationY + rotationStep) % 360f;

        if (currentGhost != null)
        {
            currentGhost.transform.rotation = GhostRotation;
        }

        Debug.Log("Rotated ghost to " + currentRotationY + " degrees.");
    }

    private void UpdateGhostPosition()
    {
        currentGhost.transform.position = PlayerManager.Instance.AimController.CurrentAimHitInfo.HitPoint;
        currentGhost.transform.rotation = GhostRotation;
    }

    private void HandleRotationInput()
    {
        if (PlayerManager.Instance.InputHandler.RotateBuildingPressed)
        {
            RotateGhost();
            PlayerManager.Instance.InputHandler.RotateBuildingPressed = false;
        }
    }

    private void HandlePlacementValidation()
    {
        // Validate placement
        var validationResult = placementValidator.Validate(currentGhost.transform.position, GhostRotation, currentBuildingData);
        ApplyValidationMaterial(validationResult.IsValid);
    }

    public bool ValidatePlacementConfirmation()
    {
        if (currentGhost == null || currentBuildingData == null)
        {
            Debug.LogWarning("No ghost or building data to validate.");
            return false;
        }

        var validationResult = placementValidator.Validate(currentGhost.transform.position, GhostRotation, currentBuildingData);
        if (validationResult.IsValid)
        {
            Debug.Log("Placement is valid.");
        }
        else
        {
            Debug.LogWarning("Placement is invalid: " + validationResult.FailureReason);
        }
        return validationResult.IsValid;
    }

    private void DisableGhostComponents(GameObject ghost)
    {
        // Disable Building component
        foreach (var building in ghost.GetComponentsInChildren<Building>(true))
        {
            building.enabled = false;
        }

        // Disable colliders (keep trigger colliders for visualization if any)
        foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.isTrigger)
            {
                collider.enabled = false;
            }
        }

        // Disable NavMeshObstacle
        foreach (var obstacle in ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
        {
            obstacle.enabled = false;
        }
    }

    private void ApplyGhostMaterial(GameObject ghost)
    {
        if (validMaterial == null) return;

        foreach (var renderer in ghost.GetComponentsInChildren<Renderer>())
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = validMaterial;
            }
            renderer.materials = newMaterials;
        }
    }

    private void ApplyValidationMaterial(bool isValid)
    {
        Material targetMaterial = isValid ? validMaterial : invalidMaterial;
        if (targetMaterial == null) return;

        foreach (var renderer in currentGhost.GetComponentsInChildren<Renderer>())
        {
            Material[] newMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = targetMaterial;
            }
            renderer.materials = newMaterials;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}