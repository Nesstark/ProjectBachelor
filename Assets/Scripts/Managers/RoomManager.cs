using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;
    public static event System.Action OnLevelExitReached;

    [Header("References")]
    public DungeonGenerator generator;
    public Transform playerTransform;

    [Header("Room Prefab Folders")]
    public string normalFolder     = "Rooms/Normal";
    public string corridorNSFolder = "Rooms/CorridorNS";
    public string corridorEWFolder = "Rooms/CorridorEW";
    public string bossFolder       = "Rooms/Boss";
    public string treasureFolder   = "Rooms/Treasure";
    public string shopFolder       = "Rooms/Shop";
    public string startFolder      = "Rooms/Start";

    [Header("Level Exit")]
    public GameObject levelExitPrefab;
    [Tooltip("Vertical offset applied when spawning the level exit portal.")]
    public float levelExitHeightOffset = 0f;

    GameObject currentRoomInstance;
    GameObject currentLevelExit;

    public RoomController CurrentRoom    { get; private set; }

    public GameObject CurrentLevelExit => currentLevelExit;
    public int CurrentCellPublic => currentCell;
    public int CurrentLevel { get; set; } = 1;
    public int CurrentSeed => generator.seed;

    // ── FIX: expose spawn cell so AIPlayerAgent doesn't hardcode 35 ──────────
    public int SpawnCellId { get; private set; } = 35;

    int currentCell = 35;
    bool isTransitioning = false;
    public bool IsTrainingMode { get; private set; }

    Dictionary<int, string> cellPrefabMap = new();
    HashSet<int> visitedCells  = new();
    HashSet<int> clearedCells  = new();

    // ── FIX: room types that contain no enemies and are cleared on arrival ────
    private static readonly HashSet<RoomType> NoCombatRooms = new()
    {
        RoomType.Start,
        RoomType.Treasure,
        RoomType.Shop,
        RoomType.CorridorNS,
        RoomType.CorridorEW,
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        GameManager.Instance?.PrepareForSceneLoad();

        var save = RunSaveManager.Instance;

        if (save != null && save.HasActiveSave)
        {
            CurrentLevel = save.Current.dungeonLevel;
            generator.GenerateWithSeed(CurrentLevel, save.Current.dungeonSeed);

            GameManager.Instance?.RestorePlayerFromSave(save.Current);

            GameManager.Instance?.OnPlayerHealthChanged.Invoke(
                save.Current.currentHealth, save.Current.maxHealth);
            GameManager.Instance?.OnXpChanged.Invoke(
                save.Current.playerLevel, save.Current.currentXp, save.Current.xpToNextLevel);

            var controller = playerTransform.GetComponent<PlayerController>();
            if (controller != null)
            {
                if (save.Current.moveSpeed   > 0f) controller.SetMoveSpeed(save.Current.moveSpeed);
                if (save.Current.attackRange > 0f) controller.SetAttackRange(save.Current.attackRange);
            }

            PickupTracker.Instance?.ClearAll();
            if (PickupTracker.Instance != null)
            {
                PickupTracker.Instance.ApplySavedPickups(
                    playerTransform.gameObject, save.Current.collectedPickupIds);
                foreach (var id in save.Current.collectedPickupIds)
                    PickupTracker.Instance.Register(id);
            }

            Debug.Log($"[RoomManager] Run restored — Level {CurrentLevel}, Seed {save.Current.dungeonSeed}");
        }
        else
        {
            CurrentLevel = 1;
            generator.Generate(CurrentLevel);
        }

        LoadRoom(SpawnCellId, Direction.South);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AI TRAINING RESET
    // ─────────────────────────────────────────────────────────────────────────
    public void ResetForTraining()
    {
        IsTrainingMode = true;

        if (currentLevelExit != null)
        {
            Destroy(currentLevelExit);
            currentLevelExit = null;
        }

        cellPrefabMap.Clear();
        visitedCells.Clear();
        clearedCells.Clear();

        RoomController.CleanupForNextLevel();

        foreach (var pickup in FindObjectsByType<PickupBase>(FindObjectsSortMode.None))
            Destroy(pickup.gameObject);

        PickupTracker.Instance?.ClearAll();

        CurrentLevel = 1;
        generator.Generate(CurrentLevel);

        if (currentRoomInstance != null)
        {
            Destroy(currentRoomInstance);
            currentRoomInstance = null;
        }

        currentCell    = SpawnCellId;
        isTransitioning = false;
        LoadRoom(SpawnCellId, Direction.South);

        Debug.Log($"[RoomManager] ResetForTraining complete — Seed:{generator.seed}");
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void LoadNextLevel()
    {
        Debug.Log("LoadNextLevel kaldt, isTransitioning: " + isTransitioning);
        if (isTransitioning) return;
        OnLevelExitReached?.Invoke();
        if (IsTrainingMode) return;
        StartCoroutine(DoLevelUpTransition());
    }

    IEnumerator DoLevelUpTransition()
    {
        Debug.Log("DoLevelUpTransition START");
        isTransitioning = true;
        CurrentLevel++;

        yield return StartCoroutine(TransitionManager.Instance.LevelUpTransition(CurrentLevel, () =>
        {
            if (currentLevelExit != null) Destroy(currentLevelExit);
            cellPrefabMap.Clear();
            visitedCells.Clear();
            clearedCells.Clear();

            RoomController.CleanupForNextLevel();

            generator.Generate(CurrentLevel);
            if (currentRoomInstance != null) Destroy(currentRoomInstance);
            currentCell = SpawnCellId;
            LoadRoom(SpawnCellId, Direction.South);
        }));

        isTransitioning = false;
    }

    public void SpawnLevelExit(Vector3 position)
    {
        if (levelExitPrefab == null)
        {
            Debug.LogError("LevelExit prefab er ikke sat på RoomManager!");
            return;
        }
        if (currentLevelExit != null) return;
        Vector3 spawnPos  = position;
        spawnPos.y       += levelExitHeightOffset;
        currentLevelExit  = Instantiate(levelExitPrefab, spawnPos, Quaternion.identity);
    }

    public void TryMove(Direction dir)
    {
        if (isTransitioning) return;

        int offset = dir switch
        {
            Direction.North =>  10,
            Direction.South => -10,
            Direction.East  =>   1,
            Direction.West  =>  -1,
            _ => 0
        };

        int targetCell = currentCell + offset;
        if (!generator.DungeonMap.ContainsKey(targetCell)) return;

        StartCoroutine(DoTransition(targetCell, dir));
    }

    IEnumerator DoTransition(int targetCell, Direction fromDirection)
    {
        isTransitioning = true;

        yield return StartCoroutine(TransitionManager.Instance.Transition(() =>
        {
            if (currentLevelExit != null) currentLevelExit.SetActive(false);

            if (currentRoomInstance != null) Destroy(currentRoomInstance);
            currentCell = targetCell;
            LoadRoom(targetCell, fromDirection);
        }));

        isTransitioning = false;
    }

    void LoadRoom(int cell, Direction fromDirection)
    {
        string prefabPath = PickPrefab(generator.DungeonMap[cell]);
        if (prefabPath == null) return;

        if (!cellPrefabMap.ContainsKey(cell))
            cellPrefabMap[cell] = prefabPath;

        GameObject prefab = Resources.Load<GameObject>(cellPrefabMap[cell]);
        if (prefab == null)
        {
            Debug.LogError($"Prefab ikke fundet: {cellPrefabMap[cell]}");
            return;
        }

        currentRoomInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        CurrentRoom = currentRoomInstance.GetComponent<RoomController>();

        if (CurrentRoom == null)
        {
            Debug.LogError($"RoomController mangler på prefab: {cellPrefabMap[cell]}");
            return;
        }

        var neighbours = generator.GetNeighbours(cell);
        CurrentRoom.SetDoors(
            north: neighbours.ContainsKey(Direction.North),
            south: neighbours.ContainsKey(Direction.South),
            east:  neighbours.ContainsKey(Direction.East),
            west:  neighbours.ContainsKey(Direction.West)
        );

        // ── FIX: rooms with no enemies are cleared the moment the player arrives.
        // Without this, Start/Treasure/Corridor rooms stay "uncleared" forever,
        // IsDoorUnlocked() never returns true, and the AI gets zero signal to leave.
        if (NoCombatRooms.Contains(CurrentRoom.roomType))
        {
            clearedCells.Add(cell);
            Debug.Log($"[RoomManager] Cell {cell} ({CurrentRoom.roomType}) auto-cleared on load.");
        }

        bool isFirstVisit = !visitedCells.Contains(cell);
        visitedCells.Add(cell);

        Transform spawn;
        if (isFirstVisit && cell == SpawnCellId && CurrentRoom.startSpawn != null)
            spawn = CurrentRoom.startSpawn;
        else
            spawn = CurrentRoom.GetSpawnPoint(fromDirection);

        if (spawn != null && playerTransform != null)
        {
            Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.MovePosition(spawn.position);
            }
            else
            {
                playerTransform.position = spawn.position;
            }
        }

        if (CurrentRoom.roomType == RoomType.Start)
            CurrentRoom.StartEncounter();

        if (CurrentRoom.isBossRoom && IsRoomCleared(cell) && currentLevelExit != null)
            currentLevelExit.SetActive(true);
    }

    public void MarkRoomCleared(int cell) => clearedCells.Add(cell);
    public bool IsRoomCleared(int cell)   => clearedCells.Contains(cell);

    string PickPrefab(RoomType type)
    {
        string folder = type switch
        {
            RoomType.Normal     => normalFolder,
            RoomType.CorridorNS => corridorNSFolder,
            RoomType.CorridorEW => corridorEWFolder,
            RoomType.Boss       => bossFolder,
            RoomType.Treasure   => treasureFolder,
            RoomType.Shop       => shopFolder,
            RoomType.Start      => startFolder,
            _ => normalFolder
        };

        GameObject[] options = Resources.LoadAll<GameObject>(folder);
        if (options.Length == 0)
        {
            Debug.LogError($"Ingen prefabs fundet i Resources/{folder}!");
            return null;
        }
        return $"{folder}/{options[Random.Range(0, options.Length)].name}";
    }
}