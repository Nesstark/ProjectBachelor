using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  PlayerController.cs  —  Driven by InputSystem_Actions
//  Implements IPlayerActions so the generated asset wires
//  every callback automatically via AddCallbacks(this).
//
//  Setup:
//   • Attach to the Player GameObject
//   • Assign enemyLayer to your Enemy physics layer
//   • Tag the Player GameObject as "Player"
//   • Rigidbody2D required (Gravity Scale = 0 for top-down)
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
public class Playercontroller : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Attack")]
    [Tooltip("Minimum seconds between attacks")]
    [SerializeField] private float attackCooldown = 0.4f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackOrigin;    // null → uses transform.position
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Camera mainCamera;      // null → Camera.main

    // ─── Private State ────────────────────────────────────────
    private InputSystem_Actions _inputs;
    private Rigidbody2D _rb;

    private Vector2 _moveInput = Vector2.zero;   // live WASD / stick value
    private float _attackTimer = 0f;             // counts DOWN; attack allowed when ≤ 0
    private bool _isDead = false;

    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    private GameManager GM => GameManager.Instance;

    // ─────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (attackOrigin == null) attackOrigin = transform;
        if (mainCamera == null) mainCamera = Camera.main;

        // Create the input asset and register this script as the callback target
        _inputs = new InputSystem_Actions();
        _inputs.Player.AddCallbacks(this);
    }

    private void OnEnable()
    {
        _inputs.Player.Enable();
        GM?.OnPlayerDied.AddListener(HandlePlayerDied);
    }

    private void OnDisable()
    {
        _inputs.Player.Disable();
        GM?.OnPlayerDied.RemoveListener(HandlePlayerDied);
    }

    private void OnDestroy()
    {
        _inputs.Player.RemoveCallbacks(this);
        _inputs.Dispose();
    }

    private void Update()
    {
        if (_isDead) return;
        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_isDead) return;
        ApplyMovement();
    }

    // ─────────────────────────────────────────────────────────
    //  Movement
    // ─────────────────────────────────────────────────────────
    private void ApplyMovement()
    {
        float speed = GM != null ? GM.Player.Speed : 5f;
        _rb.linearVelocity = _moveInput * speed;

        // Flip sprite to face horizontal movement direction
        if (_moveInput.x != 0f)
            transform.localScale = new Vector3(_moveInput.x < 0f ? -1f : 1f, 1f, 1f);

        animator?.SetFloat(AnimSpeed, _moveInput.magnitude);
    }

    // ─────────────────────────────────────────────────────────
    //  Attack
    // ─────────────────────────────────────────────────────────
    private void PerformAttack()
    {
        if (_attackTimer > 0f) return;     // still on cooldown
        _attackTimer = attackCooldown;

        float range = GM != null ? GM.AttackRange : 3f;
        float damage = GM != null ? GM.Player.Damage : 20f;
        Vector2 origin = attackOrigin.position;

        animator?.SetTrigger(AnimAttack);

        // Find all enemies within attack range
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, enemyLayer);

        if (hits.Length == 0)
        {
            Debug.Log("[Player] Attack — no enemies in range.");
            return;
        }

        // Damage only the closest enemy
        Collider2D closest = GetClosest(hits, origin);
        EnemyController enemy = closest.GetComponent<EnemyController>();

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Debug.Log($"[Player] Hit '{closest.name}' for {damage:F1} dmg.");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  IPlayerActions callbacks
    //  The InputSystem_Actions asset calls these automatically.
    // ─────────────────────────────────────────────────────────

    /// <summary>Fires on started/performed/canceled. We read the Vector2 every phase.</summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        // ReadValue returns zero vector on canceled, so this handles stop naturally
        _moveInput = context.ReadValue<Vector2>();
    }

    /// <summary>Attack fires only on the performed phase (button pressed down).</summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || _isDead) return;
        PerformAttack();
    }

    // ── Remaining interface stubs ─────────────────────────────
    // These MUST exist to satisfy IPlayerActions.
    // Fill them in as you build out those features.

    public void OnLook(InputAction.CallbackContext context) { }
    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnJump(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { }
    public void OnDash(InputAction.CallbackContext context) { }

    // ─────────────────────────────────────────────────────────
    //  Death
    // ─────────────────────────────────────────────────────────
    private void HandlePlayerDied()
    {
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _inputs.Player.Disable();   // stop receiving any input after death
        Debug.Log("[Player] Input disabled — player died.");
    }

    // ─────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────
    private Collider2D GetClosest(Collider2D[] cols, Vector2 origin)
    {
        Collider2D best = cols[0];
        float bestDist = Vector2.Distance(origin, cols[0].transform.position);

        for (int i = 1; i < cols.Length; i++)
        {
            float d = Vector2.Distance(origin, cols[i].transform.position);
            if (d < bestDist) { bestDist = d; best = cols[i]; }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        float range = GM != null ? GM.AttackRange : 3f;
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);
    }
}