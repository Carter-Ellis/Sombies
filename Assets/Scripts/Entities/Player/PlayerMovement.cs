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

    // ---> NEW VARIABLES FOR WALKING BOB <---
    [Header("Visual Walking Bob")]
    [SerializeField] private float baseBobSpeed = 2f;
    [SerializeField] private float bobAmount = 0.1f;
    private Player _player;
    private Vector3 _defaultSpriteLocalPos;
    private Vector3 _lastPos;
    private float _bobTimer;

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

        // Grab the player reference to access the SpriteTransform
        _player = GetComponent<Player>();
    }

    private void Start()
    {
        // Store the initial local position of the sprite so we can return to it when standing still
        if (_player != null && _player.SpriteTransform != null)
        {
            _defaultSpriteLocalPos = _player.SpriteTransform.localPosition;
            _lastPos = transform.position;
        }
    }

    private void Update()
    {
        // ---> NEW CODE: Procedural Bobbing <---
        if (_player != null && _player.SpriteTransform != null)
        {
            // Calculate actual distance moved this frame (Works for Owner AND other clients observing over the network)
            float moveDistanceThisFrame = (transform.position - _lastPos).magnitude;
            _lastPos = transform.position;

            // Convert distance to a speed value
            float visualSpeed = moveDistanceThisFrame / Time.deltaTime;

            // If we are moving fast enough, not knocked back, and not downed...
            if (visualSpeed > 0.1f && !isKnockedBack && (_revive == null || !_revive.IsDownedSync.Value))
            {
                // Increase timer based on current speed (so sprinting makes you bob faster!)
                _bobTimer += Time.deltaTime * visualSpeed * baseBobSpeed;

                // Use a Sine wave to smoothly move the Y position up and down
                float newY = _defaultSpriteLocalPos.y + (Mathf.Sin(_bobTimer) * bobAmount);
                _player.SpriteTransform.localPosition = new Vector3(_defaultSpriteLocalPos.x, newY, _defaultSpriteLocalPos.z);
            }
            else
            {
                // Smoothly reset the sprite back to its default standing position
                _bobTimer = 0f;
                _player.SpriteTransform.localPosition = Vector3.Lerp(
                    _player.SpriteTransform.localPosition,
                    _defaultSpriteLocalPos,
                    Time.deltaTime * 10f
                );
            }
        }
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