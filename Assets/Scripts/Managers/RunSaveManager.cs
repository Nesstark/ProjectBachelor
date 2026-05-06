using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persists the player's run across scene loads and death restarts.
/// </summary>
public class RunSaveManager : MonoBehaviour
{
    public static RunSaveManager Instance { get; private set; }

    private const string SaveKey = "RunSaveData";

    [Serializable]
    public class RunSaveData
    {
        public bool hasActiveSave = false;

        // Dungeon state
        public int dungeonLevel = 1;
        public int dungeonSeed  = 0;

        // PlayerStats fields
        public int   playerLevel;
        public float currentXp;
        public float xpToNextLevel;
        public float maxHealth;
        public float currentHealth;
        public float damage;
        public float damageReduction;

        // PlayerController fields
        public float moveSpeed;
        public float attackRange;

        // Collected permanent pickups
        public List<string> collectedPickupIds = new List<string>();
    }

    private RunSaveData _current;
    public RunSaveData Current => _current;
    public bool HasActiveSave => _current != null && _current.hasActiveSave;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromDisk();
    }

    public void SaveRun(int level, int seed, PlayerStats stats, PlayerController controller, List<string> pickupIds)
    {
        _current = new RunSaveData
        {
            hasActiveSave      = true,
            dungeonLevel       = level,
            dungeonSeed        = seed,
            playerLevel        = stats.Level,
            currentXp          = stats.CurrentXp,
            xpToNextLevel      = stats.XpToNextLevel,
            maxHealth          = stats.MaxHealth,
            currentHealth      = stats.CurrentHealth,
            damage             = stats.Damage,
            damageReduction    = stats.DamageReduction,
            moveSpeed          = controller != null ? controller.CurrentMoveSpeed   : 0f,
            attackRange        = controller != null ? controller.CurrentAttackRange : 0f,
            collectedPickupIds = new List<string>(pickupIds)
        };
        WriteToDisk();
        Debug.Log($"[RunSaveManager] Run saved — Level {level}, Seed {seed}");
    }

    public void ClearSave()
    {
        _current = new RunSaveData { hasActiveSave = false };
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("[RunSaveManager] Save cleared.");
    }

    private void WriteToDisk()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_current));
        PlayerPrefs.Save();
    }

    private void LoadFromDisk()
    {
        if (PlayerPrefs.HasKey(SaveKey))
            _current = JsonUtility.FromJson<RunSaveData>(PlayerPrefs.GetString(SaveKey));
        else
            _current = new RunSaveData { hasActiveSave = false };
    }
}