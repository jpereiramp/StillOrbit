using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    #region Fields
    private PlayerControls controls;

    public Vector2 MoveInput = Vector2.zero;
    public Vector2 LookInput = Vector2.zero;
    public bool JumpInput = false;
    public bool SprintInput = false;
    public bool CrouchInput = false;
    public bool PrimaryActionInput = false;
    public bool SecondaryActionInput = false;
    public bool InteractInput = false;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        controls.Player.Look.performed += OnLook;
        controls.Player.Look.canceled += OnLook;
        controls.Player.Jump.performed += OnJump;
        controls.Player.Jump.canceled += OnJump;
        controls.Player.Sprint.performed += OnSprint;
        controls.Player.Sprint.canceled += OnSprint;
        controls.Player.Crouch.performed += OnCrouch;
        controls.Player.Crouch.canceled += OnCrouch;
        controls.Player.PrimaryAction.performed += OnPrimaryAction;
        controls.Player.PrimaryAction.canceled += OnPrimaryAction;
        controls.Player.SecondaryAction.performed += OnSecondaryAction;
        controls.Player.SecondaryAction.canceled += OnSecondaryAction;
        controls.Player.Interact.performed += OnInteract;
        controls.Player.Interact.canceled += OnInteract;

        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Player.Look.performed -= OnLook;
        controls.Player.Look.canceled -= OnLook;
        controls.Player.Jump.performed -= OnJump;
        controls.Player.Jump.canceled -= OnJump;
        controls.Player.Sprint.performed -= OnSprint;
        controls.Player.Sprint.canceled -= OnSprint;
        controls.Player.Crouch.performed -= OnCrouch;
        controls.Player.Crouch.canceled -= OnCrouch;
        controls.Player.PrimaryAction.performed -= OnPrimaryAction;
        controls.Player.PrimaryAction.canceled -= OnPrimaryAction;
        controls.Player.SecondaryAction.performed -= OnSecondaryAction;
        controls.Player.SecondaryAction.canceled -= OnSecondaryAction;
        controls.Player.Interact.performed -= OnInteract;
        controls.Player.Interact.canceled -= OnInteract;

        controls.Disable();
    }
    #endregion

    #region Callback Methods
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        JumpInput = context.ReadValueAsButton();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        SprintInput = context.ReadValueAsButton();
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        CrouchInput = context.ReadValueAsButton();
    }

    public void OnPrimaryAction(InputAction.CallbackContext context)
    {
        PrimaryActionInput = context.ReadValueAsButton();
    }

    public void OnSecondaryAction(InputAction.CallbackContext context)
    {
        SecondaryActionInput = context.ReadValueAsButton();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        InteractInput = context.ReadValueAsButton();
    }
    #endregion
}