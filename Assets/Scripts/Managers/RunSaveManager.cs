using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ── Read-Only attribute — keeps debug fields uneditable in the Inspector ──
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif

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

    // ── Inspector Debug View ─────────────────────────────────
    [Serializable]
    private class SaveDebugView
    {
        [Header("Dungeon")]
        [ReadOnly] public bool    hasSave;
        [ReadOnly] public int     dungeonLevel;
        [ReadOnly] public int     dungeonSeed;

        [Header("Player Level & XP")]
        [ReadOnly] public int     playerLevel;
        [ReadOnly] public float   currentXp;
        [ReadOnly] public float   xpToNextLevel;

        [Header("Core Stats")]
        [ReadOnly] public float   maxHealth;
        [ReadOnly] public float   currentHealth;
        [ReadOnly] public float   damage;
        [ReadOnly] public float   damageReduction;

        [Header("Pickup Bonuses")]
        [ReadOnly] public float   moveSpeed;
        [ReadOnly] public float   attackRange;

        [Header("Collected Pickups")]
        [ReadOnly] public string[] collectedPickupIds;
    }

    [Header("── Debug View (read-only) ──")]
    [SerializeField] private SaveDebugView _debugView = new SaveDebugView();

    // ─────────────────────────────────────────────────────────

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
        RefreshDebugView();
        Debug.Log($"[RunSaveManager] Run saved — Level {level}, Seed {seed}");
    }

    public void ClearSave()
    {
        _current = new RunSaveData { hasActiveSave = false };
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        RefreshDebugView();
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

        RefreshDebugView();
    }

    private void RefreshDebugView()
    {
        if (_current == null) return;

        _debugView.hasSave            = _current.hasActiveSave;
        _debugView.dungeonLevel       = _current.dungeonLevel;
        _debugView.dungeonSeed        = _current.dungeonSeed;
        _debugView.playerLevel        = _current.playerLevel;
        _debugView.currentXp          = _current.currentXp;
        _debugView.xpToNextLevel      = _current.xpToNextLevel;
        _debugView.maxHealth          = _current.maxHealth;
        _debugView.currentHealth      = _current.currentHealth;
        _debugView.damage             = _current.damage;
        _debugView.damageReduction    = _current.damageReduction;
        _debugView.moveSpeed          = _current.moveSpeed;
        _debugView.attackRange        = _current.attackRange;
        _debugView.collectedPickupIds = _current.collectedPickupIds != null
            ? _current.collectedPickupIds.ToArray()
            : new string[0];
    }
}