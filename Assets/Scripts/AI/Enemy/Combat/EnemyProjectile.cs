using UnityEngine;

/// <summary>
/// Projectile fired by ranged enemies.
/// Handles movement, collision, and damage application.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private float _damage;
    private DamageType _damageType;
    private GameObject _source;
    private LayerMask _hitLayers;
    private Rigidbody _rb;
    private bool _hasHit;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = false;
    }

    public void Initialize(float damage, DamageType damageType, GameObject source, LayerMask hitLayers)
    {
        _damage = damage;
        _damageType = damageType;
        _source = source;
        _hitLayers = hitLayers;

        // Set velocity
        _rb.linearVelocity = transform.forward * speed;

        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // Check layer
        if ((_hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // Don't hit source
        if (other.transform.root == _source?.transform.root)
            return;

        _hasHit = true;

        // Apply damage
        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage, _damageType, _source);
        }

        // Hit effects
        var hitReceiver = other.GetComponentInParent<HitEffectReceiver>();
        hitReceiver?.PlayHitEffect(transform.position, -transform.forward);

        // Impact VFX
        if (impactVfxPrefab != null)
        {
            var vfx = Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // Impact SFX
        if (impactSfx != null)
        {
            AudioSource.PlayClipAtPoint(impactSfx, transform.position);
        }

        Destroy(gameObject);
    }
}