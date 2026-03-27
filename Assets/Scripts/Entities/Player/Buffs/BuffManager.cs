using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BUFFTYPE { Speed, Strength, Defense }

public abstract class Buff
{
    public BUFFTYPE Type { get; protected set; }
    public float Amount { get; protected set; }
    protected Player player;

    public Buff(Player player, BUFFTYPE type, float amount)
    {
        this.player = player;
        this.Type = type;
        this.Amount = amount;
    }

    public abstract void Apply();
    public abstract void Undo();
}

public class BuffManager : MonoBehaviour
{
    private Player _player;

    // We store the amounts so we can find the Max
    private Dictionary<BUFFTYPE, List<Buff>> _activeBuffs = new Dictionary<BUFFTYPE, List<Buff>>();

    private void Awake()
    {
        _player = GetComponent<Player>();

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
        // 1. Get the winner (The Highest Amount)
        float maxBoost = _activeBuffs[type].Count > 0 ? _activeBuffs[type].Max(b => b.Amount) : 0;

        // 2. Apply the winner to the correct stat
        switch (type)
        {
            case BUFFTYPE.Speed:
                _player.WalkSpeed = _player.BaseWalkSpeed + maxBoost;
                _player.SprintSpeed = _player.BaseSprintSpeed + maxBoost;
                break;

            case BUFFTYPE.Strength:
                // _player.Damage = _player.BaseDamage + maxBoost;
                break;
        }
    }
}