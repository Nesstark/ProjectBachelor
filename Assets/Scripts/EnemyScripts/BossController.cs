using UnityEngine;

// ============================================================
//  BossController.cs  —  Boss enemy type
//  ── Behaviour ─────────────────────────────────────────────
//   • Constantly fires projectiles at the player
//   • Also charges in for melee when close enough
//   • Two independent attack timers — shoot and melee run
//     simultaneously so the Boss is always threatening
// ============================================================

public class BossController : BaseEnemy
{
    protected override string EnemyTypeName => "Boss";

    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float shootInterval = 0.8f;   // shoots fast
    [SerializeField] private float maxShootRange = 20f;    // shoots from anywhere

    [Header("Melee Attack")]
    [SerializeField] private float meleeDamageMultiplier = 2f;   // hits harder up close

    [Header("Movement")]
    [SerializeField] private float chargeRange = 5f;             // rushes player when this close
    [SerializeField] private float chargeSpeed = 8f;

    // ─── Private ──────────────────────────────────────────────
    private float _shootTimer;

    private static readonly int HashShoot = Animator.StringToHash("Shoot");
    private static readonly int HashCharge = Animator.StringToHash("Charge");

    // ─────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (firePoint == null) firePoint = transform;
    }

    protected override void Start()
    {
        base.Start();
        _shootTimer = Random.Range(0f, shootInterval);
    }

    // ─── Update — run both timers independently ───────────────
    protected override void Update()
    {
        if (IsDead || PlayerTransform == null) return;

        HandleMovement();
        TickAttackCycle();    // melee — inherited timer
        TickShootCycle();     // ranged — separate timer
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // ─── Movement — charge when close, otherwise approach ─────
    protected override void HandleMovement()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        if (dist <= chargeRange)
        {
            // Charge at full speed
            Agent.speed = chargeSpeed;
            if (animator != null) animator.SetBool(HashCharge, true);
        }
        else
        {
            Agent.speed = Stats.Speed;
            if (animator != null) animator.SetBool(HashCharge, false);
        }

        Agent.SetDestination(PlayerTransform.position);
    }

    // ─── Melee — hits harder than base ────────────────────────
    protected override void TryAttack()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);
        if (dist > meleeRange) return;

        float meleeDmg = Stats.Damage * meleeDamageMultiplier;
        Debug.Log($"[Boss] MELEE — {meleeDmg:F1} dmg");
        GM?.ApplyDamageToPlayer(meleeDmg);
        if (animator != null) animator.SetTrigger(HashAttack);
    }

    // ─── Ranged — fires constantly on its own timer ───────────
    private void TickShootCycle()
    {
        _shootTimer += Time.deltaTime;

        if (_shootTimer >= shootInterval)
        {
            _shootTimer = 0f;
            ShootProjectile();
        }
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Boss] No projectile prefab assigned!");
            return;
        }

        if (animator != null) animator.SetTrigger(HashShoot);

        Vector3 dir = (PlayerTransform.position - firePoint.position);
        dir.y = 0f;
        dir.Normalize();

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(dir, Stats.Damage, gameObject);

        Debug.Log($"[Boss] SHOT — DMG:{Stats.Damage:F1}");
    }

    // ─── Gizmos ───────────────────────────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Charge range — orange
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeRange);

        // Max shoot range — red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);
    }
}