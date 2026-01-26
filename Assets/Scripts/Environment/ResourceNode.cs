using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Harvestable resource node (trees, rocks, ore deposits, etc.).
/// Requires a HealthComponent to track damage. When destroyed, awards items to the player.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class ResourceNode : MonoBehaviour
{
    [BoxGroup("Resource Settings")]
    [Tooltip("Items awarded when this resource is destroyed")]
    [SerializeField]
    private List<ResourceDrop> drops = new List<ResourceDrop>();

    [BoxGroup("Death Effects")]
    [Tooltip("Sound played when resource is destroyed")]
    [SerializeField]
    private AudioClip deathSound;

    [BoxGroup("Death Effects")]
    [Range(0f, 1f)]
    [SerializeField]
    private float deathSoundVolume = 1f;

    [BoxGroup("Death Effects")]
    [Tooltip("VFX spawned when resource is destroyed")]
    [SerializeField]
    private GameObject deathVFXPrefab;

    [BoxGroup("Death Effects")]
    [Tooltip("How long the death VFX lives")]
    [SerializeField]
    private float deathVFXLifetime = 3f;

    [BoxGroup("Death Effects")]
    [Tooltip("Delay before the resource is destroyed (allows effects to play)")]
    [SerializeField]
    private float destroyDelay = 0.5f;

    private HealthComponent healthComponent;
    private bool isDead;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[ResourceNode] {gameObject.name} destroyed!");

        // Play death effects
        PlayDeathEffects();

        // Award items to player
        AwardDrops();

        // Destroy after delay
        Destroy(gameObject, destroyDelay);
    }

    private void PlayDeathEffects()
    {
        // Play death sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathSoundVolume);
        }

        // Spawn death VFX
        if (deathVFXPrefab != null)
        {
            var vfx = Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
            if (deathVFXLifetime > 0)
            {
                Destroy(vfx, deathVFXLifetime);
            }
        }
    }

    private void AwardDrops()
    {
        if (drops == null || drops.Count == 0)
        {
            Debug.Log("[ResourceNode] No drops configured");
            return;
        }

        // Get player inventory
        PlayerInventory inventory = null;
        if (PlayerManager.Instance != null)
        {
            inventory = PlayerManager.Instance.Inventory;
        }

        foreach (var drop in drops)
        {
            if (drop.item == null) continue;

            int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
            if (amount <= 0) continue;

            // TODO: Actually add items to inventory when inventory system supports quantities
            // For now, just log what would be awarded
            Debug.Log($"[ResourceNode] Awarding {amount}x {drop.item.ItemName} to player");

            if (inventory != null)
            {
                for (int i = 0; i < amount; i++)
                {
                    bool added = inventory.TryAddItem(drop.item);
                    if (!added)
                    {
                        Debug.LogWarning($"[ResourceNode] Inventory full, could not add {drop.item.ItemName}");
                        break;
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    [Button("Test Death"), BoxGroup("Debug")]
    private void DebugTestDeath()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[ResourceNode] Test death only works in Play mode");
            return;
        }
        HandleDeath();
    }
#endif
}

/// <summary>
/// Defines an item drop with quantity range.
/// </summary>
[System.Serializable]
public class ResourceDrop
{
    [Tooltip("The item to drop")]
    public ItemData item;

    [Tooltip("Minimum amount to drop")]
    [Min(0)]
    public int minAmount = 1;

    [Tooltip("Maximum amount to drop")]
    [Min(1)]
    public int maxAmount = 3;
}
