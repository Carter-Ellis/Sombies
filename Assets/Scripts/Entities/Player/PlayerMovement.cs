using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Base Settings")]
    public float baseWalkSpeed = 5f;
    public float baseSprintSpeed = 7f;

    public float walkSpeed;
    public float sprintSpeed;

    Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedBack;
    private bool _isSprinting;
    private ReviveController _revive;


    public float CurrentSpeed
    {
        get
        {
            if (_revive != null && _revive.IsDowned)
            {
                return _revive.CrawlSpeed;
            }
            return _isSprinting ? sprintSpeed : walkSpeed;
        }
    }

    private void Awake()
    {
        _revive = GetComponent<ReviveController>();
        rb = GetComponent<Rigidbody2D>();

        walkSpeed = baseWalkSpeed;
        sprintSpeed = baseSprintSpeed;
    }


    void FixedUpdate()
    {

        if (isKnockedBack) return;
        rb.linearVelocity = moveInput * CurrentSpeed;

    }

    public void OnMove(InputAction.CallbackContext context)
    {
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
        walkSpeed = baseWalkSpeed;
        sprintSpeed = baseSprintSpeed;
    }

}
