using System.Collections.Generic;
using BrainFailProductions.PolyFew.UnityMeshSimplifier;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryPanel : UIPanel
{
    [BoxGroup("References")]
    [SerializeField]
    private PlayerInventory playerInventory;

    [BoxGroup("References")]
    [SerializeField]
    private Transform slotsContainer;

    [BoxGroup("References")]
    [SerializeField]
    private InventorySlotUI slotPrefab;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private bool isInitialized = false;

    private void Start()
    {
        InitializeSlots();

        // Start hidden
        Hide();
    }

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged += OnSlotChanged;
        }
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= OnSlotChanged;
        }
    }

    private void InitializeSlots()
    {
        if (isInitialized) return;
        if (playerInventory == null || slotPrefab == null || slotsContainer == null)
        {
            Debug.LogError("[InventoryPanel] Missing references for initialization.");
            return;
        }

        // Clear all existing slots
        DestroyAllSlots();

        // Create new UI slot for each inventory slot
        int slotCount = playerInventory.SlotCount;
        for (int i = 0; i < slotCount; i++)
        {
            InventorySlotUI slotUI = Instantiate(slotPrefab, slotsContainer);
            slotUI.Initialize(i);
            slotUIs.Add(slotUI);
        }

        // Initial refresh
        RefreshAllSlots();
        isInitialized = true;
    }

    private void OnSlotChanged(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slotUIs.Count)
        {
            RefreshSlot(slotIndex);
        }
    }

    private void RefreshSlot(int index)
    {
        InventorySlot slot = playerInventory.GetSlot(index);
        slotUIs[index].UpdateDisplay(slot.ItemData, slot.Quantity);
    }

    private void RefreshAllSlots()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            RefreshSlot(i);
        }
    }

    private void DestroyAllSlots()
    {
        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }
        slotUIs.Clear();
    }

    protected override void OnShow()
    {
        base.OnShow();

        if (isInitialized)
        {
            RefreshAllSlots();
        }


        PlayerManager.Instance.AimController.SetCursorInteractionEnabled(true);
    }

    protected override void OnHide()
    {
        base.OnHide();

        PlayerManager.Instance.AimController.SetCursorInteractionEnabled(false);
    }
}