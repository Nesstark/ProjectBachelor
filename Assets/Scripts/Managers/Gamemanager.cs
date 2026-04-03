using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// ============================================================
// GameManager.cs — Central Stat & Game-State Manager
// Attach to a single "GameManager" GameObject in your scene.
// ============================================================

public class GameManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Player Base Stats ───────────────────────────────────
    [Header("Player Base Stats (Level 1)")]
    [SerializeField] private float basePlayerHealth = 100f;
    [SerializeField] private float basePlayerDamage = 20f;

    [Header("Player Scaling Per Level")]
    [SerializeField] private float healthPerLevel = 20f;
    [SerializeField] private float damagePerLevel = 5f;

    [Header("XP Curve")]
    [SerializeField] private float baseXpToLevel    = 100f;
    [SerializeField] private float xpScalingFactor  = 1.5f;

    // ─── Enemy Type Definitions ──────────────────────────────
    [Header("Enemy Type Definitions")]
    [SerializeField] private EnemyTypeData[] enemyTypes;

    [Header("Enemy Level Scaling (applied on top of base stats)")]
    [Tooltip("+X% per player level to all enemy stats")]
    [SerializeField] private float enemyScalePerLevel = 0.15f;

    [Header("Attack Range")]
    [SerializeField] private float attackRange = 3f;

    // ─── Events ──────────────────────────────────────────────
    [HideInInspector] public UnityEvent                    OnPlayerLevelUp      = new UnityEvent();
    [HideInInspector] public UnityEvent<float, float>      OnPlayerHealthChanged = new UnityEvent<float, float>();
    [HideInInspector] public UnityEvent<int, float, float> OnXpChanged          = new UnityEvent<int, float, float>();
    [HideInInspector] public UnityEvent                    OnPlayerDied         = new UnityEvent();

    // ─── Runtime Player Stats ────────────────────────────────
    public PlayerStats Player    { get; private set; }
    public float       AttackRange => attackRange;

    // ─── Lookup cache ────────────────────────────────────────
    private Dictionary<string, EnemyTypeData> _enemyLookup;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookup();
        InitPlayer();
    }

    // ─── Lookup ──────────────────────────────────────────────
    private void BuildLookup()
    {
        _enemyLookup = new Dictionary<string, EnemyTypeData>();
        if (enemyTypes == null) return;

        foreach (var et in enemyTypes)
        {
            if (string.IsNullOrEmpty(et.typeName)) continue;
            _enemyLookup[et.typeName.ToLower()] = et;
        }
    }

    /// <summary>
    /// Returns a fully scaled EnemyStats block for the given enemy type name.
    /// Matches "Archer", "Warrior", "Elite" (case-insensitive).
    /// Falls back to Warrior stats if the name is not found.
    /// </summary>
    public EnemyStats GetEnemyStats(string enemyTypeName)
    {
        string key = enemyTypeName.ToLower();

        if (!_enemyLookup.TryGetValue(key, out EnemyTypeData data))
        {
            Debug.LogWarning($"[GameManager] Unknown enemy type '{enemyTypeName}' — using fallback stats.");
            data = FallbackType();
        }

        float scale = 1f + (Player.Level - 1) * enemyScalePerLevel;

        return new EnemyStats
        {
            MaxHealth     = data.baseHealth    * scale,
            CurrentHealth = data.baseHealth    * scale,
            Speed         = data.baseSpeed     * scale,
            Damage        = data.baseDamage    * scale,
            XpReward      = data.baseXpReward  * Mathf.Pow(1.1f, Player.Level - 1)
        };
    }

    // ─── Player Init ─────────────────────────────────────────
    private void InitPlayer()
    {
        Player = new PlayerStats
        {
            Level         = 1,
            CurrentXp     = 0f,
            XpToNextLevel = baseXpToLevel,
            MaxHealth     = basePlayerHealth,
            CurrentHealth = basePlayerHealth,
            Damage        = basePlayerDamage
        };
    }

    // ─── Player Reset (called by AIPlayerAgent on episode begin) ─
    /// <summary>
    /// Fully resets player stats back to level 1 defaults.
    /// Called at the start of every ML-Agents training episode.
    /// </summary>
    public void ResetPlayer()
    {
        Player.Level         = 1;
        Player.CurrentXp     = 0f;
        Player.XpToNextLevel = baseXpToLevel;
        Player.MaxHealth     = basePlayerHealth;
        Player.CurrentHealth = basePlayerHealth;
        Player.Damage        = basePlayerDamage;

        // Notify UI so health bars etc. update immediately
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
        OnXpChanged.Invoke(Player.Level, Player.CurrentXp, Player.XpToNextLevel);

        Debug.Log("[GM] Player reset — HP & stats restored to level 1.");
    }

    // ─── Player Damage ───────────────────────────────────────
    public void ApplyDamageToPlayer(float amount)
    {
        if (Player.CurrentHealth <= 0f) return;

        Player.CurrentHealth = Mathf.Max(0f, Player.CurrentHealth - amount);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
        Debug.Log($"[GM] Player hit {amount:F1}. HP:{Player.CurrentHealth:F0}/{Player.MaxHealth:F0}");

        if (Player.CurrentHealth <= 0f)
        {
            Debug.Log("[GM] Player died.");
            OnPlayerDied.Invoke();
        }
    }

    public void HealPlayer(float amount)
    {
        Player.CurrentHealth = Mathf.Min(Player.MaxHealth, Player.CurrentHealth + amount);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }

    // ─── XP & Level Up ───────────────────────────────────────
    public void AwardXp(float amount)
    {
        Player.CurrentXp += amount;

        while (Player.CurrentXp >= Player.XpToNextLevel)
        {
            Player.CurrentXp -= Player.XpToNextLevel;
            LevelUp();
        }

        OnXpChanged.Invoke(Player.Level, Player.CurrentXp, Player.XpToNextLevel);
    }

    private void LevelUp()
    {
        Player.Level++;
        Player.MaxHealth     = basePlayerHealth + healthPerLevel * (Player.Level - 1);
        Player.CurrentHealth = Mathf.Min(Player.CurrentHealth + healthPerLevel, Player.MaxHealth);
        Player.Damage        = basePlayerDamage + damagePerLevel * (Player.Level - 1);
        Player.XpToNextLevel = Mathf.Round(baseXpToLevel * Mathf.Pow(xpScalingFactor, Player.Level - 1));

        Debug.Log($"[GM] LEVEL UP → {Player.Level} HP:{Player.MaxHealth} DMG:{Player.Damage:F1}");

        OnPlayerLevelUp.Invoke();
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }

    // ─── Fallback ────────────────────────────────────────────
    private EnemyTypeData FallbackType() => new EnemyTypeData
    {
        typeName    = "Fallback",
        baseHealth  = 50f,
        baseSpeed   = 2.5f,
        baseDamage  = 10f,
        baseXpReward = 25f
    };

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (Player == null) return;
        GUILayout.BeginArea(new Rect(10, 10, 260, 120));
        GUILayout.Label($"Level  : {Player.Level}");
        GUILayout.Label($"HP     : {Player.CurrentHealth:F0} / {Player.MaxHealth:F0}");
        GUILayout.Label($"Damage : {Player.Damage:F1}");
        GUILayout.Label($"XP     : {Player.CurrentXp:F0} / {Player.XpToNextLevel:F0}");
        GUILayout.EndArea();
    }
#endif
}

// ============================================================
// Data Classes
// ============================================================

[System.Serializable]
public class PlayerStats
{
    public int   Level;
    public float CurrentXp;
    public float XpToNextLevel;
    public float MaxHealth;
    public float CurrentHealth;
    public float Damage;
}

[System.Serializable]
public class EnemyStats
{
    public float MaxHealth;
    public float CurrentHealth;
    public float Speed;
    public float Damage;
    public float XpReward;
}

/// <summary>
/// Defines the base stats for one enemy type.
/// Add one entry per type in the GameManager Inspector.
/// </summary>
[System.Serializable]
public class EnemyTypeData
{
    [Tooltip("Must exactly match the EnemyType set on each EnemyController (Archer / Warrior / Elite)")]
    public string typeName;

    [Space]
    public float baseHealth   = 50f;
    public float baseSpeed    = 3f;
    public float baseDamage   = 10f;
    public float baseXpReward = 25f;
}