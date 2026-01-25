using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager for all UI panels. Automatically discovers panels in children on Awake.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Dictionary<Type, UIPanel> panels = new Dictionary<Type, UIPanel>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DiscoverPanels();
    }

    private void DiscoverPanels()
    {
        var foundPanels = GetComponentsInChildren<UIPanel>(true);
        foreach (var panel in foundPanels)
        {
            var panelType = panel.GetType();
            if (!panels.ContainsKey(panelType))
            {
                panels[panelType] = panel;
            }
            else
            {
                Debug.LogWarning($"[UIManager] Duplicate panel type found: {panelType.Name}. Only the first instance will be registered.");
            }
        }

        Debug.Log($"[UIManager] Discovered {panels.Count} UI panels.");
    }

    /// <summary>
    /// Gets a panel by type.
    /// </summary>
    public T GetPanel<T>() where T : UIPanel
    {
        if (panels.TryGetValue(typeof(T), out var panel))
        {
            return panel as T;
        }

        Debug.LogWarning($"[UIManager] Panel of type {typeof(T).Name} not found.");
        return null;
    }

    /// <summary>
    /// Shows a panel by type.
    /// </summary>
    public void ShowPanel<T>() where T : UIPanel
    {
        var panel = GetPanel<T>();
        if (panel != null)
        {
            panel.Show();
        }
    }

    /// <summary>
    /// Hides a panel by type.
    /// </summary>
    public void HidePanel<T>() where T : UIPanel
    {
        var panel = GetPanel<T>();
        if (panel != null)
        {
            panel.Hide();
        }
    }

    /// <summary>
    /// Toggles a panel by type.
    /// </summary>
    public void TogglePanel<T>() where T : UIPanel
    {
        var panel = GetPanel<T>();
        if (panel != null)
        {
            panel.Toggle();
        }
    }
}
