using UnityEngine;

public class StatBuff : Buff
{
    public StatBuff(Player p, BUFFTYPE type, float amt) : base(p, type, amt) { }

    public override void Apply()
    {
        Debug.Log($"{Type} buff started: +{Amount}");
    }

    public override void Undo()
    {
        Debug.Log($"{Type} buff ended.");
    }
}
