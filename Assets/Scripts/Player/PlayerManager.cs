using UnityEngine;

/// <summary>
/// Coordinates input, camera, and locomotion systems for the player.
/// Acts as a bridge between PlayerInputHandler, PlayerCameraController, and PlayerLocomotionController.
/// Also provides centralized access to all player subsystems.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerLocomotionController locomotionController;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerEquipmentController equipmentController;
    [SerializeField] private PlayerAimController aimController;
    [SerializeField] private PlayerInteractionController interactionController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private HealthComponent healthComponent;

    public PlayerLocomotionController LocomotionController => locomotionController;
    public PlayerCameraController CameraController => cameraController;
    public PlayerInputHandler InputHandler => inputHandler;
    public PlayerEquipmentController EquipmentController => equipmentController;
    public PlayerAimController AimController => aimController;
    public PlayerInteractionController InteractionController => interactionController;
    public PlayerInventory Inventory => inventory;
    public HealthComponent HealthComponent => healthComponent;

    // Edge detection for button inputs
    private bool _previousJumpInput;
    private bool _previousCrouchInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Set the camera to follow the locomotion controller's camera follow point
        if (locomotionController != null && locomotionController.CameraFollowPoint != null)
        {
            cameraController.SetFollowTarget(locomotionController.CameraFollowPoint);
        }
    }

    private void Update()
    {
        if (locomotionController == null || inputHandler == null || cameraController == null)
        {
            return;
        }

        HandleCharacterInput();

        // Update previous input states for edge detection
        _previousJumpInput = inputHandler.JumpInput;
        _previousCrouchInput = inputHandler.CrouchInput;
    }

    private void HandleCharacterInput()
    {
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

        // Movement input
        characterInputs.MoveAxisForward = inputHandler.MoveInput.y;
        characterInputs.MoveAxisRight = inputHandler.MoveInput.x;

        // Camera rotation for camera-relative movement
        characterInputs.CameraRotation = cameraController.CameraRotation;

        // Jump - detect rising edge (pressed this frame)
        characterInputs.JumpDown = inputHandler.JumpInput && !_previousJumpInput;

        // Crouch - detect edges (pressed/released this frame)
        characterInputs.CrouchDown = inputHandler.CrouchInput && !_previousCrouchInput;
        characterInputs.CrouchUp = !inputHandler.CrouchInput && _previousCrouchInput;

        // Apply inputs to the locomotion controller
        locomotionController.SetInputs(ref characterInputs);
    }
}
