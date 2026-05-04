using System.Collections;
using UnityEngine;

// ============================================================
// RogueController.cs — Rogue enemy type
// ── Behaviour ─────────────────────────────────────────────
// Tries to get behind the player, then closes to dash range
// and fires a dash attack. Pure dash attacker — no melee.
// High damage, squishy.
// ============================================================

public class RogueController : BaseEnemy
{
    protected override string EnemyTypeName => "Rogue";

    [Header("Rogue Settings")]
    [Tooltip("How far behind the player the Rogue tries to position")]
    [SerializeField] private float flankDistance = 3f;
    [Tooltip("How strongly the Rogue prioritises circling behind vs. walking straight in. 0 = straight in, 1 = always flank")]
    [SerializeField] [Range(0f, 1f)] private float flankAggression = 0.85f;

    [Header("Dash Attack")]
    [Tooltip("Rogue must be within this distance to trigger the dash")]
    [SerializeField] private float dashTriggerRange = 4f;
    [Tooltip("Speed multiplier applied to the NavMeshAgent during the dash")]
    [SerializeField] private float dashSpeedMultiplier = 10f;
    [Tooltip("Max time the dash can run before giving up (safety cap)")]
    [SerializeField] private float dashMaxDuration = 3.0f;
    [Tooltip("How long the Rogue freezes with the ! ping before dashing (wind-up)")]
    [SerializeField] private float alertPauseDuration = 0.7f;
    [Tooltip("How close the Rogue must get to the player for the dash to land")]
    [SerializeField] private float dashLandDistance = 1.8f;
    [Tooltip("Prefab for the ! exclamation ping (world-space UI or sprite)")]
    [SerializeField] private GameObject exclamationPingPrefab;
    [Tooltip("Offset above the Rogue's transform where the ping spawns")]
    [SerializeField] private Vector3 pingOffset = new Vector3(0f, 2.2f, 0f);

    private enum RogueState { Flanking, DashTelegraph, Dashing }
    private RogueState _state = RogueState.Flanking;

    private float _baseAgentSpeed;
    private Coroutine _dashCoroutine = null;

    private PlayerController _playerController;

    private static readonly int HashAssassinate = Animator.StringToHash("Assassinate");

    // ─── Init ─────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
        _baseAgentSpeed = Agent.speed;

        if (PlayerTransform != null)
            _playerController = PlayerTransform.GetComponent<PlayerController>();

        if (_playerController == null)
            Debug.LogWarning("[Rogue] Could not find PlayerController — falling back to transform.forward for aim.");
    }

    // ─── Tick ─────────────────────────────────────────────────

    protected override void TickAttackCycle()
    {
        if (_state == RogueState.DashTelegraph || _state == RogueState.Dashing)
            return;

        base.TickAttackCycle();
    }

    // ─── Movement ─────────────────────────────────────────────

    protected override void HandleMovement()
    {
        if (_state == RogueState.Dashing || _state == RogueState.DashTelegraph)
            return;

        if (PlayerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        if (IsBehindPlayer())
        {
            // Already behind — run straight at the player to close the gap
            Agent.SetDestination(PlayerTransform.position);
        }
        else
        {
            // Not behind yet — circle toward the flank position
            Vector3 flankPos    = GetFlankPosition();
            Vector3 directPos   = PlayerTransform.position;
            Vector3 destination = Vector3.Lerp(directPos, flankPos, flankAggression);
            Agent.SetDestination(destination);
        }

        _state = RogueState.Flanking;
    }

    // ─── Attack ───────────────────────────────────────────────

    protected override void TryAttack()
    {
        if (_dashCoroutine != null) return;
        if (_state == RogueState.DashTelegraph || _state == RogueState.Dashing) return;
        if (PlayerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        // Dash whenever within range — behind the player or not
        if (distToPlayer <= dashTriggerRange)
        {
            Debug.Log($"[Rogue] Within dash range ({distToPlayer:F2}) — DASHING!");
            _dashCoroutine = StartCoroutine(DashAttackSequence());
        }
        else
        {
            // Too far — keep running in, do nothing until close enough
            Debug.Log($"[Rogue] Closing gap — DistToPlayer: {distToPlayer:F2}");
        }
    }

    // ─── Dash Attack Sequence ─────────────────────────────────

    private IEnumerator DashAttackSequence()
    {
        Debug.Log("[Rogue] DashAttackSequence STARTED");
        _state = RogueState.DashTelegraph;

        // ── Step 1: Hard stop + face the player ───────────────
        Agent.isStopped = true;
        Agent.velocity = Vector3.zero;

        Vector3 lookDir = (PlayerTransform.position - transform.position);
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // ── Step 2: Spawn the ! ping ───────────────────────────
        GameObject ping = null;
        if (exclamationPingPrefab != null)
        {
            ping = Instantiate(exclamationPingPrefab, transform.position + pingOffset, Quaternion.identity);
            ping.transform.SetParent(transform, worldPositionStays: true);
            Debug.Log("[Rogue] Ping spawned!");
        }
        else
        {
            Debug.LogWarning("[Rogue] exclamationPingPrefab is NULL — assign it in the Inspector!");
        }

        // ── Step 3: Wind-up pause ──────────────────────────────
        yield return new WaitForSeconds(alertPauseDuration);

        if (ping != null) Destroy(ping);

        // ── Step 4: Burst dash toward the player ──────────────
        _state = RogueState.Dashing;
        Agent.isStopped = false;
        Agent.speed = _baseAgentSpeed * dashSpeedMultiplier;
        Agent.SetDestination(PlayerTransform.position);

        Debug.Log($"[Rogue] DASHING — speed: {Agent.speed:F1}");

        float elapsed = 0f;
        bool landed = false;

        while (elapsed < dashMaxDuration)
        {
            if (PlayerTransform == null)
            {
                Debug.Log("[Rogue] Player destroyed during dash — aborting.");
                break;
            }

            Agent.SetDestination(PlayerTransform.position);

            float distNow = Vector3.Distance(transform.position, PlayerTransform.position);
            if (distNow <= dashLandDistance)
            {
                landed = true;
                // Base damage only — high damage, no multiplier
                Debug.Log($"[Rogue] DASH HIT — {Stats.Damage:F1} dmg");
                GM?.ApplyDamageToPlayer(Stats.Damage);
                if (animator != null) animator.SetTrigger(HashAssassinate);
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!landed)
            Debug.Log("[Rogue] Dash timed out — missed!");

        // ── Reset ──────────────────────────────────────────────
        Agent.speed = _baseAgentSpeed;
        Agent.isStopped = false;
        _state = RogueState.Flanking;
        _dashCoroutine = null;
    }

    // ─── Animator Override ────────────────────────────────────

    protected override void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, Agent.velocity.magnitude);
        // AttackCharge skipped — not in Rogue's Animator
    }

    // ─── Helpers ──────────────────────────────────────────────

    private Vector3 GetPlayerAimDir()
    {
        if (_playerController != null)
            return _playerController.AimDir;

        Vector3 fallback = PlayerTransform.forward;
        fallback.y = 0f;
        return fallback.normalized;
    }

    private Vector3 GetFlankPosition()
    {
        Vector3 aimDir = GetPlayerAimDir();
        return PlayerTransform.position - aimDir * flankDistance;
    }

    private bool IsBehindPlayer()
    {
        Vector3 toRogue = (transform.position - PlayerTransform.position).normalized;
        toRogue.y = 0f;

        Vector3 aimDir = GetPlayerAimDir();

        float dot = Vector3.Dot(aimDir, toRogue);
        return dot < -0.5f;
    }

    // ─── Gizmos ───────────────────────────────────────────────

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Flank target — magenta
        if (PlayerTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(GetFlankPosition(), 0.3f);
        }

        // Dash trigger range — yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dashTriggerRange);

        // Dash land distance — cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashLandDistance);
    }
}