using UnityEngine;
using UnityEngine.AI;

// ============================================================
//  EnemyController.cs  —  NavMeshAgent version
//  Attach to every Enemy prefab alongside a NavMeshAgent.
//  Remove the Rigidbody — NavMeshAgent handles all movement.
//
//  ── Behaviour ────────────────────────────────────────────
//   • Stats injected by EnemySpawner before Start() runs
//   • NavMeshAgent chases the player each frame
//   • Attack cycle charges while in stopping distance;
//     deals damage when fully charged and player is in range
//   • TakeDamage(float) called by PlayerController
//   • Awards XP and destroys self on death
// ============================================================

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Attack Cycle")]
    [Tooltip("Seconds between each attack attempt")]
    [SerializeField] private float attackInterval = 1.5f;

    [Tooltip("Distance at which the enemy stops and attacks (set this equal to NavMeshAgent Stopping Distance)")]
    [SerializeField] private float meleeRange = 1.5f;

    [Header("Optional")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    // ─── Live Stats (injected by EnemySpawner or set in Start) ──
    [HideInInspector] public EnemyStats Stats;

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
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // If stats weren't injected by the spawner, generate them now
        if (Stats == null || Stats.MaxHealth <= 0f)
            Stats = GM != null ? GM.CreateEnemyStats() : DefaultStats();

        // Push speed from stats into the agent
        _agent.speed = Stats.Speed;

        // Stopping distance should match meleeRange so the agent halts before swinging
        _agent.stoppingDistance = meleeRange;

        // Stagger first attacks so a group doesn't all fire at once
        _attackTimer = Random.Range(0f, attackInterval);

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            _playerTransform = playerGO.transform;
        else
            Debug.LogWarning("[Enemy] No GameObject tagged 'Player' found!");
    }

    // ─────────────────────────────────────────────────────────
    //  Update — chase + attack tick
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
    //  NavMesh Chase
    // ─────────────────────────────────────────────────────────
    private void ChasePlayer()
    {
        // Re-set destination every frame so the path stays current
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
            Debug.Log($"[Enemy:{name}] ATTACK — dealing {Stats.Damage:F1} to Player");
            GM?.ApplyDamageToPlayer(Stats.Damage);
            animator?.SetTrigger(HashAttack);
        }
        else
        {
            Debug.Log($"[Enemy:{name}] Cycle reset — player out of melee range ({dist:F2} > {meleeRange})");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Animator & Sprite
    // ─────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null) return;

        // velocity.magnitude gives 0 when stopped, full speed when moving
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

    // ─────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────
    /// <summary>Called by PlayerController when the player attacks.</summary>
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        Stats.CurrentHealth -= amount;
        Debug.Log($"[Enemy:{name}] Took {amount:F1} dmg. HP: {Stats.CurrentHealth:F1}/{Stats.MaxHealth:F1}");

        if (spriteRenderer != null)
            StartCoroutine(FlashRoutine());

        if (Stats.CurrentHealth <= 0f)
            Die();
    }

    // ─────────────────────────────────────────────────────────
    //  Death
    // ─────────────────────────────────────────────────────────
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Stop the agent immediately
        _agent.ResetPath();
        _agent.enabled = false;

        Debug.Log($"[Enemy:{name}] Died — awarding {Stats.XpReward:F0} XP");
        GM?.AwardXp(Stats.XpReward);

        animator?.SetTrigger(HashDie);

        Destroy(gameObject, 0.15f);
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────
    private System.Collections.IEnumerator FlashRoutine()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    private EnemyStats DefaultStats() => new EnemyStats
    {
        MaxHealth = 50f,
        CurrentHealth = 50f,
        Speed = 2.5f,
        Damage = 10f,
        XpReward = 30f
    };

    // ─────────────────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}