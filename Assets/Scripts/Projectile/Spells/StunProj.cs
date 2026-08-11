using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StunProj : Projectile
{
    [Header("Chain Lightning Settings")]
    [SerializeField] private int maxChainTargets = 3;
    [SerializeField] private float chainRadius = 4.5f;

    [Header("Visual Effects")]
    [SerializeField] private Color beamColorStart = new Color(0.2f, 0.8f, 1f); // Electric Cyan
    [SerializeField] private Color beamColorEnd = Color.white;
    [SerializeField] private float beamDuration = 1.0f;

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer)
        {
            base.OnTriggerEnter2D(collision);
            return;
        }

        Enemy hitEnemy = collision.GetComponentInParent<Enemy>();
        if (hitEnemy != null)
        {
            ExecuteChainLightning(hitEnemy);
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    private void ExecuteChainLightning(Enemy initialEnemy)
    {
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        // 1. Hit the initial enemy
        OnHitEnemy(initialEnemy);
        hitEnemies.Add(initialEnemy);

        Enemy currentSource = initialEnemy;
        int chainsRemaining = maxChainTargets;

        // 2. Iteratively chain to the next closest enemy within chainRadius
        while (chainsRemaining > 0)
        {
            Enemy nextTarget = FindNextChainTarget(currentSource.transform.position, hitEnemies);
            if (nextTarget == null)
            {
                break; // No more unhit enemies in range
            }

            OnHitEnemy(nextTarget);
            DrawChainLightningBeamRpc(currentSource.transform.position, nextTarget.transform.position);
            hitEnemies.Add(nextTarget);
            currentSource = nextTarget;
            chainsRemaining--;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DrawChainLightningBeamRpc(Vector3 startPos, Vector3 endPos)
    {
        GameObject lineObj = new GameObject("ChainLightningBeam");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        lr.material = new Material(shader);
        lr.sortingOrder = 15;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(beamColorStart, 0f), new GradientColorKey(beamColorEnd, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        lr.colorGradient = gradient;

        lr.startWidth = 0.25f;
        lr.endWidth = 0.08f;
        lr.positionCount = 2;
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, endPos);

        lineObj.AddComponent<ChainBeamFader>().Init(lr, beamDuration);
    }

    private Enemy FindNextChainTarget(Vector3 searchCenter, HashSet<Enemy> alreadyHit)
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(searchCenter, chainRadius);
        Enemy closestEnemy = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Collider2D col in candidates)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && !alreadyHit.Contains(enemy) && enemy.Health > 0)
            {
                float distSqr = (enemy.transform.position - searchCenter).sqrMagnitude;
                if (distSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closestEnemy = enemy;
                }
            }
        }

        return closestEnemy;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, chainRadius);
    }
}

public class ChainBeamFader : MonoBehaviour
{
    private LineRenderer lr;
    private float duration;

    public void Init(LineRenderer lineRenderer, float durationTime)
    {
        lr = lineRenderer;
        duration = durationTime;
        StartCoroutine(FadeAndDestroy());
    }

    private System.Collections.IEnumerator FadeAndDestroy()
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            if (lr != null)
            {
                lr.startWidth = Mathf.Lerp(0.25f, 0f, t);
                lr.endWidth = Mathf.Lerp(0.08f, 0f, t);
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
