using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 7f;
    private float speed;
    Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedBack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Start()
    {
        speed = walkSpeed;
    }

    void FixedUpdate()
    {
        if (isKnockedBack) return;
        rb.linearVelocity = moveInput * speed;
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
            speed = sprintSpeed;
        }
        else if (context.canceled)
        {
            speed = walkSpeed;
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

}
