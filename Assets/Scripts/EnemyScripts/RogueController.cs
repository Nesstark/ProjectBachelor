using System.Collections;
using UnityEngine;

// ============================================================
// RogueController.cs — Rogue enemy type
// ── Behaviour ─────────────────────────────────────────────
// Flanks behind the player, dashes in to attack once,
// then waits on cooldown before repeating.
// Pure dash attacker — no melee outside of dash.
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
    [Tooltip("Cooldown after a dash attempt before the Rogue can dash again")]
    [SerializeField] private float dashCooldown = 3.0f;
    [Tooltip("Prefab for the ! exclamation ping (world-space UI or sprite)")]
    [SerializeField] private GameObject exclamationPingPrefab;
    [Tooltip("Offset above the Rogue's transform where the ping spawns")]
    [SerializeField] private Vector3 pingOffset = new Vector3(0f, 2.2f, 0f);
    [Tooltip("Uniform scale multiplier applied to the ping — 1 = prefab default, 2 = double size")]
    [SerializeField] private float pingScale = 1f;

    private enum RogueState { Flanking, DashTelegraph, Dashing, Cooldown }
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
            Debug.LogWarning("[Rogue] Could not find PlayerController — falling back to transform.forward.");
    }

    // ─── Tick ─────────────────────────────────────────────────
    // Completely block BaseEnemy's melee attack cycle —
    // the Rogue only ever attacks inside DashAttackSequence.

    protected override void TickAttackCycle()
    {
        // Only tick the timer when ready to attack (Flanking state)
        if (_state != RogueState.Flanking) return;

        AttackTimer += Time.deltaTime;
        if (AttackTimer >= attackInterval)
        {
            AttackTimer = 0f;
            TryAttack();
        }
    }

    // ─── Movement ─────────────────────────────────────────────

    protected override void HandleMovement()
    {
        // Frozen during telegraph and dash; free to walk during cooldown and flanking
        if (_state == RogueState.Dashing || _state == RogueState.DashTelegraph)
            return;

        if (PlayerTransform == null) return;

        if (IsBehindPlayer())
        {
            Agent.SetDestination(PlayerTransform.position);
        }
        else
        {
            Vector3 flankPos    = GetFlankPosition();
            Vector3 directPos   = PlayerTransform.position;
            Vector3 destination = Vector3.Lerp(directPos, flankPos, flankAggression);
            Agent.SetDestination(destination);
        }
    }

    // ─── Attack ───────────────────────────────────────────────
    // Only triggers a dash — never calls base.TryAttack().

    protected override void TryAttack()
    {
        // Block all attacks outside of Flanking state
        if (_state != RogueState.Flanking) return;
        if (_dashCoroutine != null) return;
        if (PlayerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        if (distToPlayer <= dashTriggerRange)
        {
            Debug.Log($"[Rogue] Within dash range ({distToPlayer:F2}) — starting dash!");
            _dashCoroutine = StartCoroutine(DashAttackSequence());
        }
    }

    // ─── Dash Attack Sequence ─────────────────────────────────

    private IEnumerator DashAttackSequence()
    {
        // ── Step 1: Telegraph — hard stop, face player ────────
        _state          = RogueState.DashTelegraph;
        Agent.isStopped = true;
        Agent.velocity  = Vector3.zero;

        Vector3 lookDir = (PlayerTransform.position - transform.position);
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // ── Step 2: Spawn ! ping ───────────────────────────────
        GameObject ping = null;
        if (exclamationPingPrefab != null)
        {
            ping = Instantiate(exclamationPingPrefab,
                transform.position + pingOffset, Quaternion.identity);
            ping.transform.SetParent(transform, worldPositionStays: true);
            ping.transform.localScale = Vector3.one * pingScale;
        }
        else
        {
            Debug.LogWarning("[Rogue] exclamationPingPrefab is NULL — assign it in the Inspector!");
        }

        // ── Step 3: Wind-up pause ──────────────────────────────
        yield return new WaitForSeconds(alertPauseDuration);
        if (ping != null) Destroy(ping);

        // ── Step 4: Dash toward the player ────────────────────
        _state          = RogueState.Dashing;
        Agent.isStopped = false;
        Agent.speed     = _baseAgentSpeed * dashSpeedMultiplier;
        Agent.SetDestination(PlayerTransform.position);

        float elapsed  = 0f;
        bool  hasHit   = false;  // only one hit allowed per dash

        while (elapsed < dashMaxDuration)
        {
            if (PlayerTransform == null) break;

            Agent.SetDestination(PlayerTransform.position);

            // Only deal damage once per dash, the moment we're close enough
            if (!hasHit)
            {
                float distNow = Vector3.Distance(transform.position, PlayerTransform.position);
                if (distNow <= dashLandDistance)
                {
                    hasHit = true;
                    Debug.Log($"[Rogue] DASH HIT — {Stats.Damage:F1} dmg");
                    GM?.ApplyDamageToPlayer(Stats.Damage);
                    if (animator != null) animator.SetTrigger(HashAssassinate);
                    // Don't break — let the dash finish its momentum naturally
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!hasHit)
            Debug.Log("[Rogue] Dash timed out — missed.");

        // ── Step 5: Reset speed, enter cooldown ───────────────
        Agent.speed     = _baseAgentSpeed;
        Agent.isStopped = false;
        _state          = RogueState.Cooldown;

        Debug.Log($"[Rogue] Cooldown — {dashCooldown:F1}s before next dash.");
        yield return new WaitForSeconds(dashCooldown);

        // ── Step 6: Ready to dash again ───────────────────────
        _state         = RogueState.Flanking;
        _dashCoroutine = null;
        Debug.Log("[Rogue] Cooldown done — back to flanking.");
    }

    // ─── Animator Override ────────────────────────────────────

    protected override void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, Agent.velocity.magnitude);
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
        float dot = Vector3.Dot(GetPlayerAimDir(), toRogue);
        return dot < -0.5f;
    }

    // ─── Gizmos ───────────────────────────────────────────────

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (PlayerTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(GetFlankPosition(), 0.3f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dashTriggerRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashLandDistance);
    }
}