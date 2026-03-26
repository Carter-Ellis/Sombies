using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "Fireball", menuName = "Spells/Fireball")]
public class Fireball : Spell
{
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float launchForce = 15f;
    public override void Cast(Player player)
    {
        if (player.firepoint == null)
        {
            Debug.LogWarning("Player does not have a FirePoint assigned!");
            return;
        }

        // 2. Get Mouse Position in World Space
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0;

        // 3. Calculate Direction from the Firepoint to the Mouse
        Vector2 shootDirection = (mouseWorldPos - player.firepoint.position).normalized;

        // 4. Spawn the Projectile at the Firepoint's position
        GameObject ball = Instantiate(fireballPrefab, player.firepoint.position, Quaternion.identity);

        if (ball.TryGetComponent(out Projectile proj))
        {
            proj.Initialize(player); // Pass the 'player' reference from the Cast parameter
        }

        // 5. Apply Physics
        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = shootDirection * launchForce;
        }

        // 6. Rotate the projectile to face its flight path
        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        ball.transform.rotation = Quaternion.Euler(0, 0, angle);

    }

}
