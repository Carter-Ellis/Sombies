using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.Experimental.GraphView.GraphView;

public class PlayerAim : MonoBehaviour
{
    private Player player;
    private Camera _mainCam;
    private ReviveController _revive;
    [SerializeField] private Transform pivot;

    private void Awake()
    {
        player = GetComponent<Player>();
        _revive = GetComponent<ReviveController>();
        _mainCam = Camera.main;
    }

    private void Update()
    {
        // Only rotate if the game isn't paused or the player isn't dead
        if (Time.timeScale > 0)
        {
            RotateFirePoint();
        }
    }
    private void RotateFirePoint()
    {
        // Use the stored _mainCam reference
        Vector3 mousePos = _mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0;

        Vector2 lookDir = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

        pivot.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (_revive != null && _revive.IsDowned) return;

        if (!context.started) return;

        if (player.activeSpell == null) return;

        if (player.Mana < player.activeSpell.ManaCost)
        {
            Debug.Log($"Need {player.activeSpell.ManaCost} Mana! Current: {player.Mana}");
            return;
        }

        player.Mana -= player.activeSpell.ManaCost;
        player.activeSpell.Cast(player);
        
    }
}
