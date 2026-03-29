using UnityEngine;

public class Sombie : Enemy
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);

        // Stop the NavMesh momentum immediately upon touching the player
        if (collision.gameObject.GetComponent<Player>() != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Keep trying to hit the player if they are pinned
        TryDamagePlayer(collision.collider);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // Resume NavMesh movement once the player is no longer touching
        if (collision.gameObject.GetComponent<Player>() != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }
    }
}