using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("References")]
    public DungeonGenerator generator;
    public Transform playerTransform;

    [Header("Room Prefab Folders (Resources/ relative paths)")]
    public string normalFolder   = "Rooms/Normal";
    public string corridorNSFolder = "Rooms/CorridorNS";
    public string corridorEWFolder = "Rooms/CorridorEW";
    public string bossFolder     = "Rooms/Boss";
    public string treasureFolder = "Rooms/Treasure";
    public string shopFolder     = "Rooms/Shop";
    public string startFolder    = "Rooms/Start";

    GameObject currentRoomInstance;
    int currentCell = 35;
    bool isTransitioning = false;

    // Husker hvilke prefabs der er brugt per celle (konsistens ved re-visit)
    Dictionary<int, string> cellPrefabMap = new();

    void Awake() => Instance = this;

    void Start()
    {
        generator.Generate(1);
        LoadRoom(35, Direction.South); // ingen "kom fra" retning ved start
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

        yield return TransitionManager.Instance.Transition(() =>
        {
            if (currentRoomInstance != null)
                Destroy(currentRoomInstance);

            currentCell = targetCell;
            LoadRoom(targetCell, fromDirection);
        });

        isTransitioning = false;
    }

    void LoadRoom(int cell, Direction fromDirection)
    {
        // Hent eller vælg prefab for denne celle
        if (!cellPrefabMap.ContainsKey(cell))
            cellPrefabMap[cell] = PickPrefab(generator.DungeonMap[cell]);

        GameObject prefab = Resources.Load<GameObject>(cellPrefabMap[cell]);
        currentRoomInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        // Sæt aktive døre
        var neighbours = generator.GetNeighbours(cell);
        var rc = currentRoomInstance.GetComponent<RoomController>();
        rc.SetDoors(
            north: neighbours.ContainsKey(Direction.North),
            south: neighbours.ContainsKey(Direction.South),
            east:  neighbours.ContainsKey(Direction.East),
            west:  neighbours.ContainsKey(Direction.West)
        );

        // Placér spilleren ved den rigtige spawn point
        Transform spawn = rc.GetSpawnPoint(fromDirection);
        if (spawn != null && playerTransform != null)
            playerTransform.position = spawn.position;
    }

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