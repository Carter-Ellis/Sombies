using System.Collections;
using UnityEngine;

public class SombiePotion : Item
{
    [SerializeField] private float duration = 3.5f;

    public override void Use(Player player)
    {
        // Start the effect on the PLAYER, not the potion
        player.StartCoroutine(ApplyPotionEffect(player));

        Destroy(gameObject); 
    }

    private IEnumerator ApplyPotionEffect(Player player)
    {
        player.isHidden = true;

        yield return new WaitForSeconds(duration);

        player.isHidden = false;
    }
}
