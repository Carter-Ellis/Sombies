using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ReviveController : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsDownedSync = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Visual Customization")]
    private SpriteRenderer playerSr;
    [SerializeField] private Color downedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    private Color originalColor = Color.white;

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
        playerSr = GetComponent<SpriteRenderer>();

        // Hide the revive progress slider on awake
        if (reviveProgressSlider != null)
        {
            reviveProgressSlider.gameObject.SetActive(false);
        }

    }

    public override void OnNetworkSpawn()
    {
        IsDownedSync.OnValueChanged += OnDownedStateChanged;

        if (IsServer)
        {
            _netReviveDuration.Value = _baseReviveDuration;
        }

        UpdatePlayerColor(IsDownedSync.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDownedSync.OnValueChanged -= OnDownedStateChanged;
    }

    private void OnDownedStateChanged(bool previousValue, bool newValue)
    {
        UpdatePlayerColor(newValue);
    }

    private void UpdatePlayerColor(bool isDowned)
    {
        if (playerSr != null)
        {
            playerSr.color = isDowned ? downedColor : originalColor;
        }
    }

    public void GoDown()
    {
        if (!IsServer) return;

        if (IsDownedSync.Value) return;

        Audio.playSFX(FMODEvents.instance.downed, transform.position);

        IsDownedSync.Value = true;
        _playerStats.isHidden.Value = true;

        gameObject.layer = LayerMask.NameToLayer(downedLayerName);

        if (_rb != null)
        {
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


        Audio.playSFX(FMODEvents.instance.reviveSequence, transform.position);


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
        IsDownedSync.Value = false;
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