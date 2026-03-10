using UnityEngine;
using UnityEngine.AI;

// ============================================================
//  EnemyController.cs  —  NavMeshAgent version
//  Set the EnemyType dropdown in the Inspector per prefab.
//  Stats are fetched from GameManager at Start.
// ============================================================

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // ─── Enemy Type ───────────────────────────────────────────
    public enum EnemyType { Warrior, Archer, Elite }

    [Header("Enemy Type")]
    [Tooltip("Determines which stat block is fetched from GameManager")]
    [SerializeField] private EnemyType enemyType = EnemyType.Warrior;

    [Header("Attack Cycle")]
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float meleeRange = 1.5f;

    [Header("Optional")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    // ─── Runtime Stats ────────────────────────────────────────
    private EnemyStats _stats;

    // ─── Private ──────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Transform _playerTransform;
    private float _attackTimer;
    private bool _isDead;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttackCharge = Animator.StringToHash("AttackCharge");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDie = Animator.StringToHash("Die");

    private GameManager GM => GameManager.Instance;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Fetch stats from GameManager using the enum name as the key
        if (GM != null)
        {
            _stats = GM.GetEnemyStats(enemyType.ToString());
        }
        else
        {
            Debug.LogWarning($"[Enemy:{name}] GameManager not found — using fallback stats.");
            _stats = FallbackStats();
        }

        _agent.speed = _stats.Speed;
        _agent.stoppingDistance = meleeRange;

        // Stagger attack timers in a group
        _attackTimer = Random.Range(0f, attackInterval);

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _playerTransform = playerGO.transform;
        else
            Debug.LogWarning($"[Enemy:{name}] No GameObject tagged 'Player' found!");

        Debug.Log($"[Enemy:{name}] Type:{enemyType}  HP:{_stats.MaxHealth:F0}  " +
                  $"SPD:{_stats.Speed:F1}  DMG:{_stats.Damage:F1}");
    }

    // ─────────────────────────────────────────────────────────
    //  Update
    // ─────────────────────────────────────────────────────────
    private void Update()
    {
        if (_isDead || _playerTransform == null) return;

        ChasePlayer();
        TickAttackCycle();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // ─────────────────────────────────────────────────────────
    //  Chase
    // ─────────────────────────────────────────────────────────
    private void ChasePlayer()
    {
        _agent.SetDestination(_playerTransform.position);
    }

    // ─────────────────────────────────────────────────────────
    //  Attack Cycle
    // ─────────────────────────────────────────────────────────
    private void TickAttackCycle()
    {
        _attackTimer += Time.deltaTime;

        if (_attackTimer >= attackInterval)
        {
            _attackTimer = 0f;
            TryMeleeAttack();
        }
    }

    private void TryMeleeAttack()
    {
        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        if (dist <= meleeRange)
        {
            Debug.Log($"[Enemy:{name}({enemyType})] ATTACK — {_stats.Damage:F1} dmg");
            GM?.ApplyDamageToPlayer(_stats.Damage);
            if (animator != null) animator.SetTrigger(HashAttack);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _stats.CurrentHealth -= amount;
        Debug.Log($"[Enemy:{name}({enemyType})] Took {amount:F1} — HP:{_stats.CurrentHealth:F1}/{_stats.MaxHealth:F1}");

        if (spriteRenderer != null) StartCoroutine(FlashRoutine());

        if (_stats.CurrentHealth <= 0f) Die();
    }

    // ─────────────────────────────────────────────────────────
    //  Death
    // ─────────────────────────────────────────────────────────
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _agent.ResetPath();
        _agent.enabled = false;

        Debug.Log($"[Enemy:{name}({enemyType})] Died — awarding {_stats.XpReward:F0} XP");
        GM?.AwardXp(_stats.XpReward);

        RoomManager.Instance?.CurrentRoom?.OnEnemyDied();

        if (animator != null) animator.SetTrigger(HashDie);
        Destroy(gameObject, 0.15f);
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, _agent.velocity.magnitude);
        animator.SetFloat(HashAttackCharge, _attackTimer / attackInterval);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        float velX = _agent.velocity.x;
        if (velX > 0.1f) spriteRenderer.flipX = false;
        else if (velX < -0.1f) spriteRenderer.flipX = true;
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    private EnemyStats FallbackStats() => new EnemyStats
    {
        MaxHealth = 50f,
        CurrentHealth = 50f,
        Speed = 2.5f,
        Damage = 10f,
        XpReward = 25f
    };

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}