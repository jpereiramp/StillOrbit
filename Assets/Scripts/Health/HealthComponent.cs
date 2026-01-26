using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Manages health for any entity. Provides events for UI and game systems to react to health changes.
/// Implements IDamageable for combat system integration.
/// </summary>
public class HealthComponent : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int initialHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private bool isInvulnerable = false;

    [Header("Damage Type")]
    [Tooltip("What type of damage this entity receives (determines weapon effectiveness)")]
    [SerializeField] private DamageType damageType = DamageType.Flesh;

    /// <summary>
    /// Fired when health changes. Parameters: (currentHealth, maxHealth)
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// Fired when health reaches zero.
    /// </summary>
    public event Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    /// <summary>
    /// The type of damage this entity receives (IDamageable implementation).
    /// </summary>
    public DamageType DamageType => damageType;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(initialHealth, 0, maxHealth);
    }

    private void Start()
    {
        // Fire initial event so UI can initialize
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        if (currentHealth <= 0) return; // Already dead
        if (isInvulnerable) return; // Cannot take damage

        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    /// <summary>
    /// Set whether this entity is invulnerable to damage.
    /// </summary>
    public void SetInvulnerable(bool invulnerable)
    {
        isInvulnerable = invulnerable;
    }

    /// <summary>
    /// Check if this entity is invulnerable.
    /// </summary>
    public bool IsInvulnerable => isInvulnerable;

    /// <summary>
    /// IDamageable implementation. Converts float damage to int and applies it.
    /// </summary>
    public void TakeDamage(float amount, DamageType incomingDamageType, GameObject source)
    {
        int intDamage = Mathf.RoundToInt(amount);
        Debug.Log($"[Health] {gameObject.name} taking {intDamage} damage (type: {incomingDamageType}) from {source?.name ?? "unknown"}");
        TakeDamage(intDamage);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        if (currentHealth <= 0) return; // Can't heal if dead

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetMaxHealth(int newMaxHealth, bool healToMax = false)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healToMax)
        {
            currentHealth = maxHealth;
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public int GetHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    public int GetHealthPercentage()
    {
        return (int)(((float)currentHealth / maxHealth) * 100);
    }

    public bool IsAlive()
    {
        return currentHealth > 0;
    }

#if UNITY_EDITOR
    [Button("Take Damage"), BoxGroup("Debug")]
    private void DebugTakeDamage()
    {
        TakeDamage(10);
        Debug.Log($"Took 10 damage. Current Health: {currentHealth}/{maxHealth}");
    }
#endif

#if UNITY_EDITOR
    [Button("Heal"), BoxGroup("Debug")]
    private void DebugHeal()
    {
        Heal(10);
        Debug.Log($"Healed 10 health. Current Health: {currentHealth}/{maxHealth}");
    }
#endif
}
