using UnityEngine;

/// <summary>
/// Base class for all UI panels. Provides Show/Hide/Toggle functionality via CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Shows the panel.
    /// </summary>
    public virtual void Show()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        OnShow();
    }

    /// <summary>
    /// Hides the panel.
    /// </summary>
    public virtual void Hide()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        OnHide();
    }

    /// <summary>
    /// Toggles the panel visibility.
    /// </summary>
    public virtual void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Called when the panel is shown. Override for custom behavior.
    /// </summary>
    protected virtual void OnShow() { }

    /// <summary>
    /// Called when the panel is hidden. Override for custom behavior.
    /// </summary>
    protected virtual void OnHide() { }
}
