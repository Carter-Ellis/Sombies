using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Canvas Groups")]
    [SerializeField] private Canvas hudCanvas;

    [Header("HUD Text References")]
    [SerializeField] private TextMeshProUGUI healthTxt;
    [SerializeField] private TextMeshProUGUI manaTxt;
    [SerializeField] private TextMeshProUGUI coinsTxt;
    [SerializeField] private Image activeSpellImage;

    private void Awake()
    {
        // Simple Singleton pattern
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
        // Hide gameplay UI by default 
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
    public void UpdateSpellUI(Sprite spellSprite, int id)
    {
        print("U?");
        if (activeSpellImage != null)
        {
            print("Hello?");
            activeSpellImage.sprite = spellSprite;
            
            activeSpellImage.enabled = (spellSprite != null);

            switch (id)
            {
                case 0:
                    print("BLYE");
                    activeSpellImage.color = Color.lightBlue;
                    break;
                case 1:
                    activeSpellImage.color = Color.red; // Example color for Spell 2
                    break;
                case 2:
                    activeSpellImage.color = Color.green; // Example color for Spell 3
                    break;
                default:
                    print("WHITE");
                    activeSpellImage.color = Color.white; // Default color
                    break;
            }
        }
    }
}