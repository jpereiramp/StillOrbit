using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Executes enemy abilities, handling damage, VFX, and SFX.
/// Called by attack states - never by AI decision logic.
/// </summary>
public class EnemyAbilityExecutor : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("References")]
    [Tooltip("Transform for melee attack origin")]
    [SerializeField] private Transform meleeOrigin;

    [BoxGroup("References")]
    [Tooltip("Transform for ranged attack origin")]
    [SerializeField] private Transform rangedOrigin;

    [BoxGroup("Melee Settings")]
    [SerializeField] private LayerMask meleeHitLayers;

    [BoxGroup("Ranged Settings")]
    [SerializeField] private LayerMask rangedHitLayers;

    [BoxGroup("Ranged Settings")]
    [SerializeField] private GameObject projectilePrefab;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private readonly List<IDamageable> hitThisAttack = new();

    private AudioSource _audioSource;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (meleeOrigin == null)
            meleeOrigin = transform;

        if (rangedOrigin == null)
            rangedOrigin = transform;
    }

    /// <summary>
    /// Execute an ability. Called by EnemyAttackState.
    /// </summary>
    public void ExecuteAbility(EnemyAbilityData ability)
    {
        if (ability == null)
        {
            Debug.LogWarning("[EnemyAbilityExecutor] Null ability");
            return;
        }

        hitThisAttack.Clear();

        // Determine ability type based on range
        if (ability.MaxRange <= 3f)
        {
            ExecuteMeleeAbility(ability);
        }
        else
        {
            ExecuteRangedAbility(ability);
        }

        // Play SFX
        if (ability.SfxClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(ability.SfxClip);
        }

        // Spawn VFX
        if (ability.VfxPrefab != null)
        {
            SpawnVFX(ability.VfxPrefab, meleeOrigin.position, meleeOrigin.rotation);
        }
    }

    private void ExecuteMeleeAbility(EnemyAbilityData ability)
    {
        // Sphere overlap for melee hit detection
        Vector3 origin = meleeOrigin.position + meleeOrigin.forward * (ability.MaxRange / 2f);
        float radius = ability.MaxRange / 2f;

        Collider[] hits = Physics.OverlapSphere(origin, radius, meleeHitLayers);

        foreach (var hit in hits)
        {
            // Skip self
            if (hit.transform.root == transform.root)
                continue;

            ProcessHit(hit.gameObject, ability, hit.ClosestPoint(origin));
        }
    }

    private void ExecuteRangedAbility(EnemyAbilityData ability)
    {
        if (projectilePrefab != null)
        {
            // Spawn projectile
            SpawnProjectile(ability);
        }
        else
        {
            // Instant raycast
            ExecuteRaycastAbility(ability);
        }
    }

    private void ExecuteRaycastAbility(EnemyAbilityData ability)
    {
        Vector3 origin = rangedOrigin.position;
        Vector3 direction = rangedOrigin.forward;

        // Aim at target if available
        if (controller.Context?.CurrentTarget != null)
        {
            direction = (controller.Context.CurrentTarget.position - origin).normalized;
        }

        if (Physics.Raycast(origin, direction, out RaycastHit hit, ability.MaxRange, rangedHitLayers))
        {
            ProcessHit(hit.collider.gameObject, ability, hit.point);

            // Spawn impact VFX
            if (ability.VfxPrefab != null)
            {
                SpawnVFX(ability.VfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }
    }

    private void SpawnProjectile(EnemyAbilityData ability)
    {
        Vector3 origin = rangedOrigin.position;
        Vector3 direction = rangedOrigin.forward;

        // Aim at target
        if (controller.Context?.CurrentTarget != null)
        {
            direction = (controller.Context.CurrentTarget.position - origin).normalized;
        }

        GameObject projectileObj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));

        var projectile = projectileObj.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(
                ability.BaseDamage,
                ability.DamageType,
                gameObject,
                rangedHitLayers
            );
        }
    }

    private void ProcessHit(GameObject hitObject, EnemyAbilityData ability, Vector3 hitPoint)
    {
        IDamageable damageable = FindDamageable(hitObject);
        if (damageable == null)
            return;

        // Prevent hitting same target twice
        if (hitThisAttack.Contains(damageable))
            return;

        hitThisAttack.Add(damageable);

        // Apply damage through IDamageable (existing system)
        float damage = ability.BaseDamage;

        // Apply archetype damage resistance (if target has one)
        // Note: This uses the EXISTING TakeDamage interface unchanged
        damageable.TakeDamage(damage, ability.DamageType, gameObject);

        // Hit effects
        var hitEffectReceiver = hitObject.GetComponentInParent<HitEffectReceiver>();
        hitEffectReceiver?.PlayHitEffect(hitPoint, (hitPoint - transform.position).normalized);

        Debug.Log($"[EnemyAbilityExecutor] Hit {hitObject.name} for {damage} damage");
    }

    private IDamageable FindDamageable(GameObject obj)
    {
        // Check self, parent, root (same pattern as PlayerCombatManager)
        var damageable = obj.GetComponent<IDamageable>();
        if (damageable != null) return damageable;

        if (obj.transform.parent != null)
        {
            damageable = obj.transform.parent.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        if (obj.transform.root != null && obj.transform.root != obj.transform)
        {
            damageable = obj.transform.root.GetComponent<IDamageable>();
            if (damageable != null) return damageable;
        }

        return null;
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var vfx = Instantiate(prefab, position, rotation);
        Destroy(vfx, 3f); // Auto-cleanup
    }

    /// <summary>
    /// Called by animation events for melee attacks with specific timing.
    /// </summary>
    public void AnimationEvent_MeleeHit()
    {
        var ability = controller.Archetype?.PrimaryAbility;
        if (ability != null)
        {
            ExecuteMeleeAbility(ability);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (meleeOrigin == null) return;

        var ability = controller?.Archetype?.PrimaryAbility;
        float range = ability?.MaxRange ?? 2f;

        Gizmos.color = Color.red;
        Vector3 origin = meleeOrigin.position + meleeOrigin.forward * (range / 2f);
        Gizmos.DrawWireSphere(origin, range / 2f);
    }
#endif
}