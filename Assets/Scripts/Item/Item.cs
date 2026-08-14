using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public abstract class Item : NetworkBehaviour
{
    [Header("Network")]
    public int itemID;

    [Header("Lifetime Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private bool autoDespawn = true;

    [Header("Loot Drops")]
    [SerializeField] private float _dropWeight = 1f;
    public float DropWeight => _dropWeight;

    [Header("UI Visuals")]
    [SerializeField] private Sprite _itemIcon;
    [SerializeField] private Color _itemColor = Color.white;

    [Header("Pickup Settings")]
    private float pickupDelay = 0.5f;
    public NetworkVariable<ulong> DropperClientId = new NetworkVariable<ulong>(ulong.MaxValue);
    private bool _cooldownFinished = false;

    public Sprite ItemIcon => _itemIcon;
    public Color ItemColor => _itemColor;

    [SerializeField] protected string _itemName;
    [SerializeField] protected string _itemDescription;
    [SerializeField] protected float duration = 0f;
    protected bool _isUsed;
    public string ItemName => _itemName;
    public string ItemDescription => _itemDescription;
    public bool IsUsed
    {
        get => _isUsed;
        protected set => _isUsed = value;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        StartCoroutine(PickupCooldownRoutine());

        if (IsServer && autoDespawn)
        {
            StartCoroutine(DespawnTimerRoutine());
        }
    }

    private IEnumerator PickupCooldownRoutine()
    {
        _cooldownFinished = false;
        yield return new WaitForSeconds(pickupDelay);
        _cooldownFinished = true;
    }

    public bool CanBePickedUpBy(ulong clientId)
    {

        if (_cooldownFinished) return true;

        if (DropperClientId.Value == ulong.MaxValue) return true;

        return clientId != DropperClientId.Value;
    }

    private IEnumerator DespawnTimerRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        // Verify the object is still spawned before despawning
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        
    }

    [Header("Audio Settings")]
    [SerializeField] private float minBounceSpeed = 0.5f;
    [SerializeField] private float maxBounceSpeed = 8f;
    [SerializeField] private float minBounceVolume = 0.15f;
    [SerializeField] private float maxBounceVolume = 1f;

    private float lastBounceSoundTime = -1f;

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            if (Time.time >= lastBounceSoundTime + 0.15f)
            {
                lastBounceSoundTime = Time.time;
                if (FMODEvents.instance != null)
                {
                    float speed = collision.relativeVelocity.magnitude;
                    float t = Mathf.InverseLerp(minBounceSpeed, maxBounceSpeed, speed);
                    float volume = Mathf.Lerp(minBounceVolume, maxBounceVolume, t);

                    Audio.PlayNetworkedSFX(FMODEvents.instance.potionBounce, transform.position, volume);
                }
            }
        }
    }

    public void Use(Entity entity)
    {
        Audio.PlayNetworkedSFX(FMODEvents.instance.swig, entity.transform.position);
        OnUse(entity);
    }

    protected abstract void OnUse(Entity entity);

    protected void ApplyTimeEffect(Entity entity, Action startEffect, Action endEffect)
    {
        entity.StartCoroutine(EffectRoutine(startEffect, endEffect));
    }

    private IEnumerator EffectRoutine(Action start, Action end)
    {
        start?.Invoke();

        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
        }

        end?.Invoke();
    }
}