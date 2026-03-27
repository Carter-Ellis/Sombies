using UnityEngine;

public class StatBuff : Buff
{
    public StatBuff(Entity entity, BUFFTYPE type, float amt) : base(entity, type, amt) { }

    public override void Apply()
    {
        Debug.Log($"{targetEntity.name}: {Type} buff started: +{Amount}");
    }

    public override void Undo()
    {
        Debug.Log($"{targetEntity.name}: {Type} buff ended.");
    }
}
