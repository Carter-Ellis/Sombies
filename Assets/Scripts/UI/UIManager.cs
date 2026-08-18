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
    [SerializeField] private TextMeshProUGUI enemyCountTxt;
    [SerializeField] private TextMeshProUGUI spectatorTxt;

    [Header("Inventory UI Settings")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private List<string> inventoryKeyLabels = new List<string> { "Z", "X", "C" };

    [Header("Spell UI Settings")]
    [SerializeField] private GameObject spellSlotPrefab;
    [SerializeField] private Transform spellSlotParent;
    [SerializeField] private List<string> spellKeyLabels = new List<string> { "1", "2" };

    private List<InventorySlot> uiSlots = new List<InventorySlot>();
    private List<InventorySlot> uiSpellSlots = new List<InventorySlot>();

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

        if (hudCanvas != null && hudCanvas.GetComponent<DownedPlayerIndicatorUI>() == null)
        {
            hudCanvas.gameObject.AddComponent<DownedPlayerIndicatorUI>();
        }
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

    public void UpdateEnemyCount(int enemyCount)
    {
        if (enemyCountTxt != null) enemyCountTxt.text = $"Sombies: {enemyCount}";
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

    public void InitializeSpellUI(int slotCount)
    {
        if (spellSlotParent == null) return;
        foreach (Transform child in spellSlotParent) Destroy(child.gameObject);
        uiSpellSlots.Clear();

        if (spellSlotPrefab == null) return;

        for (int i = 0; i < slotCount; i++)
        {
            GameObject newSlot = Instantiate(spellSlotPrefab, spellSlotParent);
            InventorySlot slotScript = newSlot.GetComponent<InventorySlot>();
            if (slotScript != null)
            {
                uiSpellSlots.Add(slotScript);
            }
        }
    }

    public void RefreshSpells(List<Spell> spells, int selectedIndex)
    {
        if (spellSlotParent == null) return;

        if (spells == null || spells.Count == 0)
        {
            foreach (Transform child in spellSlotParent) Destroy(child.gameObject);
            uiSpellSlots.Clear();
            return;
        }

        if (uiSpellSlots.Count != spells.Count && spellSlotPrefab != null)
        {
            InitializeSpellUI(spells.Count);
        }

        for (int i = 0; i < uiSpellSlots.Count; i++)
        {
            Spell s = (i < spells.Count) ? spells[i] : null;
            string label = (i < spellKeyLabels.Count) ? spellKeyLabels[i] : (i + 1).ToString();
            uiSpellSlots[i].UpdateSlot(s, i == selectedIndex, label);
        }
    }

    public void SetGameplayHUDVisible(bool visible)
    {
        if (healthTxt != null) healthTxt.gameObject.SetActive(visible);
        if (manaTxt != null) manaTxt.gameObject.SetActive(visible);
        if (coinsTxt != null) coinsTxt.gameObject.SetActive(visible);
        if (roundTxt != null) roundTxt.gameObject.SetActive(visible);
        if (enemyCountTxt != null) enemyCountTxt.gameObject.SetActive(visible);

        if (slotParent != null) slotParent.gameObject.SetActive(visible);
        if (spellSlotParent != null) spellSlotParent.gameObject.SetActive(visible);
    }

    public void SetSpectatorUI(bool active, string spectatedName = "")
    {
        if (hudCanvas != null)
        {
            hudCanvas.enabled = true;
        }

        SetGameplayHUDVisible(!active);

        if (spectatorTxt == null && hudCanvas != null)
        {
            GameObject spectObj = new GameObject("SpectatorText", typeof(RectTransform), typeof(TextMeshProUGUI));
            spectObj.transform.SetParent(hudCanvas.transform, false);
            RectTransform rect = spectObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.85f);
            rect.anchorMax = new Vector2(0.5f, 0.85f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800f, 80f);

            spectatorTxt = spectObj.GetComponent<TextMeshProUGUI>();
            spectatorTxt.alignment = TextAlignmentOptions.Center;
            spectatorTxt.fontSize = 26;
            spectatorTxt.fontStyle = FontStyles.Bold;
            spectatorTxt.color = new Color(1f, 0.3f, 0.3f, 1f);
        }

        if (spectatorTxt != null)
        {
            if (hudCanvas != null)
            {
                spectatorTxt.transform.SetParent(hudCanvas.transform, false);
            }
            spectatorTxt.transform.SetAsLastSibling();
            spectatorTxt.gameObject.SetActive(active);
            if (active)
            {
                spectatorTxt.text = $"SPECTATING: {spectatedName}\n<size=70%>(Left Click to Switch Target)</size>";
            }
        }
    }
}