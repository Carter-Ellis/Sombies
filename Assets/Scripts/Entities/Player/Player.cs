using System.Collections;
using System.Collections.Generic;
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

    [Header("Melee Attack (Knife)")]
    [SerializeField] private int meleeDamage = 150; // Insta-kill early rounds!
    [SerializeField] private float meleeRange = 1.5f; // How far forward the knife reaches
    [SerializeField] private float meleeRadius = 0.5f; // How wide the hit detection is
    [SerializeField] private float meleeKnockbackForce = 15f;
    [SerializeField] private float meleeKnockbackDuration = 0.2f;
    [SerializeField] private float meleeCooldown = 0.8f;
    private float lastMeleeTime;
    [SerializeField] private GameObject meleeVisual;

    private ReviveController _revive;
    private Player nearbyDownedPlayer = null;
    private Player revivingTarget = null;

    protected override void Awake()
    {
        base.Awake();
        _movement = GetComponent<PlayerMovement>();

        _revive = GetComponent<ReviveController>();

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

    public override void Die()
    {
        if (_revive != null)
        {
            _revive.GoDown();
        }
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

        Player other = collision.GetComponent<Player>();
        if (other != null && other != this)
        {
            ReviveController otherRevive = other.GetComponent<ReviveController>();
            if (otherRevive != null && otherRevive.IsDowned)
            {
                nearbyDownedPlayer = other;
            }
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

        Player other = collision.GetComponent<Player>();
        if (other != null && other == nearbyDownedPlayer)
        {
            CancelMyReviveAction();
            nearbyDownedPlayer = null;
        }

    }

    public void OnInteract(InputAction.CallbackContext context)
    {

        if (_revive.IsDowned) return;

        if (context.started)
        {
            if (nearbyDownedPlayer != null)
            {
                revivingTarget = nearbyDownedPlayer;
                nearbyDownedPlayer.GetComponent<ReviveController>().StartBeingRevived(this);
            }
            else if (nearbyPurchaseSystem != null)
            {
                nearbyPurchaseSystem.AttemptPurchase(this);
            }
        }
    }

    public void CancelMyReviveAction()
    {
        if (revivingTarget != null)
        {
            revivingTarget.GetComponent<ReviveController>().StopBeingRevived();
            revivingTarget = null;
        }
    }

    public void UseItem()
    {
        if (_revive.IsDowned) return;

        if (inventory[selectedItemIndex] != null)
        {
            inventory[selectedItemIndex].Use(this);
            Destroy(inventory[selectedItemIndex].gameObject);
            inventory[selectedItemIndex] = null;
        }
    }

    public void SwitchItem(InputAction.CallbackContext context)
    {
        if (_revive.IsDowned) return;

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

    public void AddMana(int amount)
    {
        Mana += amount;
    }

    public void ReloadScene()
    {
        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(buildIndex);
    }


    public void SwitchSpell(InputAction.CallbackContext context)
    {
        if (_revive.IsDowned) return;

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
    public void OnMelee(InputAction.CallbackContext context)
    {

        if (_revive.IsDowned) return;

        // Only trigger on the initial button press, and check the cooldown
        if (context.performed && Time.time >= lastMeleeTime + meleeCooldown)
        {
            StartCoroutine(ShowMeleeVisual());
            PerformMeleeAttack();
            lastMeleeTime = Time.time;
        }
    }

    private void PerformMeleeAttack()
    {
        // 1. Figure out which way the player is looking (towards the mouse)
        Vector2 direction = firepoint.right;

        // 2. Calculate the exact point in space where the knife hits
        Vector2 attackPoint = (Vector2)transform.position + direction * meleeRange;

        // 3. Draw a circle at that point and grab everything inside it
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPoint, meleeRadius);

        foreach (Collider2D hitCollider in hitObjects)
        {
            Enemy enemy = hitCollider.GetComponent<Enemy>();
            if (enemy != null)
            {
                // Deal Damage
                enemy.TakeDamage(meleeDamage, this);

                // Apply Knockback (pushing them away from the player)
                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                enemy.ApplyKnockback(knockbackDir * meleeKnockbackForce, meleeKnockbackDuration);

                break;
            }
        }
    }

    private IEnumerator ShowMeleeVisual()
    {
        meleeVisual.SetActive(true);
        yield return new WaitForSeconds(0.1f); // Flash it for a split second
        meleeVisual.SetActive(false);
    }

}
