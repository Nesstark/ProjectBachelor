using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private Vector2 inputDir;
    private Vector3 moveDir;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 lastMoveDir;

    // Animator parameter hashes
    private static readonly int HashSpeed   = Animator.StringToHash("Speed");
    private static readonly int HashDirX    = Animator.StringToHash("DirX");
    private static readonly int HashDirZ    = Animator.StringToHash("DirZ");
    private static readonly int HashDash    = Animator.StringToHash("Dash");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastMoveDir = Vector3.forward;
    }

    // Called by Unity's Input System (Player Input component)
    public void OnMove(InputValue value)
    {
        inputDir = value.Get<Vector2>();
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && !isDashing && dashCooldownTimer <= 0f)
            StartDash();
    }

    private void Update()
    {
        dashTimer -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;

        if (isDashing && dashTimer <= 0f)
            isDashing = false;

        // Map 2D input → 3D world movement (X/Z plane)
        moveDir = new Vector3(inputDir.x, 0f, inputDir.y).normalized;

        if (moveDir.magnitude > 0.1f)
            lastMoveDir = moveDir;

        UpdateAnimator();
        UpdateSpriteFlip();
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = lastMoveDir * dashSpeed;
        }
        else
        {
            Vector3 targetVelocity = moveDir * moveSpeed;
            rb.linearVelocity = new Vector3(
                targetVelocity.x,
                rb.linearVelocity.y, // preserve gravity
                targetVelocity.z
            );
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        animator?.SetTrigger(HashDash);
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
        // Flip sprite based on horizontal direction
        if (lastMoveDir.x > 0.1f)
            spriteRenderer.flipX = true;
        else if (lastMoveDir.x < -0.1f)
            spriteRenderer.flipX = false;
    }
}
