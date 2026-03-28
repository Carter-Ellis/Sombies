using UnityEngine;

public class Sombie : Enemy
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Keep trying to hit the player if they are pinned
        TryDamagePlayer(collision.collider);
    }

}
