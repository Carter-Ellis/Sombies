using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;


public class Player : Entity
{

    [Header("Network State")]
    public NetworkVariable<int> SyncCoins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> SyncMana = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> SyncActiveSpellID = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    public NetworkVariable<bool> isHidden = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    public int SelectedSpellIndex => selectedSpellIndex;

    public int Mana
    {
        get => _mana;
        set
        {
            _mana = Mathf.Clamp(value, 0, _maxMana);

            if (IsServer)
            {
                SyncMana.Value = _mana;
                manaTxt.text = $"Mana: {_mana}/{_maxMana}";
            }
        }
    }

    [Header("Currency Components")]
    [SerializeField] private int coins = 0;

    public int Coins
    {
        get => coins;
        set
        {
            coins = value;
            
            if (IsServer)
            {
                SyncCoins.Value = coins;
                coinsTxt.text = $"Coins: {coins}";
            }
        }
    }

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

    [Header("Temporary UI Elements")]
    [SerializeField] private TextMeshProUGUI manaTxt;
    [SerializeField] private TextMeshProUGUI coinsTxt;
    [SerializeField] private TextMeshProUGUI healthTxt;

    protected override void Awake()
    {
        base.Awake();
        healthTxt = GameObject.FindWithTag("HealthTxt").GetComponent<TextMeshProUGUI>();
        manaTxt = GameObject.FindWithTag("ManaTxt").GetComponent<TextMeshProUGUI>();
        coinsTxt = GameObject.FindWithTag("CoinsTxt").GetComponent<TextMeshProUGUI>();
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
        
        Mana = _maxMana;
    }
    public override void OnNetworkSpawn()
    {
        // 1. Move UI discovery here to be safe
        if (IsOwner)
        {
            healthTxt = GameObject.FindWithTag("HealthTxt")?.GetComponent<TextMeshProUGUI>();
            manaTxt = GameObject.FindWithTag("ManaTxt")?.GetComponent<TextMeshProUGUI>();
            coinsTxt = GameObject.FindWithTag("CoinsTxt")?.GetComponent<TextMeshProUGUI>();
        }

        base.OnNetworkSpawn(); // This triggers the OnHealthChanged call in Entity

        if (!IsServer)
        {
            SyncCoins.OnValueChanged += (oldVal, newVal) => coins = newVal;
            SyncMana.OnValueChanged += (oldVal, newVal) => _mana = newVal;

            coins = SyncCoins.Value;
            _mana = SyncMana.Value;
        }

        // 2. FORCIBLY refresh the UI once we are sure we have the reference
        if (IsOwner)
        {
            UpdateHealthUI(_netHealth.Value);
            coinsTxt.text = $"Coins: {SyncCoins.Value}";
            manaTxt.text = $"Mana: {SyncMana.Value}/{_maxMana}";
        }
    }

    protected override void OnHealthChanged(int previousValue, int newValue)
    {
        base.OnHealthChanged(previousValue, newValue);

        if (IsOwner)
        {
            UpdateHealthUI(newValue);
        }
    }

    private void UpdateHealthUI(int currentHealth)
    {
        if (healthTxt != null)
        {
            healthTxt.text = $"Health: {currentHealth}/{MaxHealth}";
        }
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
        int slotToUse = openSlot != -1 ? openSlot : selectedSpellIndex;

        // 1. Add it to the Server's list
        spells[slotToUse] = spell;

        // 2. Make it the active spell locally for the server
        activeSpell = spells[selectedSpellIndex];

        // 3. Tell the clients to add it to THEIR lists
        GrantSpellClientRpc(spell.spellID, slotToUse);
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
        if (!IsOwner) return;

        Item hitItem = collision.GetComponent<Item>();

        if (hitItem != null)
        {
            var itemNetObj = hitItem.GetComponent<NetworkObject>();
            RequestPickupServerRpc(itemNetObj.NetworkObjectId);
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
            if (otherRevive != null && otherRevive.IsDownedSync.Value)
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

        if (_revive.IsDownedSync.Value) return;

        if (context.started)
        {
            if (nearbyDownedPlayer != null)
            {
                revivingTarget = nearbyDownedPlayer;
                nearbyDownedPlayer.GetComponent<ReviveController>().StartBeingRevivedServerRpc(NetworkObjectId);
            }
            else if (nearbyPurchaseSystem != null)
            {
                RequestPurchaseServerRpc(nearbyPurchaseSystem.NetworkObjectId);
            }
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(ulong purchaseSystemId)
    {
        // Find the network object the player is looking at
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(purchaseSystemId, out NetworkObject netObj))
        {
            PurchaseSystem shop = netObj.GetComponent<PurchaseSystem>();

            if (shop != null)
            {
                // The server now attempts the purchase. 
                // This means TrySpendCoins will successfully update the NetworkVariable!
                shop.AttemptPurchase(this);
            }
        }
    }

    public void CancelMyReviveAction()
    {
        if (revivingTarget != null)
        {
            revivingTarget.GetComponent<ReviveController>().StopBeingRevivedServerRpc();
            revivingTarget = null;
        }
    }

    public void TryUseSelectedItem()
    {
        if (_revive.IsDownedSync.Value) return;

        // Ask the server to use the item
        RequestUseItemServerRpc(selectedItemIndex);
    }

    public void SwitchItem(InputAction.CallbackContext context)
    {
        if (_revive.IsDownedSync.Value) return;

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
        Coins += amount;
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
        if (_revive.IsDownedSync.Value) return;

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
            if (spells[newIndex] == null)
            {
                return;
            }
            selectedSpellIndex = newIndex;
            activeSpell = spells[selectedSpellIndex];

            UpdateSelectedSpellServerRpc(activeSpell.spellID);
        }
    }

    public bool TrySpendCoins(int price)
    {
        if (Coins < price)
        {
            return false;
        }
        Coins -= price;
        return true;
    }

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (_revive.IsDownedSync.Value || !IsOwner) return;

        // 1. Check local cooldown
        if (context.performed && Time.time >= lastMeleeTime + meleeCooldown)
        {
            lastMeleeTime = Time.time;

            // 2. Immediate local visual (for the player attacking)
            StartCoroutine(ShowMeleeVisual());

            // 3. Tell the server to actually perform the hit
            PerformMeleeServerRpc(firepoint.right);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PerformMeleeServerRpc(Vector2 direction)
    {
        // 4. Server-side Hit Detection
        Vector2 attackPoint = (Vector2)transform.position + direction * meleeRange;
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPoint, meleeRadius);

        foreach (Collider2D hitCollider in hitObjects)
        {
            Enemy enemy = hitCollider.GetComponent<Enemy>();
            if (enemy != null)
            {
                // Damage and Knockback happen on the Server
                enemy.TakeDamage(meleeDamage, this);

                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                enemy.ApplyKnockback(knockbackDir * meleeKnockbackForce, meleeKnockbackDuration);

                // Break if you only want to hit one enemy, or remove to hit all in radius
                break;
            }
        }

        // 5. Tell other clients to show the visual (so they see you swing)
        ShowMeleeVisualClientRpc();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowMeleeVisualClientRpc()
    {
        if (IsOwner) return;    
        // This plays the animation for everyone EXCEPT the person who already played it locally
        StartCoroutine(ShowMeleeVisual());
    }

    private IEnumerator ShowMeleeVisual()
    {
        if (meleeVisual != null)
        {
            meleeVisual.SetActive(true);
            yield return new WaitForSeconds(0.1f);
            meleeVisual.SetActive(false);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCastSpellServerRpc(int spellIndex)
    {

        print("STEP 1");
        // 1. Server grabs the spell from ITS own list using the index
        if (spellIndex < 0 || spellIndex >= spells.Count || spells[spellIndex] == null) return;

        Spell spellToCast = spells[spellIndex];

        // 2. Server validates resources
        if (Mana < spellToCast.ManaCost) return;

        // 3. Server spends the mana (this automatically syncs back to the client)
        Mana -= spellToCast.ManaCost;
        print("STEP 2");
        // 4. Server executes your ProjectileSpell.Cast() logic
        spellToCast.Cast(this);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void GrantSpellClientRpc(int spellID, int slotIndex)
    {
        if (IsServer) return; // The server already added it above!

        // The client looks up the spell in the database
        Spell unlockedSpell = SpellDatabase.Instance.GetSpellByID(spellID);

        if (unlockedSpell != null)
        {
            // The client adds it to their local list so SwitchSpell will work
            spells[slotIndex] = unlockedSpell;
            Debug.Log($"Client added {unlockedSpell.Name} to slot {slotIndex}");
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdateSelectedSpellServerRpc(int spellID)
    {
        // The server updates the NetworkVariable, which then syncs to everyone
        SyncActiveSpellID.Value = spellID;
    }


    [Rpc(SendTo.Server)]
    private void RequestPickupServerRpc(ulong itemNetId, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj))
        {
            Item worldItem = netObj.GetComponent<Item>();
            if (worldItem != null)
            {
                int id = worldItem.itemID;
                Item prefab = ItemDatabase.Instance.GetItemByID(id);

                if (prefab != null)
                {
                    AddItem(prefab);

                    var targetParams = RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
                    SyncPickupClientRpc(id, targetParams);

                    netObj.Despawn();
                }
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncPickupClientRpc(int itemID, RpcParams rpcParams = default)
    {
        if (IsServer) return;

        // Look up the item prefab in your new database
        Item itemPrefab = ItemDatabase.Instance.GetItemByID(itemID);

        if (itemPrefab != null)
        {
            // Add the stable prefab reference to the local inventory
            AddItem(itemPrefab);
            Debug.Log($"Client: Successfully added {itemPrefab.ItemName} to inventory via ID {itemID}");
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestUseItemServerRpc(int index)
    {
        if (inventory[index] == null) return;

        Debug.Log($"Server executing use for: {inventory[index].ItemName}");

        // 1. Execute the item logic (this will update NetworkVariables like Mana)
        inventory[index].Use(this);

        // 2. Remove it from the Server's list
        inventory[index] = null;

        // 3. Tell the Client to remove it from their list too
        RemoveItemClientRpc(index);
    }

    [Rpc(SendTo.Everyone)] // Or SendTo.Owner
    private void RemoveItemClientRpc(int index)
    {
        if (IsServer) return; // Server already did this
        inventory[index] = null;
    }

}
