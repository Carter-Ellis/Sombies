using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    private Entity _entity;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedBack;
    private bool _isSprinting;
    private ReviveController _revive;

    public float CurrentSpeed
    {
        get
        {
            if (_revive != null && _revive.IsDownedSync.Value)
            {
                return _revive.CrawlSpeed;
            }
            return _isSprinting ? _entity.SprintSpeed : _entity.WalkSpeed;
        }
    }

    private void Awake()
    {
        _entity = GetComponent<Entity>();
        _revive = GetComponent<ReviveController>();
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (isKnockedBack) return;
        rb.linearVelocity = moveInput * CurrentSpeed;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.performed)
        {
            moveInput = context.ReadValue<Vector2>();
        }
        else if (context.canceled)
        {
            moveInput = Vector2.zero;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (context.started)
        {
            _isSprinting = true;
        }
        else if (context.canceled)
        {
            _isSprinting = false;
        }
    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
        StartCoroutine(KnockbackRoutine(force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 force, float duration)
    {
        isKnockedBack = true;
        rb.linearVelocity = force;

        yield return new WaitForSeconds(duration);

        isKnockedBack = false;
    }

    public void SetSpeedToDefault()
    {
        _entity.WalkSpeed = _entity.BaseWalkSpeed;
        _entity.SprintSpeed = _entity.BaseSprintSpeed;
    }

    [Rpc(SendTo.Owner)]
    public void ApplyKnockbackClientRpc(Vector2 force, float duration)
    {
        ApplyKnockback(force, duration);
    }
}