using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class ReviveController : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsDownedSync = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Physics")]
    [SerializeField] private string defaultLayerName = "Player";
    [SerializeField] private string downedLayerName = "DownedPlayer";

    [SerializeField] private float reviveDuration = 4f;
    [SerializeField] private int healthAfterRevive = 20;
    [SerializeField] private float crawlSpeed = 1.5f;
    [SerializeField] private float maxReviveDistance = 2.0f;

    public bool IsDowned => IsDownedSync.Value;
    public float CrawlSpeed => crawlSpeed;

    private Coroutine reviveCoroutine;
    private Player _player;
    private Rigidbody2D _rb;

    private Player currentReviver;
    private Vector2 reviveStartPosition;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody2D>();
    }

    public void GoDown()
    {
        if (!IsServer) return;

        if (IsDownedSync.Value) return;

        IsDownedSync.Value = true;
        _player.isHidden.Value = true;

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
        }
    }

    private IEnumerator ReviveProcess()
    {

        float timer = 0f;

        // Loop until the duration is met
        while (timer < reviveDuration)
        {
            if (currentReviver != null)
            {
                // Check the squared distance
                float sqrDistance = ((Vector2)currentReviver.transform.position - reviveStartPosition).sqrMagnitude;

                if (sqrDistance > (maxReviveDistance * maxReviveDistance))
                {
                    Debug.Log("Reviver moved too far! Revive canceled.");
                    currentReviver.CancelMyReviveAction(); // Tell the reviver to cancel
                    yield break; // Exit the coroutine immediately
                }
            }
            else
            {
                yield break; // Failsafe in case the reviver is destroyed/null
            }

            timer += Time.deltaTime; // Advance the timer
            yield return null; // Wait for the next frame
        }

        // If the loop finishes without breaking, the revive is successful!
        IsDownedSync.Value = false;
        _player.isHidden.Value = false;
        _player.Health = healthAfterRevive;

        gameObject.layer = LayerMask.NameToLayer(defaultLayerName);

        // Clean up
        currentReviver.CancelMyReviveAction(); // Resets the reviver's target
        currentReviver = null;
        reviveCoroutine = null;
    }
}