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

    private Vector2 _stickInput;
    private ControlDeviceType _lastUsedDevice = ControlDeviceType.Mouse;

    private enum ControlDeviceType { Mouse, Gamepad }

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
                // Fallback check: If right stick is actively being pushed, force Gamepad mode
                if (Gamepad.current != null)
                {
                    Vector2 rightStick = Gamepad.current.rightStick.ReadValue();
                    if (rightStick.sqrMagnitude > 0.1f)
                    {
                        _stickInput = rightStick;
                        _lastUsedDevice = ControlDeviceType.Gamepad;
                    }
                }

                RotateFirePoint();
            }
        }
        else
        {
            pivot.rotation = Quaternion.Euler(0, 0, syncRotation.Value);
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.control.device is Gamepad)
        {
            _stickInput = context.ReadValue<Vector2>();

            if (_stickInput.sqrMagnitude > 0.1f)
            {
                _lastUsedDevice = ControlDeviceType.Gamepad;
            }
        }
        else if (context.control.device is Mouse or Pointer)
        {
            // Only switch to mouse if mouse is actually moved substantially (prevents micro-jitter)
            Vector2 mouseDelta = context.ReadValue<Vector2>();
            if (context.control.name == "position" || mouseDelta.sqrMagnitude > 0.5f)
            {
                _lastUsedDevice = ControlDeviceType.Mouse;
            }
        }
    }

    private void RotateFirePoint()
    {
        Vector2 lookDir = Vector2.zero;

        if (_lastUsedDevice == ControlDeviceType.Gamepad)
        {
            // Don't update direction if stick is released; keep aiming where last pushed
            if (_stickInput.sqrMagnitude < 0.1f) return;

            lookDir = _stickInput.normalized;
        }
        else // Mouse
        {
            if (Mouse.current != null)
            {
                Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 worldMousePos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
                worldMousePos.z = 0;

                lookDir = ((Vector2)worldMousePos - (Vector2)transform.position).normalized;
            }
        }

        if (lookDir != Vector2.zero)
        {
            float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

            pivot.rotation = Quaternion.Euler(0, 0, angle);
            syncRotation.Value = angle;
        }
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