using UnityEngine;

// ============================================================
//  EnemyController.cs
//  Attach to every Enemy prefab.
//  ── Behaviour ────────────────────────────────────────────
//   • Stats are initialised from GameManager.CreateEnemyStats()
//   • Chases the player continuously (NavMesh or direct move)
//   • Attack cycle: charges for AttackInterval seconds, then
//     deals damage if player is within MeleeRange
//   • TakeDamage(float) → called by PlayerController
//   • Destroys itself when health ≤ 0, awards XP to GM
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Attack Cycle")]
    [Tooltip("Seconds between each attack attempt")]
    [SerializeField] private float attackInterval = 1.5f;

    [Tooltip("Melee reach in world units")]
    [SerializeField] private float meleeRange = 1.2f;

    [Header("Optional")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    // ─── Live Stats (set by EnemySpawner or Awake) ───────────
    [HideInInspector] public EnemyStats Stats;

    // ─── Private ──────────────────────────────────────────────
    private Rigidbody2D _rb;
    private Transform _playerTransform;
    private float _attackTimer;       // counts UP toward attackInterval
    private bool _isDead = false;

    private GameManager GM => GameManager.Instance;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // If stats weren't injected by the spawner, generate them now
        if (Stats == null || Stats.MaxHealth <= 0f)
            Stats = GM != null ? GM.CreateEnemyStats()
                               : DefaultStats();

        _attackTimer = Random.Range(0f, attackInterval); // stagger first attacks

        // Cache player reference
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerTransform = playerGO.transform;
        else Debug.LogWarning("[Enemy] No GameObject tagged 'Player' found!");
    }

    // ─── Frame ────────────────────────────────────────────────
    private void Update()
    {
        if (_isDead || _playerTransform == null) return;

        MoveTowardPlayer();
        TickAttackCycle();
    }

    // ─── Movement ─────────────────────────────────────────────
    private void MoveTowardPlayer()
    {
        Vector2 dir = ((Vector2)_playerTransform.position - _rb.position).normalized;
        _rb.linearVelocity = dir * Stats.Speed;

        if (spriteRenderer != null)
            spriteRenderer.flipX = dir.x < 0f;
    }

    // ─── Attack Cycle ─────────────────────────────────────────
    /// <summary>
    /// Increments the attack charge timer. When full, attempts a melee hit
    /// if the player is within meleeRange, then resets.
    /// The "charge" arc fills over attackInterval seconds — wire this float
    /// to a UI arc or animator for visual feedback.
    /// </summary>
    private void TickAttackCycle()
    {
        _attackTimer += Time.deltaTime;

        if (animator != null)
            animator.SetFloat("AttackCharge", _attackTimer / attackInterval);

        if (_attackTimer >= attackInterval)
        {
            _attackTimer = 0f;
            TryMeleeAttack();
        }
    }

    private void TryMeleeAttack()
    {
        if (_playerTransform == null) return;
        float dist = Vector2.Distance(transform.position, _playerTransform.position);

        if (dist <= meleeRange)
        {
            Debug.Log($"[Enemy:{name}] ATTACK — dealing {Stats.Damage:F1} to Player");
            GM?.ApplyDamageToPlayer(Stats.Damage);

            if (animator != null) animator.SetTrigger("Attack");
        }
        else
        {
            Debug.Log($"[Enemy:{name}] Attack cycle reset — player out of melee range ({dist:F2} > {meleeRange})");
        }
    }

    // ─── Public API ───────────────────────────────────────────
    /// <summary>Called by PlayerController when the player attacks.</summary>
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        Stats.CurrentHealth -= amount;
        Debug.Log($"[Enemy:{name}] Took {amount:F1} dmg. HP: {Stats.CurrentHealth:F1}/{Stats.MaxHealth:F1}");

        // Flash red
        if (spriteRenderer != null)
            StartCoroutine(FlashRoutine());

        if (Stats.CurrentHealth <= 0f)
            Die();
    }

    // ─── Death ────────────────────────────────────────────────
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _rb.linearVelocity = Vector2.zero;
        Debug.Log($"[Enemy:{name}] Died — awarding {Stats.XpReward:F0} XP");

        GM?.AwardXp(Stats.XpReward);

        if (animator != null) animator.SetTrigger("Die");

        // Give death animation a frame before destroying
        Destroy(gameObject, 0.15f);
    }

    // ─── Flash Coroutine ──────────────────────────────────────
    private System.Collections.IEnumerator FlashRoutine()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    // ─── Default fallback stats ───────────────────────────────
    private EnemyStats DefaultStats() => new EnemyStats
    {
        MaxHealth = 50f,
        CurrentHealth = 50f,
        Speed = 2.5f,
        Damage = 10f,
        XpReward = 30f
    };

    // ─── Gizmos ───────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}