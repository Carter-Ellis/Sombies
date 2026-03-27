using UnityEngine;

public class SpeedBuff : Buff
{
    public SpeedBuff(Player p, float amt) : base(p, BUFFTYPE.Speed, amt) { }
    public override void Apply()
    {
    }

    public override void Undo()
    {
    }

}
