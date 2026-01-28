using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Movement-driven footstep system for any grounded character (player or enemy).
/// Tracks distance traveled on the ground and plays footstep sounds at regular intervals.
/// Surface detection uses PhysicsMaterial on ground colliders.
///
/// Attach to any character with a movement system. Call UpdateMovement() each frame
/// with the character's grounded state and velocity.
/// </summary>
public class FootstepEmitter : MonoBehaviour
{
    [BoxGroup("Configuration")]
    [Required]
    [Tooltip("Footstep surface data asset.")]
    [SerializeField] private FootstepSurfaceData surfaceData;

    [BoxGroup("Configuration")]
    [Tooltip("Distance in meters between footstep sounds.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float stepDistance = 2f;

    [BoxGroup("Configuration")]
    [Tooltip("Origin point for ground raycasts. Defaults to this transform if not set.")]
    [SerializeField] private Transform footOrigin;

    [BoxGroup("Configuration")]
    [Tooltip("Layer mask for ground detection raycasts.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [BoxGroup("Configuration")]
    [Tooltip("How far down to raycast for ground surface detection.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float raycastDistance = 1.5f;

    // Internal state
    private float _distanceSinceLastStep;
    private bool _isGrounded;
    private bool _isMoving;
    private Vector3 _lastPosition;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 20f;

        if (footOrigin == null)
        {
            footOrigin = transform;
        }

        _lastPosition = transform.position;
    }

    /// <summary>
    /// Call this every frame from the owning movement system.
    /// Determines whether a footstep should play based on distance traveled.
    /// </summary>
    /// <param name="isGrounded">Whether the character is on stable ground.</param>
    /// <param name="horizontalSpeed">Horizontal speed magnitude (used to gate idle vs moving).</param>
    public void UpdateMovement(bool isGrounded, float horizontalSpeed)
    {
        _isGrounded = isGrounded;
        _isMoving = horizontalSpeed > 0.1f;

        if (!_isGrounded || !_isMoving)
        {
            // Reset accumulation when not moving on ground — prevents
            // a step playing immediately after landing if distance was accumulated in air.
            _distanceSinceLastStep = 0f;
            _lastPosition = transform.position;
            return;
        }

        // Accumulate horizontal distance traveled
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - _lastPosition;
        delta.y = 0f;
        _distanceSinceLastStep += delta.magnitude;
        _lastPosition = currentPos;

        if (_distanceSinceLastStep >= stepDistance)
        {
            _distanceSinceLastStep = 0f;
            PlayFootstep();
        }
    }

    /// <summary>
    /// Raycasts down to detect the ground surface and plays the appropriate clip.
    /// </summary>
    private void PlayFootstep()
    {
        if (surfaceData == null) return;

        // Detect surface
        FootstepSurfaceData.SurfaceEntry surface = surfaceData.defaultSurface;

        if (Physics.Raycast(footOrigin.position + Vector3.up * 0.1f, Vector3.down,
            out RaycastHit hit, raycastDistance, groundLayers))
        {
            PhysicsMaterial mat = hit.collider.sharedMaterial;
            surface = surfaceData.GetSurface(mat);
        }

        AudioClip clip = surface.GetRandomClip();
        if (clip == null) return;

        // Apply pitch variation
        _audioSource.pitch = 1f + Random.Range(-surface.pitchVariation, surface.pitchVariation);
        _audioSource.PlayOneShot(clip, surface.volume);
    }

    /// <summary>
    /// Sets the step distance at runtime (e.g., for sprinting vs walking).
    /// </summary>
    public void SetStepDistance(float distance)
    {
        stepDistance = Mathf.Max(0.1f, distance);
    }

#if UNITY_EDITOR
    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private float DebugDistanceSinceLastStep => _distanceSinceLastStep;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool DebugIsGrounded => _isGrounded;

    [BoxGroup("Debug")]
    [ShowInInspector, ReadOnly]
    private bool DebugIsMoving => _isMoving;
#endif
}
