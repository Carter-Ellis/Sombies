using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BUFFTYPE { Speed, Strength, Defense, Stealth, Stun }

public abstract class Buff
{
    public BUFFTYPE Type { get; protected set; }
    public float Amount { get; protected set; }

    protected Entity targetEntity;

    public Buff(Entity entity, BUFFTYPE type, float amount)
    {
        this.targetEntity = entity;
        this.Type = type;
        this.Amount = amount;
    }

    public abstract void Apply();
    public abstract void Undo();
}

public class BuffManager : MonoBehaviour
{
    private Entity _entity;

    // We store the amounts so we can find the Max
    private Dictionary<BUFFTYPE, List<Buff>> _activeBuffs = new Dictionary<BUFFTYPE, List<Buff>>();

    private void Awake()
    {
        _entity = GetComponent<Entity>();

        foreach (BUFFTYPE type in Enum.GetValues(typeof(BUFFTYPE)))
        {
            _activeBuffs[type] = new List<Buff>();
        }

    }

    public void AddBuff(Buff buff)
    {
        _activeBuffs[buff.Type].Add(buff);
        buff.Apply();
        RefreshStat(buff.Type);
    }

    public void RemoveBuff(Buff buff)
    {
        _activeBuffs[buff.Type].Remove(buff);
        buff.Undo();
        RefreshStat(buff.Type);
    }

    private void RefreshStat(BUFFTYPE type)
    {
        bool isStunned = _activeBuffs[BUFFTYPE.Stun].Count > 0;

        // 1. Get the total of buffs like for haste and slow for example
        float totalBoost = _activeBuffs[type].Count > 0 ? _activeBuffs[type].Sum(b => b.Amount) : 0;

        // 2. Apply the winner to the correct stat
        switch (type)
        {
            case BUFFTYPE.Stun:
            case BUFFTYPE.Speed:

                if (isStunned)
                {
                    _entity.WalkSpeed = 0f;
                    if (_entity.BaseSprintSpeed > 0)
                    {
                        _entity.SprintSpeed = 0f;
                    }
                }
                else
                {
                    float newWalk = _entity.BaseWalkSpeed + totalBoost;
                    float newSprint = _entity.BaseSprintSpeed + totalBoost;

                    _entity.WalkSpeed = Mathf.Max(0.1f, newWalk);

                    if (_entity.BaseSprintSpeed > 0)
                    {
                        _entity.SprintSpeed = Mathf.Max(0.5f, newSprint);
                    }
                }
                break;

            case BUFFTYPE.Strength:
                // _player.Damage = _player.BaseDamage + maxBoost;
                break;
            case BUFFTYPE.Stealth:
                if (_entity is Player player)
                {
                    player.isHidden.Value = _activeBuffs[type].Count > 0;
                }
                break;
            
        }
    }

    public void AddTemporaryBuff(Buff buff, float duration)
    {
        StartCoroutine(TemporaryBuffRoutine(buff, duration));
    }

    private IEnumerator TemporaryBuffRoutine(Buff buff, float duration)
    {
        AddBuff(buff);
        yield return new WaitForSeconds(duration);
        RemoveBuff(buff);
    }

}