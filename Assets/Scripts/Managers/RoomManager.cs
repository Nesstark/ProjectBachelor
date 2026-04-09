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

    GameObject currentRoomInstance;
    GameObject currentLevelExit;

    public RoomController CurrentRoom { get; private set; }
    public int CurrentCellPublic => currentCell;
    public int CurrentLevel { get; private set; } = 1;

    int currentCell = 35;
    bool isTransitioning = false;

    Dictionary<int, string> cellPrefabMap = new();
    HashSet<int> visitedCells = new();
    HashSet<int> clearedCells = new();

    void Awake() => Instance = this;

    void Start()
    {
        generator.Generate(CurrentLevel);
        LoadRoom(35, Direction.South);
    }

    // ─── Reset — called by AIPlayerAgent on episode begin ────
    /// <summary>
    /// Fully resets the dungeon back to level 1, room 35.
    /// Destroys all current room instances and regenerates the map.
    /// Called at the start of every ML-Agents training episode.
    /// </summary>
    public void ResetDungeon()
    {
        // Stop any ongoing transitions
        StopAllCoroutines();
        isTransitioning = false;

        // Clean up current level exit
        if (currentLevelExit != null) Destroy(currentLevelExit);

        // Clear all tracking state
        cellPrefabMap.Clear();
        visitedCells.Clear();
        clearedCells.Clear();

        // Reset level counter
        CurrentLevel = 1;

        // Regenerate the dungeon map
        generator.Generate(CurrentLevel);

        // Destroy current room and load the start room
        if (currentRoomInstance != null) Destroy(currentRoomInstance);
        currentCell = 35;
        LoadRoom(35, Direction.South);

        Debug.Log("[RoomManager] Dungeon reset — back to level 1 start room.");
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
            generator.Generate(CurrentLevel);
            if (currentRoomInstance != null) Destroy(currentRoomInstance);
            currentCell = 35;
            LoadRoom(35, Direction.South);
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
        currentLevelExit = Instantiate(levelExitPrefab, position, Quaternion.identity);
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

    // Disable gravity during room swap so player doesn't fall
    Rigidbody rb = playerTransform?.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
    }

    yield return StartCoroutine(TransitionManager.Instance.Transition(() =>
    {
        if (currentRoomInstance != null) Destroy(currentRoomInstance);
        currentCell = targetCell;
        LoadRoom(targetCell, fromDirection);
    }));

    // Re-enable gravity after new room floor is loaded
    if (rb != null)
        rb.useGravity = true;

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
    }

    public void MarkRoomCleared(int cell) => clearedCells.Add(cell);
    public bool IsRoomCleared(int cell)   => clearedCells.Contains(cell);
    public bool IsRoomVisited(int cell)   => visitedCells.Contains(cell);

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