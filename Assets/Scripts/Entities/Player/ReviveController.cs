using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ReviveController : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsDownedSync = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsDeadSync = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetDownedTimer = new NetworkVariable<float>(30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Visual Customization")]
    private SpriteRenderer[] playerSrs;
    private Color[] originalColors;
    [SerializeField] private Color downedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color downedExpiredColor = new Color(0.5f, 0.05f, 0.05f, 1f);
    [SerializeField] private float downedBleedoutDuration = 30f;
    public float DownedBleedoutDuration => downedBleedoutDuration > 0f ? downedBleedoutDuration : 30f;
    private Coroutine bleedoutCoroutine;

    [Header("Physics")]
    [SerializeField] private string defaultLayerName = "Player";
    [SerializeField] private string downedLayerName = "DownedPlayer";

    [SerializeField]
    protected NetworkVariable<float> _netReviveDuration = new NetworkVariable<float>(4f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] protected float _baseReviveDuration = 4f;

    public virtual float BaseReviveDuration => _baseReviveDuration;

    public virtual float ReviveDuration
    {
        get => _netReviveDuration.Value;
        set
        {
            if (!IsServer) return;
            _netReviveDuration.Value = value;
        }
    }

    [SerializeField] private int healthAfterRevive = 20;
    [SerializeField] private float crawlSpeed = 1.5f;
    [SerializeField] private float maxReviveDistance = 2.0f;

    [Header("UI Visuals")]
    [SerializeField] private Slider reviveProgressSlider;

    public bool IsDowned => IsDownedSync.Value;
    public float CrawlSpeed => crawlSpeed;

    private Coroutine reviveCoroutine;
    private PlayerStats _playerStats;
    private Rigidbody2D _rb;

    private Player currentReviver;
    private Vector2 reviveStartPosition;

    private void Awake()
    {
        _playerStats = GetComponent<PlayerStats>();
        _rb = GetComponent<Rigidbody2D>();
        playerSrs = GetPlayerSpriteRenderers();

        if (playerSrs != null && playerSrs.Length > 0)
        {
            originalColors = new Color[playerSrs.Length];
            for (int i = 0; i < playerSrs.Length; i++)
            {
                originalColors[i] = playerSrs[i].color;
            }
        }

        // Hide the revive progress slider on awake
        if (reviveProgressSlider != null)
        {
            reviveProgressSlider.gameObject.SetActive(false);
        }

    }

    private SpriteRenderer[] GetPlayerSpriteRenderers()
    {
        Player player = GetComponent<Player>();
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();

        Transform searchRoot = (player != null && player.SpriteTransform != null) ? player.SpriteTransform : transform;
        SpriteRenderer[] found = searchRoot.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (var sr in found)
        {
            if (sr == null) continue;
            string nameLower = sr.gameObject.name.ToLower();
            if (nameLower.Contains("melee")) continue;

            renderers.Add(sr);
        }

        return renderers.ToArray();
    }

    public override void OnNetworkSpawn()
    {
        IsDownedSync.OnValueChanged += OnDownedStateChanged;
        IsDeadSync.OnValueChanged += OnDeadStateChanged;

        if (IsServer)
        {
            _netReviveDuration.Value = _baseReviveDuration;
        }

        UpdatePlayerColor(IsDownedSync.Value);
        SetDeadVisuals(IsDeadSync.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDownedSync.OnValueChanged -= OnDownedStateChanged;
        IsDeadSync.OnValueChanged -= OnDeadStateChanged;
    }

    private void OnDownedStateChanged(bool previousValue, bool newValue)
    {
        UpdatePlayerColor(newValue);
    }

    private void OnDeadStateChanged(bool previousValue, bool newValue)
    {
        SetDeadVisuals(newValue);
    }

    public void SetDeadVisuals(bool isDead)
    {
        if (playerSrs != null)
        {
            for (int i = 0; i < playerSrs.Length; i++)
            {
                if (playerSrs[i] != null)
                {
                    playerSrs[i].enabled = !isDead;
                }
            }
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = !isDead;

        Player player = GetComponent<Player>();
        if (player != null && player.SpriteTransform != null)
        {
            foreach (var renderer in player.SpriteTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && !renderer.gameObject.name.ToLower().Contains("melee"))
                {
                    renderer.enabled = !isDead;
                }
            }
        }

        if (reviveProgressSlider != null && isDead)
        {
            reviveProgressSlider.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (IsDownedSync.Value && !IsDeadSync.Value)
        {
            float maxDuration = DownedBleedoutDuration;
            float timerRatio = Mathf.Clamp01(NetDownedTimer.Value / maxDuration);
            Color currentColor = Color.Lerp(downedExpiredColor, downedColor, timerRatio);
            ApplyPlayerColor(currentColor);
        }
    }

    private void UpdatePlayerColor(bool isDowned)
    {
        if (!isDowned)
        {
            if (playerSrs != null)
            {
                for (int i = 0; i < playerSrs.Length; i++)
                {
                    if (playerSrs[i] != null)
                    {
                        playerSrs[i].color = (originalColors != null && i < originalColors.Length) ? originalColors[i] : Color.white;
                    }
                }
            }
        }
        else
        {
            float maxDuration = DownedBleedoutDuration;
            float timerRatio = Mathf.Clamp01(NetDownedTimer.Value / maxDuration);
            Color currentColor = Color.Lerp(downedExpiredColor, downedColor, timerRatio);
            ApplyPlayerColor(currentColor);
        }
    }

    private void ApplyPlayerColor(Color color)
    {
        if (playerSrs != null)
        {
            for (int i = 0; i < playerSrs.Length; i++)
            {
                if (playerSrs[i] != null)
                {
                    playerSrs[i].color = color;
                }
            }
        }
    }

    public void GoDown()
    {
        if (!IsServer) return;

        if (IsDownedSync.Value || IsDeadSync.Value) return;

        Audio.PlayNetworkedSFX(FMODEvents.instance.downed, transform.position);

        IsDownedSync.Value = true;
        IsDeadSync.Value = false;
        NetDownedTimer.Value = downedBleedoutDuration;
        _playerStats.isHidden.Value = true;

        gameObject.layer = LayerMask.NameToLayer(downedLayerName);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }

        if (bleedoutCoroutine != null) StopCoroutine(bleedoutCoroutine);
        bleedoutCoroutine = StartCoroutine(BleedoutTimerRoutine());
    }

    private IEnumerator BleedoutTimerRoutine()
    {
        float timer = downedBleedoutDuration;
        while (timer > 0f && IsDownedSync.Value && !IsDeadSync.Value)
        {
            yield return null;
            timer -= Time.deltaTime;
            NetDownedTimer.Value = Mathf.Max(0f, timer);
        }

        if (IsDownedSync.Value && !IsDeadSync.Value && NetDownedTimer.Value <= 0f)
        {
            DieFromBleedout();
        }
    }

    public void DieFromBleedout()
    {
        if (!IsServer) return;

        StopBeingRevivedServerRpc();

        IsDeadSync.Value = true;
        IsDownedSync.Value = false;
        _playerStats.isHidden.Value = true;

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    public void RespawnFromDeathServer(Vector3 spawnPos)
    {
        if (!IsServer) return;

        if (bleedoutCoroutine != null)
        {
            StopCoroutine(bleedoutCoroutine);
            bleedoutCoroutine = null;
        }

        IsDeadSync.Value = false;
        IsDownedSync.Value = false;
        _playerStats.isHidden.Value = false;

        transform.position = spawnPos;
        if (_rb != null)
        {
            _rb.position = spawnPos;
            _rb.linearVelocity = Vector2.zero;
        }

        TeleportPlayerClientRpc(spawnPos);

        gameObject.layer = LayerMask.NameToLayer(defaultLayerName);

        SetDeadVisuals(false);
        UpdatePlayerColor(false);
    }

    [Rpc(SendTo.Everyone)]
    private void TeleportPlayerClientRpc(Vector3 newPos)
    {
        transform.position = newPos;
        if (_rb != null)
        {
            _rb.position = newPos;
            _rb.linearVelocity = Vector2.zero;
        }
    }

    [Rpc(SendTo.Server)]
    public void StartBeingRevivedServerRpc(ulong reviverNetworkObjectId)
    {
        // The server finds the reviver object from the ID
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(reviverNetworkObjectId, out var networkObject))
            return;

        Player reviver = networkObject.GetComponent<Player>();

        if (!IsDowned || reviveCoroutine != null || reviver == null) return;

        currentReviver = reviver;
        reviveStartPosition = reviver.transform.position;


        Audio.PlayNetworkedSFX(FMODEvents.instance.reviveSequence, transform.position);


        SetSliderStateClientRpc(true, 0f);

        reviveCoroutine = StartCoroutine(ReviveProcess());
    }

    [Rpc(SendTo.Server)]
    public void StopBeingRevivedServerRpc()
    {
        if (reviveCoroutine != null)
        {
            StopCoroutine(reviveCoroutine);
            reviveCoroutine = null;
            currentReviver = null;

            SetSliderStateClientRpc(false, 0f);

        }
    }

    private IEnumerator ReviveProcess()
    {

        float timer = 0f;
        float targetDuration = ReviveDuration;
        if (currentReviver != null && currentReviver.TryGetComponent<ReviveController>(out var reviverRc))
        {
            targetDuration = reviverRc.ReviveDuration;
        }

        while (timer < targetDuration)
        {
            if (currentReviver != null)
            {
                // Check the squared distance
                float sqrDistance = ((Vector2)currentReviver.transform.position - reviveStartPosition).sqrMagnitude;

                if (sqrDistance > (maxReviveDistance * maxReviveDistance))
                {
                    Debug.Log("Reviver moved too far! Revive canceled.");

                    SetSliderStateClientRpc(false, 0f);

                    currentReviver.CancelMyReviveAction(); // Tell the reviver to cancel
                    yield break; // Exit the coroutine immediately
                }
            }
            else
            {
                SetSliderStateClientRpc(false, 0f);

                yield break; // Failsafe in case the reviver is destroyed/null
            }

            timer += Time.deltaTime; // Advance the timer

            float progress = Mathf.Clamp01(timer / targetDuration);
            UpdateSliderProgressClientRpc(progress);

            yield return null; // Wait for the next frame
        }

        // If the loop finishes without breaking, the revive is successful!
        if (bleedoutCoroutine != null)
        {
            StopCoroutine(bleedoutCoroutine);
            bleedoutCoroutine = null;
        }

        IsDownedSync.Value = false;
        IsDeadSync.Value = false;
        _playerStats.isHidden.Value = false;
        _playerStats.SetHealth(healthAfterRevive);

        gameObject.layer = LayerMask.NameToLayer(defaultLayerName);

        SetSliderStateClientRpc(false, 0f);

        // Clean up
        currentReviver.CancelMyReviveAction(); // Resets the reviver's target
        currentReviver = null;
        reviveCoroutine = null;
    }

    [Rpc(SendTo.Everyone)]
    private void SetSliderStateClientRpc(bool isActive, float initialProgress)
    {
        if (reviveProgressSlider != null)
        {
            reviveProgressSlider.value = initialProgress;
            reviveProgressSlider.gameObject.SetActive(isActive);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateSliderProgressClientRpc(float progress)
    {
        if (reviveProgressSlider != null)
        {
            reviveProgressSlider.value = progress;
        }
    }

}