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
    private bool _isHoldingClick = false;
    private float _lastSpellCastTime = 0f;

    private enum ControlDeviceType { Mouse, Gamepad }

    public NetworkVariable<float> syncRotation = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

                if (_isHoldingClick && !_isChargingSpell)
                {
                    TryCastActiveSpell();
                }
            }
        }
        else
        {
            // Gradually interpolate towards the network variable's angle instead of snapping
            float currentAngle = pivot.eulerAngles.z;
            float targetAngle = syncRotation.Value;
            float smoothedAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * 15f);

            pivot.rotation = Quaternion.Euler(0, 0, smoothedAngle);
        }

        // Unified visual update
        if (player != null && player.SpriteTransform != null)
        {
            player.SpriteTransform.rotation = pivot.rotation * Quaternion.Euler(0, 0, 0);
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
            if (_stickInput.sqrMagnitude < 0.1f) return;

            lookDir = _stickInput.normalized;
        }
        else 
        {
            if (Mouse.current != null)
            {
                if (_mainCam == null)
                {
                    _mainCam = Camera.main;
                }

                if (_mainCam == null) return;

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
            
            if (Mathf.Abs(syncRotation.Value - angle) > 0.5f)
            {
                syncRotation.Value = angle;
            }
        }
    }

    private bool _isChargingSpell = false;

    public void OnClick(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.started)
        {
            _isHoldingClick = true;

            if (player.activeSpell != null && (player.activeSpell.IsChargeSpell || player.activeSpell.Name.Contains("Pulse")))
            {
                _isChargingSpell = true;
                player.RequestStartChargingSpellServerRpc(player.ActiveSpellIndex);
            }
            else
            {
                TryCastActiveSpell();
            }
        }
        else if (context.canceled)
        {
            _isHoldingClick = false;

            if (_isChargingSpell)
            {
                _isChargingSpell = false;
                if (PulseProj.LocalChargingPulse != null)
                {
                    PulseProj.LocalChargingPulse.LaunchFromClient();
                }
                player.RequestReleaseChargingSpellServerRpc();
            }
        }
    }

    private void TryCastActiveSpell()
    {
        if (_revive != null && _revive.IsDownedSync.Value) return;

        if (player.activeSpell == null) return;

        if (Time.time < _lastSpellCastTime + player.activeSpell.Cooldown) return;

        if (_playerStats.Mana < player.activeSpell.ManaCost)
        {
            return;
        }

        _lastSpellCastTime = Time.time;
        player.RequestCastSpellServerRpc(player.ActiveSpellIndex);
    }
}