using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


public class Player : MonoBehaviour
{
    [Header("Health")]
    private int _maxHealth = 100;
    [SerializeField] private int _health;

    
    public int MaxHealth
    {
        get => _maxHealth;
        set => _maxHealth = value;
    }

    public int Health
    {
        get => _health;
        set
        {
            _health = Mathf.Clamp(value, 0, MaxHealth);

            if (_health <= 0)
            {
                Die();
            }
        }
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

    [Header("Currency Components")]
    [SerializeField] private int coins = 0;

    private void Awake()
    {
        for (int i = 0; i < maxInventorySlots; i++)
        {
            inventory.Add(null);
        }
    }

    void Start()
    {
        Health = MaxHealth;
    }


    public void AddItem(Item item)
    {
        int openSlot = FindOpenInventorySlot();
        if (openSlot != -1)
        {
            inventory[openSlot] = item;
            Debug.Log("Item picked up!");
        }
        else
        {
            Debug.Log("Inventory is full!");
        }
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Item hitItem = collision.GetComponent<Item>();

        if (hitItem != null)
        {
            AddItem(hitItem);
            hitItem.gameObject.SetActive(false);
        }
    }

    public void UseItem()
    {
        if (inventory[selectedItemIndex] != null)
        {
            inventory[selectedItemIndex].Use(this);
            inventory[selectedItemIndex] = null;
        }
    }

    public void Heal(int amount)
    {
        Health = Mathf.Min(Health + amount, MaxHealth);
        Debug.Log("Healed! Current health: " + Health);
    }

    public void TakeDamage(int amount)
    {
        Health = Mathf.Max(Health - amount, 0);
        Debug.Log("Took damage! Current health: " + Health);
    }

    public void Die()
    {
        gameObject.SetActive(false);
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

}
