using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : NetworkBehaviour
{
    private Player player;
    private PlayerStats _playerStats;
    private Camera _mainCam;
    private ReviveController _revive;
    [SerializeField] private Transform pivot;

    private NetworkVariable<float> syncRotation = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        player = GetComponent<Player>();
        _playerStats = GetComponent<PlayerStats>();
        _revive = GetComponent<ReviveController>();
        _mainCam = Camera.main;
    }

    private void Update()
    {
        if (IsOwner)
        {
            if (Time.timeScale > 0)
            {
                RotateFirePoint();
            }
        }
        else
        {
            // If we aren't the owner, just apply the synced rotation
            pivot.rotation = Quaternion.Euler(0, 0, syncRotation.Value);
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

        syncRotation.Value = angle;
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (_revive != null && _revive.IsDownedSync.Value) return;

        if (!context.started) return;

        if (player.activeSpell == null) return;

        if (_playerStats.Mana < player.activeSpell.ManaCost)
        {
            Debug.Log($"Need {player.activeSpell.ManaCost} Mana! Current: {_playerStats.Mana}");
            return;
        }

        player.RequestCastSpellServerRpc(player.ActiveSpellIndex);

    }
}
