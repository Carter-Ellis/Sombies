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

        if (IsServer && autoDespawn)
        {
            StartCoroutine(DespawnTimerRoutine());
        }
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

    public void Use(Entity entity)
    {
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