using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class PlayerMovement : NetworkBehaviour
{
    private Entity _entity;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedBack;
    private bool _isSprinting;
    private ReviveController _revive;

    // ---> NEW VARIABLES FOR WALKING BOB & AUDIO <---
    [Header("Visual Walking Bob")]
    [SerializeField] private float baseBobSpeed = 2f;
    [SerializeField] private float bobAmount = 0.1f;
    private Player _player;
    private Vector3 _defaultSpriteLocalPos;
    private Vector3 _lastPos;
    private float _bobTimer;

    private FMOD.Studio.EventInstance _footstepInstance;
    private bool _isFootstepPlaying;
    private float _nextFootstepRetryTime;

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
        else
        {
            _lastPos = transform.position;
        }
    }

    private void Update()
    {
        float visualSpeed = 0f;

        // Calculate actual distance moved this frame (Works for Owner AND other clients observing over the network)
        float moveDistanceThisFrame = (transform.position - _lastPos).magnitude;
        _lastPos = transform.position;

        if (Time.deltaTime > 0f)
        {
            visualSpeed = moveDistanceThisFrame / Time.deltaTime;
        }

        // ---> Procedural Bobbing <---
        if (_player != null && _player.SpriteTransform != null)
        {
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

        // ---> Footstep Audio <---
        UpdateFootstepAudio(visualSpeed);
    }

    private void UpdateFootstepAudio(float visualSpeed)
    {
        bool isMoving = visualSpeed > 0.5f && !isKnockedBack && (_revive == null || !_revive.IsDownedSync.Value);

        if (isMoving && FMODEvents.instance != null && !FMODEvents.instance.walkWood.IsNull)
        {
            if (!_isFootstepPlaying || !_footstepInstance.isValid())
            {
                if (Time.time >= _nextFootstepRetryTime)
                {
                    _footstepInstance = Audio.CreateSFXInstance(FMODEvents.instance.walkWood);
                    if (_footstepInstance.isValid())
                    {
                        _footstepInstance.start();
                        _isFootstepPlaying = true;
                    }
                    else
                    {
                        // Cooldown 2s before retrying if the event is unbuilt/missing in FMOD bank
                        _nextFootstepRetryTime = Time.time + 2f;
                    }
                }
            }
            else
            {
                _footstepInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE playbackState);
                if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED)
                {
                    _footstepInstance.start();
                }
            }

            if (_footstepInstance.isValid())
            {
                _footstepInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));

                float baseSpeed = (_entity != null && _entity.BaseWalkSpeed > 0) ? _entity.BaseWalkSpeed : 5f;
                float pitch = Mathf.Clamp(visualSpeed / baseSpeed, 0.8f, 2.0f);
                _footstepInstance.setPitch(pitch);
            }
        }
        else
        {
            StopFootstepAudio();
        }
    }

    private void StopFootstepAudio()
    {
        if (_isFootstepPlaying)
        {
            _footstepInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _footstepInstance.release();
            _isFootstepPlaying = false;
        }
    }

    private void OnDisable()
    {
        StopFootstepAudio();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopFootstepAudio();
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