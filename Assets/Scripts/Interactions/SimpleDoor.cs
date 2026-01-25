using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// Example IInteractable implementation for a simple door.
/// Shows how to create non-pickup interactables.
/// </summary>
public class SimpleDoor : MonoBehaviour, IInteractable
{
    [BoxGroup("State")]
    [SerializeField, ReadOnly]
    private bool isOpen;

    [BoxGroup("Settings")]
    [SerializeField]
    private string openPrompt = "Open Door";

    [BoxGroup("Settings")]
    [SerializeField]
    private string closePrompt = "Close Door";

    [BoxGroup("Animation")]
    [SerializeField]
    private Transform doorPivot;

    [BoxGroup("Animation")]
    [SerializeField]
    private float openAngle = 90f;

    [BoxGroup("Animation")]
    [SerializeField]
    private float rotationSpeed = 5f;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onDoorOpened;

    [BoxGroup("Events")]
    [SerializeField]
    private UnityEvent onDoorClosed;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    public string InteractionPrompt => isOpen ? closePrompt : openPrompt;

    private void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        targetRotation = closedRotation;
    }

    private void Update()
    {
        // Smoothly rotate to target
        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        isOpen = !isOpen;
        targetRotation = isOpen ? openRotation : closedRotation;

        if (isOpen)
            onDoorOpened?.Invoke();
        else
            onDoorClosed?.Invoke();
    }

    #if UNITY_EDITOR
    [Button("Toggle Door"), BoxGroup("Debug")]
    private void DebugToggle()
    {
        Interact(null);
    }
    #endif
}
