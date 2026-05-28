using UnityEngine;

// ============================================================
//  ArcherController.cs  —  Archer enemy type
//  Inherits all shared logic from BaseEnemy.
//  Only defines what is UNIQUE: flee movement, ranged attack.
//
//  FIX: Added a self-contained _shootTimer so the archer fires
//  independently of whatever range/state check BaseEnemy uses
//  to call TryAttack(). The root cause was that BaseEnemy's
//  attack state machine likely requires the enemy to be within
//  melee range before transitioning to Attack state — an archer
//  never reaches melee range (it flees at fleeRange), so
//  TryAttack() was never being called by the base class.
// ============================================================

public class ArcherController : BaseEnemy
{
    protected override string EnemyTypeName => "Archer";

    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform  firePoint;
    [SerializeField] private float      preferredRange  = 8f;
    [SerializeField] private float      maxShootRange   = 12f;
    [SerializeField] private float      shootCooldown   = 1.5f; // seconds between shots

    [Header("Flee")]
    [SerializeField] private float fleeRange = 4f;
    [SerializeField] private float fleeSpeed = 6f;

    private float _shootTimer = 0f;

    private static readonly int HashFlee = Animator.StringToHash("Flee");

    // ─────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (firePoint == null) firePoint = transform;

        // Stagger initial shot so multiple archers in a room
        // don't all fire on the same frame.
        _shootTimer = Random.Range(0f, shootCooldown);
    }

    // ─── Movement — flee or reposition ───────────────────────
    protected override void HandleMovement()
    {
        if (PlayerTransform == null) return;

        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        if (dist < fleeRange)
        {
            // Too close — run away
            Agent.speed = fleeSpeed;
            Vector3 fleeDir    = (transform.position - PlayerTransform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * preferredRange;
            Agent.SetDestination(fleeTarget);
            if (animator != null) animator.SetBool(HashFlee, true);
        }
        else if (dist > preferredRange)
        {
            // Too far — move closer
            Agent.speed = Stats.Speed;
            Agent.SetDestination(PlayerTransform.position);
            if (animator != null) animator.SetBool(HashFlee, false);
        }
        else
        {
            // At preferred range — stop and face player
            Agent.speed = Stats.Speed;
            Agent.ResetPath();
            FacePlayer();
            if (animator != null) animator.SetBool(HashFlee, false);
        }

        // ── Self-contained shoot timer ────────────────────────
        // Ticks every frame regardless of BaseEnemy's state machine.
        // Guarantees the archer fires as long as the player is in
        // range, even if BaseEnemy never calls TryAttack().
        _shootTimer -= Time.deltaTime;
        if (_shootTimer <= 0f)
        {
            _shootTimer = shootCooldown;
            TryAttack();
        }
    }

    // ─── Attack — override base melee with ranged shot ───────
    // Also still called by BaseEnemy if its own cooldown fires,
    // but the self-contained timer above is the primary driver.
    protected override void TryAttack()
    {
        if (PlayerTransform == null) return;

        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        bool inShootRange = dist <= maxShootRange;
        bool notTooClose  = dist >= fleeRange;

        if (inShootRange && notTooClose)
            ShootProjectile();
    }

    // ─────────────────────────────────────────────────────────
    private void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Archer] No projectile prefab assigned!", this);
            return;
        }

        if (PlayerTransform == null) return;

        if (animator != null) animator.SetTrigger(HashAttack);

        AudioManager.Instance?.Play("EnemyFireball");

        Vector3 dir = (PlayerTransform.position - firePoint.position);
        dir.y = 0f;
        dir.Normalize();

        GameObject     proj  = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(dir, Stats.Damage, gameObject);

        Debug.Log($"[Archer] Fired projectile — DMG:{Stats.Damage:F1}");
    }

    // ─────────────────────────────────────────────────────────
    private void FacePlayer()
    {
        if (PlayerTransform == null) return;
        Vector3 dir = (PlayerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeRange);
    }
}