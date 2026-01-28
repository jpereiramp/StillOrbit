using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bridges enemy NavMeshAgent movement to FootstepEmitter.
/// Attach to ground-based enemy prefabs alongside FootstepEmitter.
/// Do NOT add to flying or stationary enemies.
/// </summary>
public class EnemyFootstepBridge : MonoBehaviour
{
    [Tooltip("The footstep emitter to drive. Auto-resolved from this GameObject if not set.")]
    [SerializeField] private FootstepEmitter footstepEmitter;

    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        if (footstepEmitter == null)
        {
            footstepEmitter = GetComponent<FootstepEmitter>();
        }
    }

    private void Update()
    {
        if (_agent == null || footstepEmitter == null) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        bool isGrounded = _agent.isOnNavMesh && !_agent.isOnOffMeshLink;
        float speed = _agent.velocity.magnitude;
        footstepEmitter.UpdateMovement(isGrounded, speed);
    }
}
