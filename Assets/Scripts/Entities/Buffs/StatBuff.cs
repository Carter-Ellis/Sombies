using UnityEngine;

public class StatBuff : Buff
{
    public StatBuff(Entity entity, BUFFTYPE type, float amt) : base(entity, type, amt) { }

    public override void Apply()
    {
    }

    public override void Undo()
    {
    }
}
