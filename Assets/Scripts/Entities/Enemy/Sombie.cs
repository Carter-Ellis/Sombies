using UnityEngine;

public class Sombie : Enemy
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Player player = collision.collider.GetComponent<Player>();

        if (player != null)
        {
            PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
            playerMovement.ApplyKnockback((player.transform.position - transform.position).normalized * KnockbackForce, KnockbackDuration);
            player.TakeDamage(DamageAmount);

        }
    }

}
