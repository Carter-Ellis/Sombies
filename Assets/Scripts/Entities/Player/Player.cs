using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : NetworkBehaviour
{
    [Header("Name")]
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
    [SerializeField] private TextMeshProUGUI nameTagText;

    public NetworkVariable<int> _netActiveSpellID = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Inventory")]
    [SerializeField] private List<Item> inventory = new List<Item>();
    [SerializeField] private NetworkList<int> _netInventory;
    [SerializeField] private int maxInventorySlots = 3;
    [SerializeField] private int selectedItemIndex = 0;

    [Header("Magic")]
    public Spell activeSpell;
    public Transform firepoint;
    [SerializeField] private List<Spell> spells = new List<Spell>();
    [SerializeField] private int activeSpellIndex = 0;
    [SerializeField] private int maxSpellSlots = 2;

    public int ActiveSpellIndex
    {
        get => activeSpellIndex;
        set => activeSpellIndex = Mathf.Clamp(value, 0, maxSpellSlots - 1);
    }

    [Header("Interaction")]
    private PurchaseSystem nearbyPurchaseSystem = null;

    [Header("Melee Attack (Knife)")]
    [SerializeField] private int meleeDamage = 150;
    [SerializeField] private float meleeRange = 1.5f;
    [SerializeField] private float meleeRadius = 0.5f;
    [SerializeField] private float meleeKnockbackForce = 15f;
    [SerializeField] private float meleeKnockbackDuration = 0.2f;
    [SerializeField] private float meleeCooldown = 0.8f;
    private float lastMeleeTime;
    [SerializeField] private GameObject meleeVisual;

    private ReviveController _revive;
    private Player nearbyDownedPlayer = null;
    private Player revivingTarget = null;
    private PlayerStats _playerStats;

    private void Awake()
    {
        _revive = GetComponent<ReviveController>();
        _playerStats = GetComponent<PlayerStats>();
        _netInventory = new NetworkList<int>();

        for (int i = 0; i < maxInventorySlots; i++)
        {
            inventory.Add(null);
        }

        for (int i = 0; i < maxSpellSlots; i++)
        {
            spells.Add(null);
        }
    }

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        playerName.OnValueChanged += OnNameChanged;

        nameTagText.text = playerName.Value.ToString();

        if (IsServer)
        {
            _netInventory.Clear();
            for (int i = 0; i < maxInventorySlots; i++)
            {
                _netInventory.Add(-1);
            }
        }

        _netInventory.OnListChanged += OnInventoryChanged;

        if (IsOwner)
        {
            UIManager.Instance.InitializeInventoryUI(maxInventorySlots);
            UIManager.Instance.RefreshInventory(inventory, selectedItemIndex);
            UpdateInventoryUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        _netInventory.OnListChanged -= OnInventoryChanged;
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

    private void OnNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        if (nameTagText != null)
        {
            nameTagText.text = newVal.ToString();
        }
    }

    public void OnDeathTriggered()
    {
        if (!IsServer) return;

        if (_revive != null)
        {
            _revive.GoDown();
        }
        else
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    public void AddSpell(Spell spell)
    {
        if (!IsServer) return;

        int openSlot = FindOpenSpellSlot();
        int slotIndex = openSlot != -1 ? openSlot : ActiveSpellIndex;

        spells[slotIndex] = spell;

        activeSpell = spells[slotIndex];
        ActiveSpellIndex = slotIndex;

        _netActiveSpellID.Value = activeSpell.spellID;

        GrantSpellClientRpc(spell.spellID, slotIndex);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void GrantSpellClientRpc(int spellID, int slotIndex)
    {
        if (!IsOwner) return;

        Spell unlockedSpell = SpellDatabase.Instance.GetSpellByID(spellID);

        if (unlockedSpell != null)
        {
            spells[slotIndex] = unlockedSpell;
            activeSpell = spells[slotIndex];
            ActiveSpellIndex = slotIndex;
            UpdateHUDWithActiveSpell();
        }
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

    [Rpc(SendTo.Server)]
    private void RequestPickupServerRpc(ulong itemNetId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj))
        {
            float distance = Vector3.Distance(transform.position, netObj.transform.position);

            if (distance > 3.0f)
            {
                return;
            }

            Item worldItem = netObj.GetComponent<Item>();
            if (worldItem != null)
            {
                for (int i = 0; i < maxInventorySlots; i++)
                {
                    if (_netInventory[i] == -1)
                    {
                        _netInventory[i] = worldItem.itemID;
                        netObj.Despawn();
                        break;
                    }
                }
            }
        }
    }

    private void OnInventoryChanged(NetworkListEvent<int> changeEvent)
    {
        int index = changeEvent.Index;
        int newItemID = changeEvent.Value;

        if (newItemID == -1)
        {
            inventory[index] = null;
        }
        else
        {
            inventory[index] = ItemDatabase.Instance.GetItemByID(newItemID);
        }

        UpdateInventoryUI();
    }

    public void TryUseSelectedItem()
    {
        if (!IsOwner) return;

        if (_revive.IsDownedSync.Value) return;

        RequestUseItemServerRpc(selectedItemIndex);
    }

    [Rpc(SendTo.Server)]
    private void RequestUseItemServerRpc(int index)
    {
        if (_netInventory[index] == -1) return;

        Item itemToUse = ItemDatabase.Instance.GetItemByID(_netInventory[index]);

        if (itemToUse != null)
        {
            itemToUse.Use(_playerStats);
        }

        _netInventory[index] = -1;
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
            UpdateInventoryUI();
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
    public void RequestPurchaseServerRpc(ulong purchaseSystemId, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Entity buyer = client.PlayerObject.GetComponent<Entity>();

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(purchaseSystemId, out NetworkObject netObj))
            {
                PurchaseSystem shop = netObj.GetComponent<PurchaseSystem>();
                if (shop != null && buyer != null)
                {
                    shop.AttemptPurchase(buyer);
                }
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
            ActiveSpellIndex = newIndex;
            activeSpell = spells[ActiveSpellIndex];

            if (IsOwner)
            {
                UpdateHUDWithActiveSpell();
            }

            UpdateSelectedSpellServerRpc(activeSpell.spellID);
        }
    }

    private void UpdateHUDWithActiveSpell()
    {
        return;
    }

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (_revive.IsDownedSync.Value || !IsOwner) return;

        if (context.performed && Time.time >= lastMeleeTime + meleeCooldown)
        {
            lastMeleeTime = Time.time;
            StartCoroutine(ShowMeleeVisual());

            Vector2 direction = firepoint.right;
            Vector2 attackPoint = (Vector2)transform.position + direction * meleeRange;
            Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPoint, meleeRadius);

            ulong? hitEnemyId = null;

            foreach (Collider2D hitCollider in hitObjects)
            {
                Enemy enemy = hitCollider.GetComponent<Enemy>();
                if (enemy != null)
                {
                    hitEnemyId = enemy.NetworkObjectId;
                    break;
                }
            }

            if (hitEnemyId.HasValue)
            {
                PerformMeleeServerRpc(hitEnemyId.Value);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PerformMeleeServerRpc(ulong enemyId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyId, out var netObj))
        {
            Enemy enemy = netObj.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(meleeDamage, _playerStats);
                Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                enemy.ApplyKnockback(knockbackDir * meleeKnockbackForce, meleeKnockbackDuration);
            }
        }
        ShowMeleeVisualClientRpc();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowMeleeVisualClientRpc()
    {
        if (IsOwner) return;
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
        if (spellIndex < 0 || spellIndex >= spells.Count || spells[spellIndex] == null) return;

        Spell spellToCast = spells[spellIndex];

        if (_playerStats.Mana < spellToCast.ManaCost) return;

        _playerStats.Mana -= spellToCast.ManaCost;
        spellToCast.Cast(_playerStats);
    }

    [Rpc(SendTo.Server)]
    public void UpdateSelectedSpellServerRpc(int spellID)
    {
        _netActiveSpellID.Value = spellID;
    }

    private void UpdateInventoryUI()
    {
        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.RefreshInventory(inventory, selectedItemIndex);
        }
    }
}