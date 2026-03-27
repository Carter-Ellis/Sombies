using UnityEngine;

[CreateAssetMenu(fileName = "New Projectile Spell", menuName = "Spells/ProjectileSpell")]
public class ProjectileSpell : Spell
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float launchForce = 15f;

    public override void Cast(Player player)
    {
        if (player.firepoint == null) return;

        GameObject ball = Instantiate(projectilePrefab, player.firepoint.position, player.firepoint.rotation);

        if (ball.TryGetComponent(out Projectile proj))
        {
            proj.Initialize(player, damage);
        }

        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = player.firepoint.right * launchForce;
        }
    }
}
