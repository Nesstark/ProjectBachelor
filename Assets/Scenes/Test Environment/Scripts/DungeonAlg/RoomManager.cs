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

    GameObject currentRoomInstance;
    public RoomController CurrentRoom { get; private set; }

    int currentCell = 35;
    bool isTransitioning = false;

    Dictionary<int, string> cellPrefabMap = new();
    HashSet<int> visitedCells = new();

    void Awake() => Instance = this;

    void Start()
    {
        generator.Generate(1);
        LoadRoom(35, Direction.South);
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

        if (currentRoomInstance != null)
            Destroy(currentRoomInstance);

        currentCell = targetCell;
        LoadRoom(targetCell, fromDirection);

        isTransitioning = false;
        yield break;
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

        // Spawn spiller
        bool isFirstVisit = !visitedCells.Contains(cell);
        visitedCells.Add(cell);

        Transform spawn;
        if (isFirstVisit && cell == 35 && CurrentRoom.startSpawn != null)
            spawn = CurrentRoom.startSpawn;
        else
            spawn = CurrentRoom.GetSpawnPoint(fromDirection);

        if (spawn != null && playerTransform != null)
            playerTransform.position = spawn.position;

        CurrentRoom.StartEncounter();
    }

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
