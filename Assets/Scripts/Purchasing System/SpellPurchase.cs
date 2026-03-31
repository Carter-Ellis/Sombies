using UnityEngine;

public class SpellPurchase : PurchaseSystem
{
    protected override void GrantPurchase(Player player)
    {
        player.AddSpell(spell);
        player.SyncActiveSpellID.Value = spell.spellID;
    }

}
