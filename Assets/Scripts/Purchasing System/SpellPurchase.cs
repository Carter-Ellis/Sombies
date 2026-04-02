using UnityEngine;

public class SpellPurchase : PurchaseSystem
{
    protected override void GrantPurchase(Entity buyer)
    {
        Player player = buyer.GetComponent<Player>();
        if (player == null) return;

        player.AddSpell(spell);
        player.SyncActiveSpellID.Value = spell.spellID;
    }

}
