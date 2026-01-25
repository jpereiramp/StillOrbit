using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera mainCamera;
    [SerializeField]
    private Transform cameraRig;

    [SerializeField]
    private Transform followTarget;

    [SerializeField]
    private Transform orientation;

    [Header("Camera Settings")]
    [SerializeField]
    private float mouseSensitivity = 1f;
    [SerializeField]
    [Range(-90f, 0f)]
    private float verticalRotationMinLimit = -45f;
    [SerializeField]
    [Range(0, 90f)]
    private float verticalRotationMaxLimit = 45f;

    private PlayerInputHandler playerInputHandler;

    private float xRotation;
    private float yRotation;

    /// <summary>
    /// Gets the current camera rotation quaternion for camera-relative movement.
    /// </summary>
    public Quaternion CameraRotation => mainCamera != null
        ? mainCamera.transform.rotation
        : Quaternion.identity;

    /// <summary>
    /// Sets the transform that the camera should follow.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    private void Awake()
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
        mainCamera = AttemptToFindMainCamera();
    }

    private void Start()
    {
        // Lock the cursor to the center of the screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Get the look input from the PlayerInputHandler
        Vector2 lookInput = playerInputHandler.LookInput;

        // Apply the look input to rotate the camera
        float mouseX = lookInput.x * Time.deltaTime * mouseSensitivity;
        float mouseY = lookInput.y * Time.deltaTime * mouseSensitivity;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, verticalRotationMinLimit, verticalRotationMaxLimit);

        // Rotate the camera and orientation
        mainCamera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        orientation.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    private void LateUpdate()
    {
        // Update camera rig position after all other updates are complete
        if (followTarget != null)
        {
            cameraRig.position = followTarget.position;
        }
    }

    private Camera AttemptToFindMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        Camera foundCamera = FindAnyObjectByType<Camera>();
        if (foundCamera != null)
        {
            return foundCamera;
        }

        Debug.LogWarning("No camera found in the scene. Please assign a camera to the PlayerCameraController.");
        return null;
    }
}
