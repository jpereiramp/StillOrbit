using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Configuration data for companion behavior.
/// All tunable parameters live here, not hardcoded in scripts.
/// </summary>
[CreateAssetMenu(fileName = "CompanionData", menuName = "StillOrbit/Companion/Companion Data")]
public class CompanionData : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string companionId = "default_companion";

    [BoxGroup("Identity")]
    [SerializeField] private string displayName = "Resource Mule";

    [BoxGroup("Movement")]
    [Tooltip("Movement speed when following or navigating")]
    [SerializeField] private float moveSpeed = 5f;

    [BoxGroup("Movement")]
    [Tooltip("How close the companion must get to consider arrival")]
    [SerializeField] private float arrivalDistance = 1.5f;

    [BoxGroup("Movement")]
    [Tooltip("Distance to maintain when following player")]
    [SerializeField] private float followDistance = 3f;

    [BoxGroup("Calling")]
    [Tooltip("Minimum spawn distance from player when called")]
    [SerializeField] private float minSpawnDistance = 5f;

    [BoxGroup("Calling")]
    [Tooltip("Maximum spawn distance from player when called")]
    [SerializeField] private float maxSpawnDistance = 10f;

    [BoxGroup("Calling")]
    [Tooltip("Prefer spawning behind player (outside field of view)")]
    [SerializeField] private bool preferSpawnBehindPlayer = true;

    [BoxGroup("Calling")]
    [Tooltip("Angle range behind player to try spawning (degrees)")]
    [SerializeField] private float behindPlayerAngleRange = 120f;

    [BoxGroup("Auto-Deposit")]
    [Tooltip("Seconds of no interaction before auto-deposit triggers")]
    [SerializeField] private float idleTimeBeforeAutoDeposit = 5f;

    [BoxGroup("Auto-Deposit")]
    [Tooltip("Maximum distance to search for deposit targets")]
    [SerializeField] private float depotSearchRadius = 100f;

    [BoxGroup("Inventory")]
    [Tooltip("Resource types the companion can carry (empty = all)")]
    [SerializeField] private List<ResourceType> acceptedResourceTypes = new List<ResourceType>();

    [BoxGroup("Inventory")]
    [Tooltip("If true, accepts all resource types regardless of list")]
    [SerializeField] private bool acceptAllResources = true;

    [BoxGroup("Interaction")]
    [Tooltip("Prompt shown when player can interact")]
    [SerializeField] private string interactionPrompt = "Deposit Resources";

    [BoxGroup("Interaction")]
    [Tooltip("Distance within which player can interact")]
    [SerializeField] private float interactionRange = 2f;

    // Public Accessors
    public string CompanionId => companionId;
    public string DisplayName => displayName;
    public float MoveSpeed => moveSpeed;
    public float ArrivalDistance => arrivalDistance;
    public float FollowDistance => followDistance;
    public float MinSpawnDistance => minSpawnDistance;
    public float MaxSpawnDistance => maxSpawnDistance;
    public bool PreferSpawnBehindPlayer => preferSpawnBehindPlayer;
    public float BehindPlayerAngleRange => behindPlayerAngleRange;
    public float IdleTimeBeforeAutoDeposit => idleTimeBeforeAutoDeposit;
    public float DepotSearchRadius => depotSearchRadius;
    public IReadOnlyList<ResourceType> AcceptedResourceTypes => acceptedResourceTypes;
    public bool AcceptAllResources => acceptAllResources;
    public string InteractionPrompt => interactionPrompt;
    public float InteractionRange => interactionRange;

    /// <summary>
    /// Check if the companion can carry a specific resource type.
    /// </summary>
    public bool CanAcceptResource(ResourceType resourceType)
    {
        if (acceptAllResources) return true;
        if (resourceType == ResourceType.None) return false;

        return acceptedResourceTypes.Contains(resourceType);
    }

#if UNITY_EDITOR
    [Button("Validate Data"), BoxGroup("Debug")]
    private void ValidateData()
    {
        bool valid = true;

        if (string.IsNullOrEmpty(companionId))
        {
            Debug.LogWarning("[CompanionData] Companion ID is empty");
            valid = false;
        }

        if (minSpawnDistance >= maxSpawnDistance)
        {
            Debug.LogWarning("[CompanionData] Min spawn distance should be less than max");
            valid = false;
        }

        if (idleTimeBeforeAutoDeposit <= 0)
        {
            Debug.LogWarning("[CompanionData] Idle time should be positive");
            valid = false;
        }

        if (valid)
        {
            Debug.Log("[CompanionData] Validation passed!");
        }
    }
#endif
}