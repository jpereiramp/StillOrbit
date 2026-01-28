using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Handles flying movement for airborne enemies.
/// Replaces NavMeshAgent movement when MovementType is Flying.
/// </summary>
public class EnemyFlyingMovement : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField] private EnemyController controller;

    [BoxGroup("Settings")]
    [SerializeField] private float hoverVariation = 0.5f;

    [BoxGroup("Settings")]
    [SerializeField] private float hoverSpeed = 2f;

    [BoxGroup("Settings")]
    [SerializeField] private float bankAngle = 15f;

    [BoxGroup("Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;

    [BoxGroup("Avoidance")]
    [SerializeField] private float avoidanceDistance = 3f;

    private Vector3 _targetPosition;
    private float _currentHoverOffset;
    private float _hoverPhase;
    private float _hoverHeight;
    private bool _isStopped;

    public bool HasReachedDestination { get; private set; }
    public bool IsStopped => _isStopped;
    public Vector3 CurrentDestination => _targetPosition;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<EnemyController>();

        _hoverPhase = Random.value * Mathf.PI * 2f; // Random start phase
    }

    private void Start()
    {
        // Cache hover height from archetype
        if (controller != null && controller.Archetype != null)
        {
            _hoverHeight = controller.Archetype.FlyingHeight;
        }
        else
        {
            _hoverHeight = 3f; // Default
        }

        _targetPosition = transform.position;
    }

    private void Update()
    {
        if (controller == null || !controller.IsInitialized)
            return;

        if (controller.Archetype.MovementType != EnemyMovementType.Flying)
            return;

        // Cache hover height if not set (for dynamic initialization)
        if (_hoverHeight <= 0)
        {
            _hoverHeight = controller.Archetype.FlyingHeight;
        }

        UpdateHover();

        if (!_isStopped)
        {
            UpdateMovement();
            UpdateRotation();
        }
    }

    /// <summary>
    /// Set movement destination.
    /// </summary>
    public void SetDestination(Vector3 destination)
    {
        // Adjust for flying height
        _targetPosition = destination + Vector3.up * _hoverHeight;
        HasReachedDestination = false;
        _isStopped = false;
    }

    /// <summary>
    /// Stop movement.
    /// </summary>
    public void Stop()
    {
        _targetPosition = transform.position;
        HasReachedDestination = true;
        _isStopped = true;
    }

    /// <summary>
    /// Resume movement.
    /// </summary>
    public void Resume()
    {
        _isStopped = false;
    }

    /// <summary>
    /// Get remaining distance to target.
    /// </summary>
    public float GetRemainingDistance()
    {
        return Vector3.Distance(transform.position, _targetPosition);
    }

    private void UpdateHover()
    {
        // Sine wave hover
        _hoverPhase += Time.deltaTime * hoverSpeed;
        _currentHoverOffset = Mathf.Sin(_hoverPhase) * hoverVariation;
    }

    private void UpdateMovement()
    {
        Vector3 targetWithHover = _targetPosition + Vector3.up * _currentHoverOffset;
        Vector3 direction = targetWithHover - transform.position;
        float distance = direction.magnitude;

        if (distance < 0.5f)
        {
            HasReachedDestination = true;
            return;
        }

        // Obstacle avoidance
        Vector3 avoidance = CalculateAvoidance();
        direction = (direction.normalized + avoidance).normalized;

        // Move
        float speed = controller.Archetype.MoveSpeed;
        transform.position += direction * speed * Time.deltaTime;
    }

    private void UpdateRotation()
    {
        Vector3 velocity = (_targetPosition - transform.position).normalized;

        if (velocity.sqrMagnitude > 0.01f)
        {
            // Face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(velocity);

            // Add banking based on lateral movement
            float lateralSpeed = Vector3.Dot(velocity, transform.right);
            Quaternion bankRotation = Quaternion.Euler(0, 0, -lateralSpeed * bankAngle);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation * bankRotation,
                controller.Archetype.TurnSpeed * Time.deltaTime * Mathf.Deg2Rad
            );
        }
    }

    private Vector3 CalculateAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        int rayCount = 8;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, avoidanceDistance, obstacleLayer))
            {
                float strength = 1f - (hit.distance / avoidanceDistance);
                avoidance -= direction * strength;
            }
        }

        // Vertical avoidance - maintain minimum height
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, _hoverHeight + 1f, obstacleLayer))
        {
            if (groundHit.distance < _hoverHeight)
            {
                avoidance += Vector3.up * (_hoverHeight - groundHit.distance);
            }
        }

        return avoidance.normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, avoidanceDistance);

        if (Application.isPlaying)
        {
            Gizmos.color = HasReachedDestination ? Color.green : Color.yellow;
            Gizmos.DrawLine(transform.position, _targetPosition);
            Gizmos.DrawWireSphere(_targetPosition, 0.5f);
        }
    }
#endif
}
