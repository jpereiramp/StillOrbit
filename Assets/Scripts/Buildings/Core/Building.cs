using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Base class for all buildings.
/// Handles lifecycle, registration, and common functionality.
/// </summary>
public class Building : MonoBehaviour, IBuilding
{
    [BoxGroup("Configuration")]
    [Required]
    [SerializeField]
    protected BuildingData buildingData;

    [BoxGroup("State")]
    [ShowInInspector, ReadOnly]
    private bool isOperational = true;

    // Cached components
    private HealthComponent healthComponent;

    // IBuilding implementation
    public BuildingData Data => buildingData;
    public Transform Transform => transform;
    public HealthComponent Health => healthComponent;
    public bool IsOperational => isOperational;

    public event Action OnBuildingDestroyed;

    protected virtual void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();

        // Configure health from building data if health component exists
        if (buildingData != null && healthComponent != null)
        {
            healthComponent.SetMaxHealth(buildingData.MaxHealth);

            if (buildingData.IsIndestructible)
            {
                healthComponent.SetInvulnerable(true);
            }
        }
    }

    protected virtual void Start()
    {
        // Register with the building registry
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.Register(this);
        }
        else
        {
            Debug.LogWarning($"[Building] No BuildingRegistry found! {gameObject.name} will not be discoverable.");
        }
    }

    protected virtual void OnEnable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath += HandleDestruction;
        }
    }

    protected virtual void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleDestruction;
        }
    }

    protected virtual void OnDestroy()
    {
        // Unregister from the building registry
        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.Unregister(this);
        }

        OnBuildingDestroyed?.Invoke();
    }

    /// <summary>
    /// Called when the building's health reaches zero.
    /// </summary>
    protected virtual void HandleDestruction()
    {
        isOperational = false;

        Debug.Log($"[Building] {buildingData?.BuildingName ?? gameObject.name} destroyed!");

        // Subclasses can override to add destruction effects, drops, etc.
        OnDestroyBuilding();
    }

    /// <summary>
    /// Override in subclasses to add destruction behavior.
    /// </summary>
    protected virtual void OnDestroyBuilding()
    {
        // Default: just destroy the GameObject
        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// Set the building's operational state.
    /// </summary>
    public void SetOperational(bool operational)
    {
        if (isOperational == operational) return;

        isOperational = operational;
        OnOperationalStateChanged(operational);
    }

    /// <summary>
    /// Override to respond to operational state changes.
    /// </summary>
    protected virtual void OnOperationalStateChanged(bool operational)
    {
        // Subclasses can override (e.g., disable production, visual changes)
    }

#if UNITY_EDITOR
    [Button("Damage (10)"), BoxGroup("Debug")]
    private void DebugDamage()
    {
        if (Application.isPlaying && healthComponent != null)
        {
            healthComponent.TakeDamage(10f, DamageType.Generic, null);
        }
    }

    [Button("Destroy Building"), BoxGroup("Debug")]
    private void DebugDestroy()
    {
        if (Application.isPlaying)
        {
            HandleDestruction();
        }
    }
#endif
}
