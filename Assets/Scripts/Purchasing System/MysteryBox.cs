using System.Collections;
using UnityEngine;

public class MysteryBox : PurchaseSystem
{
    [Header("Mystery Box Settings")]
    [SerializeField] private SpellPurchase[] possibleSpells;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float displayDuration = 7.5f;

    private void Awake()
    {
        disableOnPurchase = false;
    }

    protected override void GrantPurchase(Player player)
    {
        StartCoroutine(Sequence());
    }
    private IEnumerator Sequence()
    {
        // 1. Pick and Spawn
        int randomIndex = Random.Range(0, possibleSpells.Length);
        SpellPurchase spawnedSpell = Instantiate(possibleSpells[randomIndex], spawnPoint.position, spawnPoint.rotation);

        spawnedSpell.MakeFree();

        // 2. Wait
        // During this time, hasBeenPurchased is TRUE, 
        // so the player can't buy another box use yet.
        float timer = 0;
        while (timer < displayDuration)
        {
            // If the player picked up the spell, it will be destroyed or disabled.
            if (spawnedSpell == null || !spawnedSpell.gameObject.activeInHierarchy)
            {
                break; // Player grabbed it early!
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. Clean up
        if (spawnedSpell != null)
        {
            Destroy(spawnedSpell.gameObject);
        }

        // 4. Reset the Box
        Debug.Log("Box is ready for another spin!");
        hasBeenPurchased = false;
    }

}
