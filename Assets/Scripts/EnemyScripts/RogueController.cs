using System.Collections;
using UnityEngine;

// ============================================================
// RogueController.cs — Rogue enemy type
// ── Behaviour ─────────────────────────────────────────────
// FAR:   Tries to circle behind the player before attacking
// CLOSE: Stops caring about positioning, attacks directly
// FLANK: When positioned behind the player (mouse-facing),
//        shows a ! ping, freezes to wind up, then bursts
//        forward. Damage resolves on contact.
// ============================================================

public class RogueController : BaseEnemy
{
    protected override string EnemyTypeName => "Rogue";

    [Header("Rogue Settings")]
    [Tooltip("Distance at which the Rogue stops flanking and just attacks")]
    [SerializeField] private float assassinRange = 2.5f;
    [Tooltip("How far behind the player the Rogue tries to position")]
    [SerializeField] private float flankDistance = 3f;
    [Tooltip("How strongly the Rogue prioritises circling behind vs. walking straight in. 0 = straight in, 1 = always flank")]
    [SerializeField] [Range(0f, 1f)] private float flankAggression = 0.85f;

    [Header("Dash Attack")]
    [Tooltip("Speed multiplier applied to the NavMeshAgent during the dash")]
    [SerializeField] private float dashSpeedMultiplier = 10f;
    [Tooltip("Max time the dash can run before giving up (safety cap)")]
    [SerializeField] private float dashMaxDuration = 2.0f;
    [Tooltip("How long the Rogue freezes with the ! ping before dashing (wind-up)")]
    [SerializeField] private float alertPauseDuration = 0.7f;
    [Tooltip("Damage multiplier for a successful dash backstab")]
    [SerializeField] private float dashBackstabMultiplier = 2f;
    [Tooltip("How close the Rogue must get to the player for the dash to land")]
    [SerializeField] private float dashLandDistance = 1.8f;
    [Tooltip("Prefab for the ! exclamation ping (world-space UI or sprite)")]
    [SerializeField] private GameObject exclamationPingPrefab;
    [Tooltip("Offset above the Rogue's transform where the ping spawns")]
    [SerializeField] private Vector3 pingOffset = new Vector3(0f, 2.2f, 0f);

    private enum RogueState { Flanking, Assassinating, DashTelegraph, Dashing }
    private RogueState _state = RogueState.Flanking;

    private float _baseAgentSpeed;
    private Coroutine _dashCoroutine = null;

    // Cached reference to the PlayerController so we can read aimDir
    private PlayerController _playerController;

    private static readonly int HashAssassinate = Animator.StringToHash("Assassinate");

    // ─── Init ─────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
        _baseAgentSpeed = Agent.speed;

        // Grab the PlayerController so we can read the mouse aim direction
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

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        if (distToPlayer <= assassinRange)
        {
            // Close enough — go straight for the player
            _state = RogueState.Assassinating;
            Agent.SetDestination(PlayerTransform.position);
        }
        else
        {
            _state = RogueState.Flanking;

            // Blend between the flank point and the player's position based
            // on flankAggression. At 1.0 the Rogue always tries to get behind
            // the player; at 0.0 it just walks straight in.
            Vector3 flankPos  = GetFlankPosition();
            Vector3 directPos = PlayerTransform.position;
            Vector3 destination = Vector3.Lerp(directPos, flankPos, flankAggression);

            Agent.SetDestination(destination);
        }
    }

    // ─── Attack ───────────────────────────────────────────────

    protected override void TryAttack()
    {
        if (_dashCoroutine != null) return;
        if (_state == RogueState.DashTelegraph || _state == RogueState.Dashing) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        if (_state == RogueState.Assassinating)
        {
            if (distToPlayer > meleeRange) return;

            float bonusDamage = Stats.Damage * 2f;
            Debug.Log($"[Rogue] ASSASSINATE — {bonusDamage:F1} dmg (2x bonus)");
            GM?.ApplyDamageToPlayer(bonusDamage);
            if (animator != null) animator.SetTrigger(HashAssassinate);
        }
        else if (IsBehindPlayer())
        {
            // Behind the player's AIM direction — trigger dash from any distance
            Debug.Log($"[Rogue] Behind player aim — DistToPlayer: {distToPlayer:F2} — triggering dash!");
            _dashCoroutine = StartCoroutine(DashAttackSequence());
        }
        else
        {
            if (distToPlayer > meleeRange) return;
            Debug.Log($"[Rogue] ATTACK (frontal) — {Stats.Damage:F1} dmg");
            GM?.ApplyDamageToPlayer(Stats.Damage);
            if (animator != null) animator.SetTrigger(HashAttack);
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
            // Player was destroyed mid-dash (e.g. they died) — abort cleanly
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
                float dashDamage = Stats.Damage * dashBackstabMultiplier;
                Debug.Log($"[Rogue] DASH BACKSTAB — {dashDamage:F1} dmg ({dashBackstabMultiplier}x bonus)");
                GM?.ApplyDamageToPlayer(dashDamage);
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

    // ─── Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns the player's current aim direction (mouse-driven).
    /// Falls back to transform.forward if PlayerController isn't available.
    /// </summary>
    private Vector3 GetPlayerAimDir()
    {
        if (_playerController != null)
            return _playerController.AimDir;

        Vector3 fallback = PlayerTransform.forward;
        fallback.y = 0f;
        return fallback.normalized;
    }

    /// <summary>Returns a point directly behind the player's AIM direction.</summary>
    private Vector3 GetFlankPosition()
    {
        Vector3 aimDir = GetPlayerAimDir();
        return PlayerTransform.position - aimDir * flankDistance;
    }

    /// <summary>
    /// Returns true if the Rogue is behind the player's AIM direction.
    /// Uses a dot product — negative means the Rogue is in the rear 120° arc.
    /// </summary>
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

        // Assassin range — orange
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, assassinRange);

        // Flank target — magenta
        if (PlayerTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(GetFlankPosition(), 0.3f);
        }

        // Dash land distance — cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashLandDistance);
    }
}