using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private float itemDropOffset = 2f;

    [Header("Throw Settings")]
    [SerializeField] private float minThrowForce = 4f;
    [SerializeField] private float maxThrowForce = 20f;
    [SerializeField] private float maxThrowChargeTime = 1.0f;
    [SerializeField] private LineRenderer throwTrajectoryLine;

    private bool _isChargingThrow = false;
    private float _throwChargeStartTime = 0f;

    [Header("Magic")]
    public Spell activeSpell;
    public Transform firepoint;
    [SerializeField] private List<Spell> spells = new List<Spell>();
    [SerializeField] private int activeSpellIndex = 0;
    [SerializeField] private int maxSpellSlots = 2;
    [SerializeField] private float spellCastCheckDistance = 1.2f;

    public int ActiveSpellIndex
    {
        get => activeSpellIndex;
        set => activeSpellIndex = Mathf.Clamp(value, 0, maxSpellSlots - 1);
    }

    [Header("Interaction")]
    private System.Collections.Generic.List<PurchaseSystem> nearbyPurchaseSystems = new System.Collections.Generic.List<PurchaseSystem>();
    private PurchaseSystem lastActivePurchaseTarget = null;

    private void Update()
    {
        if (!IsOwner) return;

        if (nearbyPurchaseSystems.Count > 0)
        {
            RefreshPurchaseSystemUI();
        }

        if (_isChargingThrow)
        {
            UpdateThrowTrajectory();
        }
    }

    public PurchaseSystem GetActivePurchaseTarget()
    {
        for (int i = nearbyPurchaseSystems.Count - 1; i >= 0; i--)
        {
            PurchaseSystem system = nearbyPurchaseSystems[i];
            if (system == null || system.gameObject == null || !system.gameObject.activeInHierarchy)
            {
                nearbyPurchaseSystems.RemoveAt(i);
            }
            else if (system.TryGetComponent<NetworkObject>(out var netObj) && !netObj.IsSpawned)
            {
                nearbyPurchaseSystems.RemoveAt(i);
            }
        }

        if (nearbyPurchaseSystems.Count == 0) return null;

        // Priority 1: Pick a spawned SpellPurchase over a MysteryBox
        PurchaseSystem spellPurchase = nearbyPurchaseSystems.Find(p => p is SpellPurchase && p.gameObject.activeInHierarchy);
        if (spellPurchase != null)
        {
            return spellPurchase;
        }

        // Priority 2: Pick the closest purchase target to the player
        PurchaseSystem closest = nearbyPurchaseSystems[0];
        float minDistance = Vector3.Distance(transform.position, closest.transform.position);

        for (int i = 1; i < nearbyPurchaseSystems.Count; i++)
        {
            float dist = Vector3.Distance(transform.position, nearbyPurchaseSystems[i].transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = nearbyPurchaseSystems[i];
            }
        }

        return closest;
    }

    private void RefreshPurchaseSystemUI()
    {
        PurchaseSystem activeTarget = GetActivePurchaseTarget();

        if (activeTarget != lastActivePurchaseTarget)
        {
            if (lastActivePurchaseTarget != null)
            {
                lastActivePurchaseTarget.HidePrice();
            }
            lastActivePurchaseTarget = activeTarget;
        }

        if (activeTarget != null)
        {
            activeTarget.DisplayPrice();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsOwner || (_revive != null && _revive.IsDownedSync.Value)) return;

        Item hitItem = collision.GetComponent<Item>();

        if (hitItem != null && hitItem.CanBePickedUpBy(OwnerClientId))
        {
            var itemNetObj = hitItem.GetComponent<NetworkObject>();
            RequestPickupServerRpc(itemNetObj.NetworkObjectId);
        }

        PurchaseSystem shop = collision.GetComponent<PurchaseSystem>();

        if (shop != null && !nearbyPurchaseSystems.Contains(shop))
        {
            nearbyPurchaseSystems.Add(shop);
            RefreshPurchaseSystemUI();
        }

        Player other = collision.GetComponent<Player>();
        if (other != null && other != this)
        {
            ReviveController otherRevive = other.GetComponent<ReviveController>();
            if (otherRevive != null && otherRevive.IsDownedSync.Value)
            {
                nearbyDownedPlayer = other;
                nearbyDownedPlayer.DisplayReviveText();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        PurchaseSystem shop = collision.GetComponent<PurchaseSystem>();
        if (shop != null && nearbyPurchaseSystems.Contains(shop))
        {
            shop.HidePrice();
            nearbyPurchaseSystems.Remove(shop);
            RefreshPurchaseSystemUI();
        }

        Player other = collision.GetComponent<Player>();
        if (other != null && other == nearbyDownedPlayer)
        {
            CancelMyReviveAction();
            nearbyDownedPlayer.HideReviveText();
            nearbyDownedPlayer = null;
        }
    }

    [Header("Revive")]
    [SerializeField] private TextMeshPro reviveTxt;

    [Header("Melee Attack (Knife)")]
    [SerializeField] private int meleeDamage = 150;
    [SerializeField] private float meleeRange = 1.5f;
    [SerializeField] private float meleeRadius = 0.5f;
    [SerializeField] private float meleeKnockbackForce = 15f;
    [SerializeField] private float meleeKnockbackDuration = 0.2f;
    [SerializeField] private float meleeCooldown = 0.8f;
    private float lastMeleeTime;

    [SerializeField] private GameObject meleeVisual;
    [SerializeField] private Animator meleeAnimator;
    [SerializeField] private float meleeAnimationDuration = 0.3f;

    private ReviveController _revive;
    private Player nearbyDownedPlayer = null;
    private Player revivingTarget = null;
    private PlayerStats _playerStats;

    [Header("Visuals")]
    [SerializeField] private Transform spriteTransform;
    // ---> NEW CODE: Added reference for the Sprite's Animator
    [SerializeField] private Animator spriteAnimator;
    public Transform SpriteTransform => spriteTransform;

    private void Awake()
    {
        _revive = GetComponent<ReviveController>();
        _playerStats = GetComponent<PlayerStats>();
        _netInventory = new NetworkList<int>();

        reviveTxt = GetComponentInChildren<TextMeshPro>();

        HideReviveText();

        for (int i = 0; i < maxInventorySlots; i++)
        {
            inventory.Add(null);
        }

        for (int i = 0; i < maxSpellSlots; i++)
        {
            spells.Add(null);
        }

        if (throwTrajectoryLine == null)
        {
            GameObject lineObj = new GameObject("ThrowTrajectoryLine");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            throwTrajectoryLine = lineObj.AddComponent<LineRenderer>();
            throwTrajectoryLine.startWidth = 0.08f;
            throwTrajectoryLine.endWidth = 0.02f;
            throwTrajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            throwTrajectoryLine.startColor = new Color(1f, 0.85f, 0.3f, 0.8f);
            throwTrajectoryLine.endColor = new Color(1f, 0.35f, 0.1f, 0.1f);
            throwTrajectoryLine.sortingOrder = 10;
            throwTrajectoryLine.enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        playerName.OnValueChanged += OnNameChanged;

        if (nameTagText != null)
        {
            nameTagText.text = playerName.Value.ToString();
        }

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
            if (UIManager.Instance != null)
            {
                UIManager.Instance.InitializeInventoryUI(maxInventorySlots);
                UIManager.Instance.RefreshInventory(inventory, selectedItemIndex);
                UIManager.Instance.InitializeSpellUI(maxSpellSlots);
                UIManager.Instance.RefreshSpells(spells, ActiveSpellIndex);
            }
            UpdateInventoryUI();
            UpdateHUDWithActiveSpell();
        }

        if (!IsOwner)
        {
            if (firepoint != null)
            {
                firepoint.gameObject.SetActive(false);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        _netInventory.OnListChanged -= OnInventoryChanged;
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
                        if (FMODEvents.instance != null)
                        {
                            Audio.PlayNetworkedSFX(FMODEvents.instance.itemPickup, transform.position);
                        }
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

    public void OnDropItem(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.started)
        {
            if (HasItemInSlot(selectedItemIndex))
            {
                StartThrowCharge();
            }
        }
        else if (context.canceled)
        {
            if (_isChargingThrow)
            {
                ReleaseThrowCharge();
            }
        }
        else if (context.performed && !_isChargingThrow && HasItemInSlot(selectedItemIndex))
        {
            StartThrowCharge();
        }
    }

    private bool HasItemInSlot(int index)
    {
        if (index < 0 || index >= maxInventorySlots) return false;

        if (_netInventory != null && index < _netInventory.Count)
        {
            return _netInventory[index] != -1;
        }
        return inventory != null && index < inventory.Count && inventory[index] != null;
    }

    private void StartThrowCharge()
    {
        _isChargingThrow = true;
        _throwChargeStartTime = Time.time;
        if (throwTrajectoryLine != null)
        {
            throwTrajectoryLine.enabled = true;
        }
    }

    private void ReleaseThrowCharge()
    {
        if (!_isChargingThrow) return;

        float chargeDuration = Time.time - _throwChargeStartTime;
        float ratio = Mathf.Clamp01(chargeDuration / maxThrowChargeTime);
        float calculatedForce = Mathf.Lerp(minThrowForce, maxThrowForce, ratio);

        CancelThrowCharge();

        Vector3 launchDir = firepoint != null ? firepoint.right : transform.right;
        RequestDropItemRpc(selectedItemIndex, transform.position, launchDir, calculatedForce);
    }

    public void CancelThrowCharge()
    {
        _isChargingThrow = false;
        if (throwTrajectoryLine != null)
        {
            throwTrajectoryLine.enabled = false;
        }
    }

    private void UpdateThrowTrajectory()
    {
        if (!_isChargingThrow)
        {
            if (throwTrajectoryLine != null) throwTrajectoryLine.enabled = false;
            return;
        }

        Transform origin = firepoint != null ? firepoint : transform;
        float chargeDuration = Time.time - _throwChargeStartTime;
        float ratio = Mathf.Clamp01(chargeDuration / maxThrowChargeTime);
        float currentForce = Mathf.Lerp(minThrowForce, maxThrowForce, ratio);

        if (throwTrajectoryLine != null)
        {
            throwTrajectoryLine.enabled = true;
            int points = 18;
            throwTrajectoryLine.positionCount = points;

            Vector3 startPos = origin.position;
            Vector2 direction = origin.right;
            Vector3 currentPos = startPos;
            Vector2 currentVel = direction * currentForce;
            float timeStep = 0.035f;
            float linearDrag = 2.0f;

            for (int i = 0; i < points; i++)
            {
                throwTrajectoryLine.SetPosition(i, currentPos);
                currentPos += (Vector3)currentVel * timeStep;
                currentVel *= Mathf.Clamp01(1f - linearDrag * timeStep);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestDropItemRpc(int itemIndex, Vector3 clientPos, Vector3 clientDir, float force)
    {
        if (itemIndex < 0 || itemIndex >= maxInventorySlots) return;

        int itemID = _netInventory[itemIndex];

        if (itemID == -1) return;

        Item itemPrefab = ItemDatabase.Instance.GetItemByID(itemID);

        if (itemPrefab != null)
        {
            LayerMask wallLayer = LayerMask.GetMask("Wall");

            RaycastHit2D hit = Physics2D.Raycast(clientPos, clientDir, itemDropOffset, wallLayer);

            if (hit.collider != null)
            {
                return;
            }

            Vector3 dropOffset = clientDir * itemDropOffset;
            Vector3 spawnPos = clientPos + dropOffset;

            Item droppedItem = Instantiate(itemPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = droppedItem.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();
                droppedItem.DropperClientId.Value = OwnerClientId;
            }
            else
            {
                Debug.LogError($"Item Prefab '{itemPrefab.name}' is missing a NetworkObject component!");
            }

            Rigidbody2D itemRb = droppedItem.GetComponent<Rigidbody2D>();
            if (itemRb != null)
            {
                itemRb.AddForce(clientDir * force, ForceMode2D.Impulse);
                PlayCastAnimationClientRpc();
                if (FMODEvents.instance != null)
                {
                    Audio.PlayNetworkedSFX(FMODEvents.instance.itemThrow, transform.position);
                }
            }
            else
            {
                Debug.LogWarning($"Dropped item '{droppedItem.name}' does not have a Rigidbody2D component. It will not be thrown.");
            }

            _netInventory[itemIndex] = -1;

        }
        else
        {
            Debug.LogError($"Item with ID {itemID} not found in ItemDatabase!");
        }
    }

    public void TryUseSelectedItem(InputAction.CallbackContext context)
    {
        if (!context.performed || !IsOwner) return;

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

    public void CycleItemInput(InputAction.CallbackContext context)
    {

        if (context.performed)
        {
            float value = context.ReadValue<float>();
            CycleSelectedItem((int)value);
        }
    }

    public void SelectSpecificItemInput(InputAction.CallbackContext context)
    {

        if (context.performed)
        {
            float value = context.ReadValue<float>();
            int index = Mathf.RoundToInt(value);
            ChangeSelectedItem(index);
        }
    }

    private void CycleSelectedItem(int direction)
    {
        CancelThrowCharge();
        selectedItemIndex = (selectedItemIndex + direction + maxInventorySlots) % maxInventorySlots;
        UpdateInventoryUI();
    }

    private void ChangeSelectedItem(int newIndex)
    {
        if (newIndex >= 0 && newIndex < maxInventorySlots)
        {
            CancelThrowCharge();
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
                revivingTarget.HideReviveText();
                nearbyDownedPlayer.GetComponent<ReviveController>().StartBeingRevivedServerRpc(NetworkObjectId);
            }
            else
            {
                PurchaseSystem activeTarget = GetActivePurchaseTarget();
                if (activeTarget != null)
                {
                    RequestPurchaseServerRpc(activeTarget.NetworkObjectId);
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
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
                    print($"Client {clientId} is attempting to purchase from {shop.name}");
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
        if (_revive != null && _revive.IsDownedSync.Value) return;

        if (context.performed)
        {
            float value = context.ReadValue<float>();
            int index = Mathf.RoundToInt(value);
            ChangeSelectedSpell(index);
        }
    }

    public void CycleSpellInput(InputAction.CallbackContext context)
    {
        if (_revive != null && _revive.IsDownedSync.Value) return;

        if (context.performed)
        {
            float value = context.ReadValue<float>();
            CycleSelectedSpell((int)value);
        }
    }

    private void CycleSelectedSpell(int direction)
    {
        int nextIndex = ActiveSpellIndex;

        for (int i = 0; i < maxSpellSlots; i++)
        {
            nextIndex = (nextIndex + direction + maxSpellSlots) % maxSpellSlots;

            if (spells[nextIndex] != null)
            {
                ChangeSelectedSpell(nextIndex);
                break;
            }
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
        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.RefreshSpells(spells, ActiveSpellIndex);
        }
    }

    public void OnMelee(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed && Time.time >= lastMeleeTime + meleeCooldown)
        {
            Audio.PlayNetworkedSFX(FMODEvents.instance.meleeAttack, transform.position);
            lastMeleeTime = Time.time;
            StartCoroutine(ShowMeleeVisual());

            Vector2 direction = firepoint.right;
            Vector2 attackPoint = (Vector2)transform.position + direction * meleeRange;
            Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPoint, meleeRadius);

            ulong hitEnemyId = 0;

            foreach (Collider2D hitCollider in hitObjects)
            {
                Enemy enemy = hitCollider.GetComponent<Enemy>();
                if (enemy != null)
                {
                    hitEnemyId = enemy.NetworkObjectId;

                    Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
                    enemy.ApplyKnockback(knockbackDir * meleeKnockbackForce, meleeKnockbackDuration);

                    break;
                }
            }

            PerformMeleeServerRpc(hitEnemyId);
        }
    }

    [Rpc(SendTo.Server)]
    private void PerformMeleeServerRpc(ulong enemyId)
    {
        if (enemyId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyId, out var netObj))
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
        PlayCastAnimationClientRpc();
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

            if (meleeAnimator != null)
            {
                meleeAnimator.SetTrigger("Melee");
            }

            yield return new WaitForSeconds(meleeAnimationDuration);
            meleeVisual.SetActive(false);
        }
    }

    public bool IsWallBlockingCast()
    {
        if (firepoint == null) return false;

        LayerMask wallLayer = LayerMask.GetMask("Wall");
        Vector3 origin = transform.position;
        Vector3 direction = firepoint.right;

        float distance = Vector3.Distance(origin, firepoint.position);
        if (distance < 0.5f) distance = spellCastCheckDistance;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, wallLayer);
        return hit.collider != null;
    }

    private float _lastServerSpellCastTime = 0f;

    [Rpc(SendTo.Server)]
    public void RequestCastSpellServerRpc(int spellIndex)
    {
        if (spellIndex < 0 || spellIndex >= spells.Count || spells[spellIndex] == null) return;

        if (IsWallBlockingCast()) return;

        Spell spellToCast = spells[spellIndex];

        if (Time.time < _lastServerSpellCastTime + spellToCast.Cooldown) return;

        if (_playerStats.Mana < spellToCast.ManaCost) return;

        _lastServerSpellCastTime = Time.time;

        _playerStats.Mana -= spellToCast.ManaCost;
        spellToCast.Cast(_playerStats);

        PlayCastAnimationClientRpc();
    }

    private PulseProj _activeChargingPulse;

    [Rpc(SendTo.Server)]
    public void RequestStartChargingSpellServerRpc(int spellIndex)
    {
        if (spellIndex < 0 || spellIndex >= spells.Count || spells[spellIndex] == null) return;

        if (IsWallBlockingCast()) return;

        Spell spellToCast = spells[spellIndex];

        if (Time.time < _lastServerSpellCastTime + spellToCast.Cooldown) return;
        if (_playerStats.Mana < spellToCast.ManaCost) return;

        _lastServerSpellCastTime = Time.time;
        _playerStats.Mana -= spellToCast.ManaCost;

        if (spellToCast is ProjectileSpell projSpell)
        {
            GameObject ball = Instantiate(projSpell.ProjectilePrefab, firepoint.position, firepoint.rotation);

            if (ball.TryGetComponent(out PulseProj pulse))
            {
                pulse.Initialize(_playerStats, spellToCast.Damage, projSpell.LaunchForce);
                _activeChargingPulse = pulse;
            }
            else if (ball.TryGetComponent(out Projectile proj))
            {
                proj.Initialize(_playerStats, spellToCast.Damage, projSpell.LaunchForce);
            }

            if (ball.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }
        }
        else
        {
            spellToCast.Cast(_playerStats);
        }

        PlayCastAnimationClientRpc();
    }

    [Rpc(SendTo.Server)]
    public void RequestReleaseChargingSpellServerRpc()
    {
        if (_activeChargingPulse != null)
        {
            _activeChargingPulse.LaunchServerRpc();
            _activeChargingPulse = null;
        }
        else
        {
            // Failsafe: find any unlaunched PulseProj owned by this player
            PulseProj[] activePulses = Object.FindObjectsByType<PulseProj>(FindObjectsInactive.Exclude);
            foreach (var pulse in activePulses)
            {
                if (pulse.GetOwnerStats() == _playerStats)
                {
                    pulse.LaunchServerRpc();
                }
            }
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayCastAnimationClientRpc()
    {
        if (spriteAnimator != null)
        {
            spriteAnimator.Play("Friz_Cast", -1, 0f);
        }
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

    public void DisplayReviveText()
    {
        if (reviveTxt != null)
        {
            reviveTxt.gameObject.SetActive(true);
        }
    }

    public void HideReviveText()
    {
        if (reviveTxt != null)
        {
            reviveTxt.gameObject.SetActive(false);
        }
    }
}