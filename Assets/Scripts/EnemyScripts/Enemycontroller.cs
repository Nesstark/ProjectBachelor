using UnityEngine;

// ============================================================
//  EnemyController.cs  —  Warrior & Elite enemy types
//  Inherits all shared logic from BaseEnemy.
//  Only defines what is UNIQUE to these types: movement.
// ============================================================

public class EnemyController : BaseEnemy
{
    public enum EnemyType { Warrior, Elite }

    [Header("Enemy Type")]
    [SerializeField] private EnemyType enemyType = EnemyType.Warrior;

    // Tells BaseEnemy which stat block to fetch from GameManager
    protected override string EnemyTypeName => enemyType.ToString();

    // ─── Movement — chase the player directly ─────────────────
    protected override void HandleMovement()
    {
        Agent.SetDestination(PlayerTransform.position);
    }
}