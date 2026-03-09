using UnityEngine;
using UnityEngine.AI;

// ============================================================
//  ArcherController.cs
//  Attach this INSTEAD of EnemyController on the Archer prefab.
//  ── Behaviour ─────────────────────────────────────────────
//   • Keeps a preferred distance from the player
//   • Shoots a projectile sphere on its attack cycle
//   • Flees when the player gets inside fleeRange
// ============================================================

[RequireComponent(typeof(NavMeshAgent))]
public class ArcherController : MonoBehaviour
{
    // ─── Enemy Type ───────────────────────────────────────────
    // Hard-coded to Archer so GameManager always returns Archer stats
    private const string EnemyTypeName = "Archer";

    // ─── Inspector ────────────────────────────────────────────
    [Header("Ranged Attack")]
    [Tooltip("Projectile prefab — assign a sphere with ArrowProjectile attached")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;          // null → uses transform
    [SerializeField] private float attackInterval = 2f;
    [SerializeField] private float preferredRange = 8f;   // ideal shooting distance
    [SerializeField] private float maxShootRange = 12f;  // won't shoot beyond this

    [Header("Flee")]
    [SerializeField] private float fleeRange = 4f;   // starts running when player closer than this
    [SerializeField] private float fleeSpeed = 6f;   // speed while fleeing (overrides stat speed)

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
    private bool _isFleeing;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashFlee = Animator.StringToHash("Flee");
    private static readonly int HashDie = Animator.StringToHash("Die");

    private GameManager GM => GameManager.Instance;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (firePoint == null) firePoint = transform;
    }

    private void Start()
    {
        _stats = GM != null ? GM.GetEnemyStats(EnemyTypeName) : FallbackStats();

        _agent.speed = _stats.Speed;
        _agent.stoppingDistance = 0f;   // Archer manages its own stopping logic

        _attackTimer = Random.Range(0f, attackInterval);

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _playerTransform = playerGO.transform;
        else
            Debug.LogWarning("[Archer] No GameObject tagged 'Player' found!");

        Debug.Log($"[Archer] Stats — HP:{_stats.MaxHealth:F0} SPD:{_stats.Speed:F1} DMG:{_stats.Damage:F1}");
    }

    // ─────────────────────────────────────────────────────────
    //  Update
    // ─────────────────────────────────────────────────────────
    private void Update()
    {
        if (_isDead || _playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        HandleMovement(dist);
        TickAttackCycle(dist);
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // ─────────────────────────────────────────────────────────
    //  Movement — flee or reposition to preferred range
    // ─────────────────────────────────────────────────────────
    private void HandleMovement(float distToPlayer)
    {
        if (distToPlayer < fleeRange)
        {
            // Player too close — run directly away
            _isFleeing = true;
            _agent.speed = fleeSpeed;

            Vector3 fleeDir = (transform.position - _playerTransform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * preferredRange;
            _agent.SetDestination(fleeTarget);

            if (animator != null) animator.SetBool(HashFlee, true);
        }
        else if (distToPlayer > preferredRange)
        {
            // Too far — move closer until inside preferredRange
            _isFleeing = false;
            _agent.speed = _stats.Speed;
            _agent.SetDestination(_playerTransform.position);

            if (animator != null) animator.SetBool(HashFlee, false);
        }
        else
        {
            // Already at preferred range — stop and face player
            _isFleeing = false;
            _agent.speed = _stats.Speed;
            _agent.ResetPath();

            FacePlayer();
            if (animator != null) animator.SetBool(HashFlee, false);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Attack Cycle — only fires when in range and not fleeing
    // ─────────────────────────────────────────────────────────
    private void TickAttackCycle(float distToPlayer)
    {
        _attackTimer += Time.deltaTime;

        if (_attackTimer >= attackInterval)
        {
            _attackTimer = 0f;

            bool inShootRange = distToPlayer <= maxShootRange;
            bool notTooClose = distToPlayer >= fleeRange;

            if (inShootRange && notTooClose)
                ShootProjectile();
            else
                Debug.Log($"[Archer] Skipped shot — dist:{distToPlayer:F1} inRange:{inShootRange} notClose:{notTooClose}");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Shoot
    // ─────────────────────────────────────────────────────────
    private void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Archer] No projectile prefab assigned!");
            return;
        }

        if (animator != null) animator.SetTrigger(HashAttack);

        // 3D direction on X/Z plane toward player
        Vector3 dir = (_playerTransform.position - firePoint.position);
        dir.y = 0f;
        dir.Normalize();

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(dir, _stats.Damage);

        Debug.Log($"[Archer] Fired projectile. DMG:{_stats.Damage:F1}");
    }

    // ─────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _stats.CurrentHealth -= amount;
        Debug.Log($"[Archer] Took {amount:F1} — HP:{_stats.CurrentHealth:F1}/{_stats.MaxHealth:F1}");

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

        Debug.Log($"[Archer] Died — awarding {_stats.XpReward:F0} XP");
        GM?.AwardXp(_stats.XpReward);

        if (animator != null) animator.SetTrigger(HashDie);
        Destroy(gameObject, 0.15f);
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────
    private void FacePlayer()
    {
        Vector3 dir = (_playerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, _agent.velocity.magnitude);
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
        MaxHealth = 40f,
        CurrentHealth = 40f,
        Speed = 4f,
        Damage = 10f,
        XpReward = 25f
    };

    private void OnDrawGizmosSelected()
    {
        // Flee range — red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeRange);

        // Preferred range — yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredRange);

        // Max shoot range — blue
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);
    }
}