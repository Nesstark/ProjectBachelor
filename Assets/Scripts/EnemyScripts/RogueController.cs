using UnityEngine;

// ============================================================
//  RogueController.cs  —  Rogue enemy type
//  ── Behaviour ─────────────────────────────────────────────
//   FAR:   Tries to circle behind the player before attacking
//   CLOSE: Stops caring about positioning, attacks directly
// ============================================================

public class RogueController : BaseEnemy
{
    protected override string EnemyTypeName => "Rogue";

    [Header("Rogue Settings")]
    [Tooltip("Distance at which the Rogue stops flanking and just attacks")]
    [SerializeField] private float assassinRange  = 2.5f;
    [Tooltip("How far behind the player the Rogue tries to position")]
    [SerializeField] private float flankDistance  = 3f;
    [Tooltip("How close the Rogue needs to get to its flank position before attacking")]
    [SerializeField] private float flankTolerance = 1.2f;

    private enum RogueState { Flanking, Assassinating }
    private RogueState _state = RogueState.Flanking;

    private static readonly int HashAssassinate = Animator.StringToHash("Assassinate");


    // ─── Movement ─────────────────────────────────────────────


    protected override void HandleMovement()
    {
        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        if (distToPlayer <= assassinRange)
        {
            // Close enough — switch to assassin mode, go straight for the player
            _state = RogueState.Assassinating;
            Agent.SetDestination(PlayerTransform.position);
        }
        else
        {
            // Still far — try to get behind the player
            _state = RogueState.Flanking;
            Agent.SetDestination(GetFlankPosition());
        }
    }


    // ─── Attack ───────────────────────────────────────────────


    protected override void TryAttack()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);
        if (dist > meleeRange) return;

        if (_state == RogueState.Assassinating)
        {
            // Assassination — bonus damage when close regardless of angle
            float bonusDamage = Stats.Damage * 2f;
            Debug.Log($"[Rogue] ASSASSINATE — {bonusDamage:F1} dmg (2x bonus)");
            GM?.ApplyDamageToPlayer(bonusDamage);
            if (animator != null) animator.SetTrigger(HashAssassinate);
        }
        else if (IsBehindPlayer())
        {
            Vector3 flankTarget = GetFlankPosition();
            bool reachedFlank   = Vector3.Distance(transform.position, flankTarget) <= flankTolerance;

            if (reachedFlank)
            {
                // Successfully flanked — backstab bonus
                float backstabDamage = Stats.Damage * 1.5f;
                Debug.Log($"[Rogue] BACKSTAB — {backstabDamage:F1} dmg (1.5x bonus)");
                GM?.ApplyDamageToPlayer(backstabDamage);
                if (animator != null) animator.SetTrigger(HashAttack);
            }
            else
            {
                // Behind player but not yet in position — regular hit
                Debug.Log($"[Rogue] ATTACK — {Stats.Damage:F1} dmg");
                GM?.ApplyDamageToPlayer(Stats.Damage);
                if (animator != null) animator.SetTrigger(HashAttack);
            }
        }
        else
        {
            // Frontal attack — still trying to flank but player is facing us
            Debug.Log($"[Rogue] ATTACK — {Stats.Damage:F1} dmg");
            GM?.ApplyDamageToPlayer(Stats.Damage);
            if (animator != null) animator.SetTrigger(HashAttack);
        }
    }


    // ─── Helpers ──────────────────────────────────────────────


    /// <summary>Returns a point directly behind the player.</summary>
    private Vector3 GetFlankPosition()
    {
        Vector3 playerForward = PlayerTransform.forward;
        playerForward.y = 0f;
        return PlayerTransform.position - playerForward.normalized * flankDistance;
    }

    /// <summary>Returns true if the Rogue is behind the player (dot product check).</summary>
    private bool IsBehindPlayer()
    {
        Vector3 toRogue = (transform.position - PlayerTransform.position).normalized;
        toRogue.y = 0f;

        Vector3 playerForward = PlayerTransform.forward;
        playerForward.y = 0f;

        // Negative dot = behind the player
        float dot = Vector3.Dot(playerForward.normalized, toRogue.normalized);
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
    }
}