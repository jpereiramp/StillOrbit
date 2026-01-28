using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Orchestrates the player death and respawn flow.
/// Listens to HealthComponent.OnDeath, then:
///   1. Disables player input/movement
///   2. Drops all inventory items into the world
///   3. Clears resource inventory
///   4. Triggers death music state
///   5. Shows death screen with countdown
///   6. Respawns player after timer expires
///
/// Attach to the Player GameObject alongside PlayerManager.
/// </summary>
public class PlayerDeathController : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [Tooltip("Time in seconds before the player respawns.")]
    [Range(1f, 30f)]
    [SerializeField] private float respawnDelay = 5f;

    [BoxGroup("Configuration")]
    [Tooltip("Where the player respawns. Defaults to Vector3.zero.")]
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;

    [BoxGroup("Configuration")]
    [Tooltip("Radius around death position where dropped items scatter.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float itemDropRadius = 2f;

    [BoxGroup("Configuration")]
    [Tooltip("Upward force applied to dropped items.")]
    [Range(0f, 5f)]
    [SerializeField] private float itemDropUpForce = 2f;

    [BoxGroup("References")]
    [Tooltip("Optional. Auto-resolved from PlayerManager if not set.")]
    [SerializeField] private PlayerManager playerManager;

    // Resolved references
    private HealthComponent _health;
    private PlayerInventory _inventory;
    private PlayerResourceInventory _resourceInventory;
    private PlayerEquipmentController _equipment;
    private PlayerLocomotionController _locomotion;

    private bool _isDead;
    private Coroutine _respawnCoroutine;

    /// <summary>
    /// Fired when the player dies. UI and other systems can subscribe.
    /// Parameter: respawn delay in seconds.
    /// </summary>
    public event Action<float> OnPlayerDied;

    /// <summary>
    /// Fired each frame during the respawn countdown.
    /// Parameter: remaining seconds.
    /// </summary>
    public event Action<float> OnRespawnTimerTick;

    /// <summary>
    /// Fired when the player has respawned and regained control.
    /// </summary>
    public event Action OnPlayerRespawned;

    /// <summary>
    /// Whether the player is currently dead.
    /// </summary>
    public bool IsDead => _isDead;

    /// <summary>
    /// The configured respawn delay in seconds.
    /// </summary>
    public float RespawnDelay => respawnDelay;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
        }
        else
        {
            Debug.LogError("[PlayerDeath] No HealthComponent found on player. Death system disabled.");
        }
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
        }
    }

    /// <summary>
    /// Resolves all required component references from the PlayerManager
    /// or directly from the GameObject.
    /// </summary>
    private void ResolveReferences()
    {
        if (playerManager == null)
        {
            playerManager = GetComponent<PlayerManager>();
            if (playerManager == null)
            {
                playerManager = PlayerManager.Instance;
            }
        }

        if (playerManager != null)
        {
            _health = playerManager.HealthComponent;
            _inventory = playerManager.Inventory;
            _resourceInventory = playerManager.ResourceInventory;
            _equipment = playerManager.EquipmentController;
            _locomotion = playerManager.LocomotionController;
        }
        else
        {
            // Fallback: try to resolve directly
            _health = GetComponent<HealthComponent>();
            _inventory = GetComponent<PlayerInventory>();
            _resourceInventory = GetComponent<PlayerResourceInventory>();
            _equipment = GetComponent<PlayerEquipmentController>();
            _locomotion = GetComponent<PlayerLocomotionController>();
        }
    }

    /// <summary>
    /// Called once by HealthComponent.OnDeath.
    /// Prevents re-entry via _isDead guard.
    /// </summary>
    private void HandleDeath()
    {
        if (_isDead) return;
        _isDead = true;

        Debug.Log("[PlayerDeath] Player died. Starting death sequence.");

        // 1. Disable player control
        DisablePlayerControl();

        // 2. Drop inventory items into the world
        DropAllItems();

        // 3. Clear resource inventory
        ClearResources();

        // 4. Unequip held item
        UnequipHeldItem();

        // 5. Trigger death music
        TriggerDeathMusic();

        // 6. Notify listeners (UI will show death screen)
        OnPlayerDied?.Invoke(respawnDelay);

        // 7. Start respawn countdown
        _respawnCoroutine = StartCoroutine(RespawnCountdown());
    }

    /// <summary>
    /// Disables player movement and input processing.
    /// </summary>
    private void DisablePlayerControl()
    {
        if (_locomotion != null)
        {
            _locomotion.SetCharacterControllerMotorEnabled(false);
        }

        Debug.Log("[PlayerDeath] Player control disabled.");
    }

    /// <summary>
    /// Re-enables player movement and input processing.
    /// </summary>
    private void EnablePlayerControl()
    {
        if (_locomotion != null)
        {
            _locomotion.SetCharacterControllerMotorEnabled(true);
        }

        Debug.Log("[PlayerDeath] Player control enabled.");
    }

    /// <summary>
    /// Drops all items from PlayerInventory as world pickups near the death position.
    /// Each occupied slot spawns its WorldPrefab. Items scatter in a circle.
    /// </summary>
    private void DropAllItems()
    {
        if (_inventory == null) return;

        Vector3 deathPos = transform.position;
        int droppedCount = 0;

        // Collect items to drop first (modifying inventory while iterating)
        var itemsToDrop = new List<(ItemData data, int quantity)>();
        for (int i = 0; i < _inventory.SlotCount; i++)
        {
            var slot = _inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty) continue;
            itemsToDrop.Add((slot.ItemData, slot.Quantity));
        }

        // Remove all items from inventory
        foreach (var (data, quantity) in itemsToDrop)
        {
            _inventory.TryRemoveItem(data, quantity);
        }

        // Spawn world pickups
        foreach (var (data, quantity) in itemsToDrop)
        {
            if (data.WorldPrefab == null)
            {
                Debug.LogWarning($"[PlayerDeath] Cannot drop {data.ItemName}: no WorldPrefab assigned.");
                continue;
            }

            // For stackable items, spawn one pickup per stack (not per unit)
            // For non-stackable items, spawn one per unit
            int spawnCount = data.IsStackable ? 1 : quantity;

            for (int j = 0; j < spawnCount; j++)
            {
                Vector3 scatter = GetScatterPosition(deathPos, droppedCount);
                var worldItem = Instantiate(data.WorldPrefab, scatter, Quaternion.identity);

                // Apply upward + outward force if it has a Rigidbody
                var rb = worldItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    Vector3 outward = (scatter - deathPos).normalized;
                    rb.AddForce((outward + Vector3.up * itemDropUpForce) * 1.5f, ForceMode.VelocityChange);
                }

                droppedCount++;
            }
        }

        Debug.Log($"[PlayerDeath] Dropped {droppedCount} item(s) near death position.");
    }

    /// <summary>
    /// Calculates a scatter position around the death point for dropped items.
    /// Distributes items in a rough circle.
    /// </summary>
    private Vector3 GetScatterPosition(Vector3 center, int index)
    {
        float angle = index * 137.5f * Mathf.Deg2Rad; // Golden angle for even distribution
        float distance = Mathf.Sqrt(index + 1) * (itemDropRadius / 3f);
        distance = Mathf.Min(distance, itemDropRadius);

        return center + new Vector3(
            Mathf.Cos(angle) * distance,
            1f, // Spawn slightly above ground
            Mathf.Sin(angle) * distance
        );
    }

    /// <summary>
    /// Clears all resources from the player's resource inventory.
    /// </summary>
    private void ClearResources()
    {
        if (_resourceInventory == null) return;

        _resourceInventory.GetInventory().Clear();
        Debug.Log("[PlayerDeath] Resource inventory cleared.");
    }

    /// <summary>
    /// Unequips and destroys the currently held item.
    /// The item was already counted in the inventory drop — this just removes the visual.
    /// </summary>
    private void UnequipHeldItem()
    {
        if (_equipment == null || !_equipment.HasEquippedItem) return;

        _equipment.UnequipItem(destroy: true);
        Debug.Log("[PlayerDeath] Equipped item removed.");
    }

    /// <summary>
    /// Triggers the GameOver music state via AudioManager.
    /// </summary>
    private void TriggerDeathMusic()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicState(MusicState.GameOver);
        }
    }

    /// <summary>
    /// Coroutine that counts down to respawn, firing tick events each frame.
    /// </summary>
    private IEnumerator RespawnCountdown()
    {
        float remaining = respawnDelay;

        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            remaining = Mathf.Max(0f, remaining);

            OnRespawnTimerTick?.Invoke(remaining);

            yield return null;
        }

        PerformRespawn();
    }

    /// <summary>
    /// Executes the respawn: teleport, heal, restore control, notify listeners.
    /// </summary>
    private void PerformRespawn()
    {
        Debug.Log("[PlayerDeath] Respawning player.");

        // 1. Teleport to respawn position
        if (_locomotion != null && _locomotion.Motor != null)
        {
            _locomotion.Motor.SetPosition(respawnPosition);
        }
        else
        {
            transform.position = respawnPosition;
        }

        // 2. Restore health
        if (_health != null)
        {
            _health.SetMaxHealth(_health.MaxHealth, healToMax: true);
        }

        // 3. Re-enable control
        EnablePlayerControl();

        // 4. Restore music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ForceSetMusicState(MusicState.Exploration);
        }

        // 5. Reset death state
        _isDead = false;
        _respawnCoroutine = null;

        // 6. Notify listeners (UI will hide death screen)
        OnPlayerRespawned?.Invoke();

        Debug.Log("[PlayerDeath] Player respawned successfully.");
    }

#if UNITY_EDITOR
    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool DebugIsDead => _isDead;

    [Button("Force Death"), BoxGroup("Debug")]
    private void DebugForceDeath()
    {
        if (!Application.isPlaying) return;
        if (_health != null)
        {
            _health.TakeDamage(_health.MaxHealth * 10);
        }
    }

    [Button("Force Respawn"), BoxGroup("Debug")]
    private void DebugForceRespawn()
    {
        if (!Application.isPlaying || !_isDead) return;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
        }

        PerformRespawn();
    }
#endif
}
