using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    #region Fields
    private PlayerControls controls;

    // Movement
    public Vector2 MoveInput = Vector2.zero;
    public Vector2 LookInput = Vector2.zero;
    public bool JumpInput = false;
    public bool SprintInput = false;
    public bool CrouchInput = false;

    // Actions
    public bool PrimaryActionInput = false;
    public bool SecondaryActionInput = false;
    public bool InteractInput = false;
    public bool DropInput = false;

    // Building
    public bool ToggleBuildModePressed = false;
    public bool RotateBuildingPressed = false;
    public bool ConfirmBuildPressed = false;
    public bool CancelBuildPressed = false;

    // Inventory
    public bool ToggleInventoryPressed = false;

    // Quick Slots
    public int QuickSlotPressed = QuickSlotController.EmptySlotIndex;

    // Combat
    public bool ReloadPressed = false;

    // Companion
    public bool CallCompanionPressed = false;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        // Movement
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

        // Actions
        controls.Player.PrimaryAction.performed += OnPrimaryAction;
        controls.Player.PrimaryAction.canceled += OnPrimaryAction;
        controls.Player.SecondaryAction.performed += OnSecondaryAction;
        controls.Player.SecondaryAction.canceled += OnSecondaryAction;
        controls.Player.Interact.performed += OnInteract;
        controls.Player.Interact.canceled += OnInteract;
        controls.Player.Drop.performed += OnDrop;
        controls.Player.Drop.canceled += OnDrop;

        // Build
        controls.Player.ToggleBuildMode.performed += OnToggleBuildMode;
        controls.Player.ToggleBuildMode.canceled += OnToggleBuildMode;
        controls.Player.RotateBuilding.performed += OnRotateBuilding;
        controls.Player.RotateBuilding.canceled += OnRotateBuilding;
        controls.Player.ConfirmBuildingPlacement.performed += OnConfirmBuild;
        controls.Player.ConfirmBuildingPlacement.canceled += OnConfirmBuild;
        controls.Player.CancelBuildingPlacement.performed += OnCancelBuild;
        controls.Player.CancelBuildingPlacement.canceled += OnCancelBuild;

        // Inventory
        controls.Player.ToggleInventory.performed += OnToggleInventory;
        controls.Player.ToggleInventory.canceled += OnToggleInventory;

        // Quick Slot (0-indexed: slot 1 = index 0, slot 5 = index 4)
        controls.Player.QuickSlot1.performed += context => OnQuickSlotSelect(context, 0);
        controls.Player.QuickSlot1.canceled += context => OnQuickSlotSelect(context, 0);
        controls.Player.QuickSlot2.performed += context => OnQuickSlotSelect(context, 1);
        controls.Player.QuickSlot2.canceled += context => OnQuickSlotSelect(context, 1);
        controls.Player.QuickSlot3.performed += context => OnQuickSlotSelect(context, 2);
        controls.Player.QuickSlot3.canceled += context => OnQuickSlotSelect(context, 2);
        controls.Player.QuickSlot4.performed += context => OnQuickSlotSelect(context, 3);
        controls.Player.QuickSlot4.canceled += context => OnQuickSlotSelect(context, 3);
        controls.Player.QuickSlot5.performed += context => OnQuickSlotSelect(context, 4);
        controls.Player.QuickSlot5.canceled += context => OnQuickSlotSelect(context, 4);

        // Combat
        controls.Player.Reload.performed += OnReload;
        controls.Player.Reload.canceled += OnReload;

        // Companion
        controls.Player.CallCompanion.performed += OnCallCompanion;
        controls.Player.CallCompanion.canceled += OnCallCompanion;

        controls.Enable();
    }

    private void OnDisable()
    {
        // Movement
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

        // Actions
        controls.Player.PrimaryAction.performed -= OnPrimaryAction;
        controls.Player.PrimaryAction.canceled -= OnPrimaryAction;
        controls.Player.SecondaryAction.performed -= OnSecondaryAction;
        controls.Player.SecondaryAction.canceled -= OnSecondaryAction;
        controls.Player.Interact.performed -= OnInteract;
        controls.Player.Interact.canceled -= OnInteract;
        controls.Player.Drop.performed -= OnDrop;
        controls.Player.Drop.canceled -= OnDrop;

        // Building
        controls.Player.ToggleBuildMode.performed -= OnToggleBuildMode;
        controls.Player.ToggleBuildMode.canceled -= OnToggleBuildMode;
        controls.Player.RotateBuilding.performed -= OnRotateBuilding;
        controls.Player.RotateBuilding.canceled -= OnRotateBuilding;
        controls.Player.ConfirmBuildingPlacement.performed -= OnConfirmBuild;
        controls.Player.ConfirmBuildingPlacement.canceled -= OnConfirmBuild;
        controls.Player.CancelBuildingPlacement.performed -= OnCancelBuild;
        controls.Player.CancelBuildingPlacement.canceled -= OnCancelBuild;

        // Inventory
        controls.Player.ToggleInventory.performed -= OnToggleInventory;
        controls.Player.ToggleInventory.canceled -= OnToggleInventory;

        // Quick Slot (0-indexed: slot 1 = index 0, slot 5 = index 4)
        controls.Player.QuickSlot1.performed -= context => OnQuickSlotSelect(context, 0);
        controls.Player.QuickSlot1.canceled -= context => OnQuickSlotSelect(context, 0);
        controls.Player.QuickSlot2.performed -= context => OnQuickSlotSelect(context, 1);
        controls.Player.QuickSlot2.canceled -= context => OnQuickSlotSelect(context, 1);
        controls.Player.QuickSlot3.performed -= context => OnQuickSlotSelect(context, 2);
        controls.Player.QuickSlot3.canceled -= context => OnQuickSlotSelect(context, 2);
        controls.Player.QuickSlot4.performed -= context => OnQuickSlotSelect(context, 3);
        controls.Player.QuickSlot4.canceled -= context => OnQuickSlotSelect(context, 3);
        controls.Player.QuickSlot5.performed -= context => OnQuickSlotSelect(context, 4);
        controls.Player.QuickSlot5.canceled -= context => OnQuickSlotSelect(context, 4);

        // Combat
        controls.Player.Reload.performed -= OnReload;
        controls.Player.Reload.canceled -= OnReload;

        // Companion
        controls.Player.CallCompanion.performed -= OnCallCompanion;
        controls.Player.CallCompanion.canceled -= OnCallCompanion;

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
        if (context.performed)
            ToggleBuildModePressed = true;
    }

    public void OnRotateBuilding(InputAction.CallbackContext context)
    {
        if (context.performed)
            RotateBuildingPressed = true;
    }

    public void OnConfirmBuild(InputAction.CallbackContext context)
    {
        if (context.performed)
            ConfirmBuildPressed = true;
    }

    public void OnCancelBuild(InputAction.CallbackContext context)
    {
        if (context.performed)
            CancelBuildPressed = true;
    }

    public void OnCallCompanion(InputAction.CallbackContext context)
    {
        if (context.performed)
            CallCompanionPressed = true;
    }

    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
            ToggleInventoryPressed = true;
    }

    public void OnQuickSlotSelect(InputAction.CallbackContext context, int slotIndex)
    {
        if (context.performed)
            QuickSlotPressed = slotIndex;
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed)
            ReloadPressed = true;
    }
    #endregion
}