using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Canvas Groups")]
    [SerializeField] private Canvas hudCanvas;

    [Header("HUD Text References")]
    [SerializeField] private TextMeshProUGUI healthTxt;
    [SerializeField] private TextMeshProUGUI manaTxt;
    [SerializeField] private TextMeshProUGUI coinsTxt;
    [SerializeField] private TextMeshProUGUI roundTxt;

    [Header("Inventory UI Settings")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    [SerializeField] private List<string> inventoryKeyLabels = new List<string> { "Z", "X", "C" };

    private List<InventorySlot> uiSlots = new List<InventorySlot>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeUI();
    }

    private void InitializeUI()
    {
        SetHUDVisibility(false);
    }

    public void SetHUDVisibility(bool visible)
    {
        if (hudCanvas != null) hudCanvas.enabled = visible;
    }

    public void UpdateHUD(int hp, int maxHp, int mana, int coins)
    {
        if (healthTxt != null) healthTxt.text = $"Health: {hp}/{maxHp}";
        if (manaTxt != null) manaTxt.text = $"Mana: {mana}";
        if (coinsTxt != null) coinsTxt.text = $"Coins: {coins}";
    }

    public void UpdateRound(int roundNumber)
    {
        if (roundTxt != null) roundTxt.text = $"Round: {roundNumber}";
    }

    public void InitializeInventoryUI(int slotCount)
    {
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        uiSlots.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotParent);
            InventorySlot slotScript = newSlot.GetComponent<InventorySlot>();
            uiSlots.Add(slotScript);
        }
    }

    public void RefreshInventory(List<Item> inventory, int selectedIndex)
    {
        for (int i = 0; i < uiSlots.Count; i++)
        {
            if (i < inventory.Count)
            {
                // Pull label from our internal list, or use index + 1 as fallback
                string label = (i < inventoryKeyLabels.Count) ? inventoryKeyLabels[i] : (i + 1).ToString();

                uiSlots[i].UpdateSlot(inventory[i], i == selectedIndex, label);
            }
        }
    }
}