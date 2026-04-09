using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class BaseEnemy : MonoBehaviour
{
    [Header("Attack Cycle")]
    [SerializeField] protected float attackInterval = 1.5f;
    [SerializeField] protected float meleeRange = 1.5f;

    [Header("VFX")]
    [SerializeField] protected GameObject hitVFXPrefab;
    [SerializeField] protected GameObject deathVFXPrefab;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private GameObject slashVFXPrefab;
    private HitFlashHandler _hitFlash;

    [Header("Optional")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    protected static readonly int HashSpeed        = Animator.StringToHash("Speed");
    protected static readonly int HashAttackCharge = Animator.StringToHash("AttackCharge");
    protected static readonly int HashAttack       = Animator.StringToHash("Attack");
    protected static readonly int HashDie          = Animator.StringToHash("Die");

    protected EnemyStats   Stats;
    protected NavMeshAgent Agent;
    protected Transform    PlayerTransform;
    protected float        AttackTimer;
    protected bool         IsDead;

    private EnemyHealthBar _healthBar;

    protected GameManager GM => GameManager.Instance;

    protected virtual void Awake()
    {
        Agent     = GetComponent<NavMeshAgent>();
        _hitFlash = GetComponentInChildren<HitFlashHandler>();
    }

    protected virtual void Start()
    {
        Stats = GM != null ? GM.GetEnemyStats(EnemyTypeName) : FallbackStats();

        Agent.speed            = Stats.Speed;
        Agent.stoppingDistance = meleeRange;
        AttackTimer            = Random.Range(0f, attackInterval);

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            PlayerTransform = playerGO.transform;
        else
            Debug.LogWarning($"[{name}] No GameObject tagged 'Player' found!");

        _healthBar = GetComponentInChildren<EnemyHealthBar>();
        _healthBar?.Init(EnemyTypeName, Stats.MaxHealth);

        Debug.Log($"[{name}] Type:{EnemyTypeName} HP:{Stats.MaxHealth:F0} SPD:{Stats.Speed:F1} DMG:{Stats.Damage:F1}");
    }

    protected virtual void Update()
    {
        if (IsDead || PlayerTransform == null) return;
        if (!Agent.isOnNavMesh) return;

        HandleMovement();
        TickAttackCycle();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    protected abstract string EnemyTypeName { get; }
    protected abstract void HandleMovement();

    protected virtual void TickAttackCycle()
    {
        AttackTimer += Time.deltaTime;
        if (AttackTimer >= attackInterval)
        {
            AttackTimer = 0f;
            TryAttack();
        }
    }

    protected virtual void TryAttack()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);
        if (dist <= meleeRange)
        {
            Debug.Log($"[{name}] ATTACK — {Stats.Damage:F1} dmg to Player");
            GM?.ApplyDamageToPlayer(Stats.Damage);
            if (animator != null) animator.SetTrigger(HashAttack);
            
            if (slashVFXPrefab != null)
            {
                Transform origin = attackOrigin != null ? attackOrigin : transform;
                Vector3 dir = (PlayerTransform.position - transform.position);
                dir.y = 0f;
                float yAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Quaternion slashRot = Quaternion.Euler(0f, yAngle, 0f);
                GameObject slash = Instantiate(slashVFXPrefab, origin.position, slashRot, origin);
                Destroy(slash, 0.5f);
            }
        }
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;

        Stats.CurrentHealth -= amount;
        Debug.Log($"[{name}] Took {amount:F1} — HP:{Stats.CurrentHealth:F1}/{Stats.MaxHealth:F1}");

        _healthBar?.SetHealth(Stats.CurrentHealth, Stats.MaxHealth);
        _hitFlash?.Flash();

        AudioManager.Instance.Play("EnemyHit");

        if (hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(hitVFXPrefab, transform.position + Vector3.up * 0.7f, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.hitShakeForce);

        if (Stats.CurrentHealth <= 0f) Die();
    }

    protected virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Agent.ResetPath();
        Agent.enabled = false;

        Debug.Log($"[{name}] Died — awarding {Stats.XpReward:F0} XP");
        GM?.AwardXp(Stats.XpReward);

        RoomManager.Instance?.CurrentRoom?.OnEnemyDied();

        if (animator != null) animator.SetTrigger(HashDie);

        if (deathVFXPrefab != null)
            Instantiate(deathVFXPrefab, transform.position + Vector3.up * 0.7f, Quaternion.identity);

        Destroy(gameObject, 0.15f);
    }

    protected virtual void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed,        Agent.velocity.magnitude);
        animator.SetFloat(HashAttackCharge, AttackTimer / attackInterval);
    }

    protected virtual void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        float velX = Agent.velocity.x;
        if      (velX >  0.1f) spriteRenderer.flipX = false;
        else if (velX < -0.1f) spriteRenderer.flipX = true;
    }

    private EnemyStats FallbackStats() => new EnemyStats
    {
        MaxHealth = 50f, CurrentHealth = 50f,
        Speed = 2.5f, Damage = 10f, XpReward = 25f
    };

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}