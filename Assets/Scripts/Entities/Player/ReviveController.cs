using System.Collections;
using UnityEngine;

public class ReviveController : MonoBehaviour
{
    [SerializeField] private float reviveDuration = 4f;
    [SerializeField] private int healthAfterRevive = 20;
    [SerializeField] private float crawlSpeed = 1.5f;
    [SerializeField] private float maxReviveDistance = 2.0f;

    public bool IsDowned { get; private set; }
    public float CrawlSpeed => crawlSpeed;

    private Coroutine reviveCoroutine;
    private Player _player;
    private Rigidbody2D _rb;

    private Player currentReviver;
    private Vector2 reviveStartPosition;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody2D>();
    }

    public void GoDown()
    {
        if (IsDowned) return;

        IsDowned = true;
        _player.isHidden = true;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    public void StartBeingRevived(Player reviver)
    {
        if (!IsDowned || reviveCoroutine != null) return;

        currentReviver = reviver;
        reviveStartPosition = reviver.transform.position;

        reviveCoroutine = StartCoroutine(ReviveProcess());
    }

    public void StopBeingRevived()
    {
        if (reviveCoroutine != null)
        {
            StopCoroutine(reviveCoroutine);
            reviveCoroutine = null;
        }
    }

    private IEnumerator ReviveProcess()
    {
        float timer = 0f;

        // Loop until the duration is met
        while (timer < reviveDuration)
        {
            if (currentReviver != null)
            {
                // Check the squared distance
                float sqrDistance = ((Vector2)currentReviver.transform.position - reviveStartPosition).sqrMagnitude;

                if (sqrDistance > (maxReviveDistance * maxReviveDistance))
                {
                    Debug.Log("Reviver moved too far! Revive canceled.");
                    currentReviver.CancelMyReviveAction(); // Tell the reviver to cancel
                    yield break; // Exit the coroutine immediately
                }
            }
            else
            {
                yield break; // Failsafe in case the reviver is destroyed/null
            }

            timer += Time.deltaTime; // Advance the timer
            yield return null; // Wait for the next frame
        }

        // If the loop finishes without breaking, the revive is successful!
        IsDowned = false;
        _player.isHidden = false;
        _player.Health = healthAfterRevive;

        // Clean up
        currentReviver.CancelMyReviveAction(); // Resets the reviver's target
        currentReviver = null;
        reviveCoroutine = null;
    }
}