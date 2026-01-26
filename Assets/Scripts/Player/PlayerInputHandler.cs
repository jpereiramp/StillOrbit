using Unity.VisualScripting;
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
    public bool DropInput = false;
    public bool ToggleBuildModeInput = false;
    public bool RotateBuildingInput = false;
    public bool ConfirmBuildInput = false;
    public bool CancelBuildInput = false;
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
        controls.Player.Drop.performed += OnDrop;
        controls.Player.Drop.canceled += OnDrop;
        controls.Player.ToggleBuildMode.performed += OnToggleBuildMode;
        controls.Player.ToggleBuildMode.canceled += OnToggleBuildMode;
        controls.Player.RotateBuilding.performed += OnRotateBuilding;
        controls.Player.RotateBuilding.canceled += OnRotateBuilding;
        controls.Player.ConfirmBuildingPlacement.performed += OnConfirmBuild;
        controls.Player.ConfirmBuildingPlacement.canceled += OnConfirmBuild;
        controls.Player.CancelBuildingPlacement.performed += OnCancelBuild;
        controls.Player.CancelBuildingPlacement.canceled += OnCancelBuild;

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
        controls.Player.Drop.performed -= OnDrop;
        controls.Player.Drop.canceled -= OnDrop;
        controls.Player.ToggleBuildMode.performed -= OnToggleBuildMode;
        controls.Player.ToggleBuildMode.canceled -= OnToggleBuildMode;
        controls.Player.RotateBuilding.performed -= OnRotateBuilding;
        controls.Player.RotateBuilding.canceled -= OnRotateBuilding;
        controls.Player.ConfirmBuildingPlacement.performed -= OnConfirmBuild;
        controls.Player.ConfirmBuildingPlacement.canceled -= OnConfirmBuild;
        controls.Player.CancelBuildingPlacement.performed -= OnCancelBuild;
        controls.Player.CancelBuildingPlacement.canceled -= OnCancelBuild;
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

    public void OnDrop(InputAction.CallbackContext context)
    {
        DropInput = context.ReadValueAsButton();
    }

    public void OnToggleBuildMode(InputAction.CallbackContext context)
    {
        ToggleBuildModeInput = context.ReadValueAsButton();
    }

    public void OnRotateBuilding(InputAction.CallbackContext context)
    {
        RotateBuildingInput = context.ReadValueAsButton();
    }

    public void OnConfirmBuild(InputAction.CallbackContext context)
    {
        ConfirmBuildInput = context.ReadValueAsButton();
    }

    public void OnCancelBuild(InputAction.CallbackContext context)
    {
        CancelBuildInput = context.ReadValueAsButton();
    }
    #endregion
}