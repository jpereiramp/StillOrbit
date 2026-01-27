using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryUIController : MonoBehaviour
{
    [BoxGroup("References")]
    [SerializeField]
    [Required]
    private PlayerInputHandler inputHandler;

    private void Update()
    {
        HandleToggleInventoryInput();
    }

    private void HandleToggleInventoryInput()
    {
        if (inputHandler.ToggleInventoryPressed)
        {
            UIManager.Instance.TogglePanel<InventoryPanel>();
            inputHandler.ToggleInventoryPressed = false;
        }
    }
}