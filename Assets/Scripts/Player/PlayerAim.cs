using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
    }
    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.started && player.activeSpell != null)
        {
            player.activeSpell.Cast(player);
        }
    }
}
