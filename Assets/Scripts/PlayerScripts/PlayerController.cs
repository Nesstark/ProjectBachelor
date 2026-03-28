using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    // ─── Serialized Fields ────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 120f;
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackOrigin;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    // ─── Private State ────────────────────────────────────────
    private Rigidbody rb;
    private Vector2 inputDir;
    private Vector3 moveDir;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 lastMoveDir;
    private float attackTimer;
    private bool isDead;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashDirX = Animator.StringToHash("DirX");
    private static readonly int HashDirZ = Animator.StringToHash("DirZ");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashAttack = Animator.StringToHash("Attack");

    private GameManager GM => GameManager.Instance;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastMoveDir = Vector3.forward;
        if (attackOrigin == null) attackOrigin = transform;

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 0f;
    }

    private void Start()
    {
        if (GM != null)
            GM.OnPlayerDied.AddListener(HandlePlayerDied);
        else
            Debug.LogError("[Player] GameManager not found!");

        if (enemyLayer.value == 0)
            Debug.LogWarning("[Player] enemyLayer not set — will hit ALL layers as fallback.");
    }

    private void OnDestroy()
    {
        GM?.OnPlayerDied.RemoveListener(HandlePlayerDied);
    }

    // ─────────────────────────────────────────────────────────
    // Input
    // ─────────────────────────────────────────────────────────
    public void OnMove(InputValue value)
    {
        if (!isDead) inputDir = value.Get<Vector2>();
    }

    public void OnDash(InputValue value)
    {
        if (isDead) return;
        if (value.isPressed && !isDashing && dashCooldownTimer <= 0f)
            StartDash();
    }

    public void OnAttack(InputValue value)
    {
        if (isDead) return;
        if (value.Get<float>() < 0.5f) return;
        if (attackTimer > 0f) return;

        attackTimer = attackCooldown;
        PerformAttack();
    }

    // ─────────────────────────────────────────────────────────
    // Update / FixedUpdate
    // ─────────────────────────────────────────────────────────
    private void Update()
    {
        if (isDead) return;

        dashTimer -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        if (isDashing && dashTimer <= 0f)
            isDashing = false;

        moveDir = new Vector3(inputDir.x, 0f, inputDir.y).normalized;
        if (moveDir.magnitude > 0.1f)
            lastMoveDir = moveDir;

        UpdateAnimator();
        UpdateSpriteFlip();

        CameraShakeManager.Instance?.SetRunningShake(rb.linearVelocity.magnitude, moveSpeed);
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (isDashing)
        {
            rb.linearVelocity = lastMoveDir * dashSpeed;
            return;
        }

        float speed = moveSpeed;
        Vector3 targetVelocity = moveDir * speed;
        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        float accel = moveDir.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, targetVelocity, accel * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
    }

    // ─────────────────────────────────────────────────────────
    // Attack
    // ─────────────────────────────────────────────────────────
    private void PerformAttack()
    {
        float range = GM != null ? GM.AttackRange : 3f;
        float damage = GM != null ? GM.Player.Damage : 20f;
        int mask = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        if (animator != null) animator.SetTrigger(HashAttack);

        Collider[] hits = Physics.OverlapSphere(attackOrigin.position, range, mask);
        if (hits.Length == 0) return;

        Collider closest = null;
        float bestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 toEnemy = hit.transform.position - transform.position;
            toEnemy.y = 0f;

            if (lastMoveDir.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(lastMoveDir.normalized, toEnemy.normalized);
                if (dot < 0.3f) continue;
            }

            float dist = toEnemy.magnitude;
            if (dist < bestDist) { bestDist = dist; closest = hit; }
        }

        if (closest == null) return;

        BaseEnemy enemy = closest.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.hitShakeForce);
            Debug.Log($"[Player] HIT '{closest.name}' for {damage:F1}");
            return;
        }

        Debug.LogWarning($"[Player] '{closest.name}' has no BaseEnemy component!");
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────
    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        if (animator != null) animator.SetTrigger(HashDash);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.dashShakeForce);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, moveDir.magnitude);
        animator.SetFloat(HashDirX, lastMoveDir.x);
        animator.SetFloat(HashDirZ, lastMoveDir.z);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        if (lastMoveDir.x > 0.1f) spriteRenderer.flipX = true;
        else if (lastMoveDir.x < -0.1f) spriteRenderer.flipX = false;
    }

    private void HandlePlayerDied()
    {
        if (isDead) return;
        isDead = true;
        rb.linearVelocity = Vector3.zero;
        inputDir = Vector2.zero;
        Debug.Log("[Player] Died — destroying in 0.5s");
        Destroy(gameObject, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        float range = Application.isPlaying && GM != null ? GM.AttackRange : 3f;
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);
    }
}