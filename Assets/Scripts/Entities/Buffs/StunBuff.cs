using UnityEngine;

public class StunBuff : Buff
{
    public StunBuff(Entity entity) : base(entity, BUFFTYPE.Stun, 2f) { }

    public override void Apply()
    {

    }

    public override void Undo()
    {

    }
}
