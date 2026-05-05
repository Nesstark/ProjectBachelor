using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A static training dummy for the start room.
/// - Infinite health (never dies, never rewards XP)
/// - No AI / NavMesh movement
/// - Reacts to hits with flash + VFX as normal
/// - 0.001% chance (1-in-100,000) to deal 1 damage back to the player per hit
/// </summary>
public class TestDummyController : BaseEnemy
{
    // ── Easter-egg ──────────────────────────────────────────────────────────
    private const float ChanceToHitBack = 0.00001f; // 0.001 %
    private const float CounterDamage   = 1f;

    // ── BaseEnemy contract ──────────────────────────────────────────────────
    protected override string EnemyTypeName => "TestDummy";

    // The dummy never moves — satisfy the abstract requirement with a no-op.
    protected override void HandleMovement() { }

    // ── Initialisation ──────────────────────────────────────────────────────
    protected override void Start()
    {
        base.Start();

        // Disable NavMesh so the dummy stays rooted to the spot
        if (Agent != null)
        {
            Agent.ResetPath();
            Agent.enabled = false;
        }

        // Infinite health: set an astronomically large pool
        Stats.MaxHealth     = float.MaxValue;
        Stats.CurrentHealth = float.MaxValue;

        // No XP, no threat
        Stats.XpReward = 0f;
        Stats.Damage   = 0f;

        Debug.Log("[TestDummy] Training dummy ready. Hit me all you like!");
    }

    // ── Update — skip all AI logic entirely ────────────────────────────────
    protected override void Update()
    {
        // Intentionally empty — no movement, no attack cycle, no animator ticks.
    }

    // ── Damage — absorb hits but never die ─────────────────────────────────
    public override void TakeDamage(float amount)
    {
        if (IsDead) return;

        // Log without modifying health (keep it at float.MaxValue)
        Debug.Log($"[TestDummy] Absorbed {amount:F1} damage — dummy is unbreakable.");

        // Visual & audio feedback still fires so the player gets satisfying feedback
        var hitFlash = GetComponentInChildren<HitFlashHandler>();
        hitFlash?.Flash();

        AudioManager.Instance?.Play("EnemyHit");

        if (hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                hitVFXPrefab,
                transform.position + Vector3.up * 0.7f,
                Quaternion.identity);
            Destroy(vfx, 2f);
        }

        CameraShakeManager.Instance?.ShakeImpulse(
            CameraShakeManager.Instance.hitShakeForce);

        // ── Easter egg: 0.001 % chance the dummy slaps back ────────────────
        if (Random.value < ChanceToHitBack)
        {
            Debug.Log("[TestDummy] ...wait, did the dummy just flinch? (counter-hit!)");
            GM?.ApplyDamageToPlayer(CounterDamage);

            if (animator != null)
                animator.SetTrigger(HashAttack);
        }
    }

    // ── Die — completely suppressed ────────────────────────────────────────
    protected override void Die()
    {
        // The dummy cannot die. Do nothing.
        Debug.Log("[TestDummy] Nice try.");
    }

    // ── Animator / sprite — disabled since no movement ─────────────────────
    protected override void UpdateAnimator()  { }
    protected override void UpdateSpriteFlip() { }
}
