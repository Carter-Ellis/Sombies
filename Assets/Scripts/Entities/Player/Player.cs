using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


public class Player : Entity
{

    [Header("Movement")]
    private PlayerMovement _movement;
    public override float BaseWalkSpeed => _movement.baseWalkSpeed;
    public override float BaseSprintSpeed => _movement.baseSprintSpeed;
    public override float WalkSpeed
    {
        get => _movement.walkSpeed;
        set => _movement.walkSpeed = value;
    }

    public override float SprintSpeed
    {
        get => _movement.sprintSpeed;
        set => _movement.sprintSpeed = value;
    }

    [Header("Perks")]
    public bool isHidden = false;

    [Header("Inventory")]
    [SerializeField] private List<Item> inventory = new List<Item>();
    [SerializeField] private int maxInventorySlots = 3;
    [SerializeField] private int selectedItemIndex = 0;

    [Header("Magic")]
    public Spell activeSpell;
    public Transform firepoint;
    [SerializeField] private List<Spell> spells = new List<Spell>();
    [SerializeField] private int selectedSpellIndex = 0;
    [SerializeField] private int maxSpellSlots = 2;
    [SerializeField] private int _mana = 0;
    [SerializeField] private int _maxMana = 100;

    public int Mana
    {
        get => _mana;
        set => _mana = Mathf.Clamp(value, 0, _maxMana);
    }

    [Header("Currency Components")]
    [SerializeField] private int coins = 0;

    [Header("Interaction")]
    private PurchaseSystem nearbyPurchaseSystem = null;

    protected override void Awake()
    {
        base.Awake();
        _movement = GetComponent<PlayerMovement>();

        for (int i = 0; i < maxInventorySlots; i++)
        {
            inventory.Add(null);
        }

        for (int i = 0; i < maxSpellSlots; i++)
        {
            spells.Add(null);
        }
    }

    void Start()
    {
        Health = MaxHealth;
        Mana = _maxMana;
    }


    public void AddItem(Item item)
    {
        int openSlot = FindOpenInventorySlot();
        if (openSlot != -1)
        {
            inventory[openSlot] = item;
            item.gameObject.SetActive(false);
            
        }
        else
        {
            Debug.Log("Inventory is full!");
        }
    }

    public void AddSpell(Spell spell)
    {
        int openSlot = FindOpenSpellSlot();
        if (openSlot != -1)
        {
            spells[openSlot] = spell;
            
        }
        else
        {
            spells[selectedSpellIndex] = spell;
            Debug.Log("Spell overridden learned!");
        }
        activeSpell = spells[selectedSpellIndex];

    }
    private int FindOpenInventorySlot()
    {
        for (int i = 0; i < inventory.Count; i++)
        {
            if (inventory[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    private int FindOpenSpellSlot()
    {
        for (int i = 0; i < spells.Count; i++)
        {
            if (spells[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Item hitItem = collision.GetComponent<Item>();

        if (hitItem != null)
        {
            AddItem(hitItem);  
        }

        PurchaseSystem shop = collision.GetComponent<PurchaseSystem>();
        if (shop != null)
        {
            nearbyPurchaseSystem = shop;
            Debug.Log("Press E to purchase!");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // Clear the reference if the player walks away
        PurchaseSystem shop = collision.GetComponent<PurchaseSystem>();
        if (shop != null && shop == nearbyPurchaseSystem)
        {
            nearbyPurchaseSystem = null;
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        // Only trigger once when the button is fully pressed down
        if (context.performed)
        {
            if (nearbyPurchaseSystem != null)
            {
                // Send THIS player's data to the purchase system
                nearbyPurchaseSystem.AttemptPurchase(this);
            }
        }
    }

    public void UseItem()
    {
        if (inventory[selectedItemIndex] != null)
        {
            inventory[selectedItemIndex].Use(this);
            Destroy(inventory[selectedItemIndex].gameObject);
            inventory[selectedItemIndex] = null;
        }
    }

    public void SwitchItem(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            int index = Mathf.RoundToInt(context.ReadValue<float>());

            ChangeSelectedItem(index);
        }
    }

    private void ChangeSelectedItem(int newIndex)
    {
        if (newIndex >= 0 && newIndex < maxInventorySlots)
        {
            selectedItemIndex = newIndex;
        }
    }

    public void AddCoins(int amount)
    {
        coins += amount;
    }
    public void ReloadScene()
    {
        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(buildIndex);
    }


    public void SwitchSpell(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            int index = Mathf.RoundToInt(context.ReadValue<float>());

            ChangeSelectedSpell(index);
        }
    }

    private void ChangeSelectedSpell(int newIndex)
    {
        if (newIndex >= 0 && newIndex < maxSpellSlots)
        {
            if (!spells[newIndex])
            {
                return;
            }
            selectedSpellIndex = newIndex;
            activeSpell = spells[selectedSpellIndex];
        }
    }

    public bool TrySpendCoins(int price)
    {
        if (coins < price)
        {
            return false;
        }
        coins -= price;
        return true;
    }

}
