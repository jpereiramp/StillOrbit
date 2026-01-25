using UnityEngine;

/// <summary>
/// Coordinates input, camera, and locomotion systems for the player.
/// Acts as a bridge between PlayerInputHandler, PlayerCameraController, and PlayerLocomotionController.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLocomotionController locomotionController;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private PlayerInputHandler inputHandler;

    // Edge detection for button inputs
    private bool _previousJumpInput;
    private bool _previousCrouchInput;

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
