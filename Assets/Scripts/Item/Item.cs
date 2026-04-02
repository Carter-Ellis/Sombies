using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public abstract class Item : NetworkBehaviour
{
    [Header("Network")]
    public int itemID;

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

    public abstract void Use(Entity entity);

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
