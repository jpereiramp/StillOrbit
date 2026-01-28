using UnityEngine;

/// <summary>
/// Makes the player perceivable by enemies.
/// </summary>
public class PlayerPerceivable : MonoBehaviour, IPerceivable
{
    [SerializeField] private Transform perceptionPoint;
    [SerializeField] private float baseNoiseLevel = 0.5f;

    private PlayerLocomotionController _locomotion;

    public Vector3 PerceptionPosition => perceptionPoint != null ? perceptionPoint.position : transform.position;
    public bool IsPerceivable => true; // Could check for invisibility power-ups, etc.
    public int TargetPriority => 100; // Player is always high priority

    public float NoiseLevel
    {
        get
        {
            // Louder when moving, sprinting, shooting
            float noise = baseNoiseLevel;

            if (_locomotion != null)
            {
                if (_locomotion.Motor.Velocity.magnitude > 0.1f) // Moving threshold
                    noise += 0.3f;
                if (_locomotion.Motor.Velocity.magnitude > _locomotion.MaxStableMoveSpeed * 0.9f) // Sprinting threshold
                    noise += 0.5f;
            }

            return Mathf.Clamp01(noise);
        }
    }

    private void Awake()
    {
        _locomotion = GetComponent<PlayerLocomotionController>();

        if (perceptionPoint == null)
            perceptionPoint = transform;
    }
}