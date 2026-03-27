using UnityEngine;

public class StealthBuff : Buff
{
    public StealthBuff(Entity entity) : base(entity, BUFFTYPE.Stealth, 0f) { }

    public override void Apply()
    {

    }

    public override void Undo()
    {

    }
}
