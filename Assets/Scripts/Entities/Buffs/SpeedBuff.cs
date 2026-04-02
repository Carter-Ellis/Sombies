using UnityEngine;

public class SpeedBuff : Buff
{
    public SpeedBuff(Entity e, float amt) : base(e, BUFFTYPE.Speed, amt) { }
    public override void Apply()
    {
    }

    public override void Undo()
    {
    }

}
