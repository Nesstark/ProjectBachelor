using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

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

    public RoomController CurrentRoom { get; private set; }
    public int CurrentCellPublic => currentCell;
    public int CurrentLevel { get; set; } = 1;
    public int CurrentSeed => generator.seed;

    int currentCell = 35;
    bool isTransitioning = false;

    Dictionary<int, string> cellPrefabMap = new();
    HashSet<int> visitedCells = new();
    HashSet<int> clearedCells = new();
    

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Always clear stale event listeners first — prevents invincibility bug
        GameManager.Instance?.PrepareForSceneLoad();

        var save = RunSaveManager.Instance;

        if (save != null && save.HasActiveSave)
        {
            CurrentLevel = save.Current.dungeonLevel;
            generator.GenerateWithSeed(CurrentLevel, save.Current.dungeonSeed);

            // Restore all PlayerStats values
            GameManager.Instance?.RestorePlayerFromSave(save.Current);

            // Fire HUD update events once so health bar and XP bar refresh
            GameManager.Instance?.OnPlayerHealthChanged.Invoke(
                save.Current.currentHealth, save.Current.maxHealth);
            GameManager.Instance?.OnXpChanged.Invoke(
                save.Current.playerLevel, save.Current.currentXp, save.Current.xpToNextLevel);

            // Restore PlayerController stats (move speed and attack range)
            var controller = playerTransform.GetComponent<PlayerController>();
            if (controller != null)
            {
                if (save.Current.moveSpeed   > 0f) controller.SetMoveSpeed(save.Current.moveSpeed);
                if (save.Current.attackRange > 0f) controller.SetAttackRange(save.Current.attackRange);
            }

            // Reapply permanent pickup effects silently
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

        LoadRoom(35, Direction.South);
    }

    public void LoadNextLevel()
    {
        Debug.Log("LoadNextLevel kaldt, isTransitioning: " + isTransitioning);
        if (isTransitioning) return;
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

            // ── NEW: destroy all enemies from the previous floor before
            //        generating the next one, so none bleed into the start room.
            RoomController.CleanupForNextLevel();

            generator.Generate(CurrentLevel);
            if (currentRoomInstance != null) Destroy(currentRoomInstance);
            currentCell = 35;
            LoadRoom(35, Direction.South);
        }));

        isTransitioning = false;
    }

    // ── NEW: guard prevents spawning a duplicate exit if UnlockDoors()
    //        is called more than once on a cleared boss room (re-entry path).
    public void SpawnLevelExit(Vector3 position)
    {
        if (levelExitPrefab == null)
        {
            Debug.LogError("LevelExit prefab er ikke sat på RoomManager!");
            return;
        }
        if (currentLevelExit != null) return;   // already exists — do nothing
        Vector3 spawnPos = position;
        spawnPos.y += levelExitHeightOffset;
        currentLevelExit = Instantiate(levelExitPrefab, spawnPos, Quaternion.identity);
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
            // ── NEW: hide the level exit before the room swap so it cannot
            //        appear floating in the middle of the next room.
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

        bool isFirstVisit = !visitedCells.Contains(cell);
        visitedCells.Add(cell);

        Transform spawn;
        if (isFirstVisit && cell == 35 && CurrentRoom.startSpawn != null)
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

        CurrentRoom.StartEncounter();

        // ── NEW: if the player re-enters a cleared boss room, re-show
        //        the level exit that was hidden on the way out.
        if (CurrentRoom.isBossRoom && IsRoomCleared(cell) && currentLevelExit != null)
            currentLevelExit.SetActive(true);
    }

    public void MarkRoomCleared(int cell) => clearedCells.Add(cell);
    public bool IsRoomCleared(int cell) => clearedCells.Contains(cell);

    string PickPrefab(RoomType type)
    {
        string folder = type switch
        {
            RoomType.Normal      => normalFolder,
            RoomType.CorridorNS  => corridorNSFolder,
            RoomType.CorridorEW  => corridorEWFolder,
            RoomType.Boss        => bossFolder,
            RoomType.Treasure    => treasureFolder,
            RoomType.Shop        => shopFolder,
            RoomType.Start       => startFolder,
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