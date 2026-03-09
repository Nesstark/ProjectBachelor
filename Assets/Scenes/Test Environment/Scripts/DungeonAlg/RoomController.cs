using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("Door GameObjects")]
    public GameObject doorNorth;
    public GameObject doorSouth;
    public GameObject doorEast;
    public GameObject doorWest;

    [Header("Door Closed Overlays")]
    public GameObject doorNorthClosed;
    public GameObject doorSouthClosed;
    public GameObject doorEastClosed;
    public GameObject doorWestClosed;

    [Header("Door Triggers")]
    public Collider doorNorthTrigger;
    public Collider doorSouthTrigger;
    public Collider doorEastTrigger;
    public Collider doorWestTrigger;

    [Header("Wall Blocks (lukker ubrugte døråbninger)")]
    public GameObject doorNorthWall;
    public GameObject doorSouthWall;
    public GameObject doorEastWall;
    public GameObject doorWestWall;

    [Header("Player Spawn Points")]
    public Transform spawnNorth;
    public Transform spawnSouth;
    public Transform spawnEast;
    public Transform spawnWest;
    public Transform startSpawn;

    [Header("Doorway Visuals")]
    public MeshRenderer[] doorNorthCrosses;
    public MeshRenderer[] doorSouthCrosses;
    public MeshRenderer[] doorEastCrosses;
    public MeshRenderer[] doorWestCrosses;
    public Material doorwayLit;
    public Material doorwayDim;

    [Header("Enemy Spawn Points")]
    public Transform[] enemySpawnPoints;

    [Header("Encounter Settings")]
    public int enemyCount = 0;
    int remainingEnemies = 0;
    bool encounterActive = false;

    [Header("Boss Settings")]
    public bool isBossRoom = false;
    public Transform levelExitSpawnPoint;

    public void SetDoors(bool north, bool south, bool east, bool west)
    {
        ConfigureDoor(doorNorth, doorNorthClosed, doorNorthTrigger, doorNorthWall, doorNorthCrosses, north);
        ConfigureDoor(doorSouth, doorSouthClosed, doorSouthTrigger, doorSouthWall, doorSouthCrosses, south);
        ConfigureDoor(doorEast,  doorEastClosed,  doorEastTrigger,  doorEastWall,  doorEastCrosses,  east);
        ConfigureDoor(doorWest,  doorWestClosed,  doorWestTrigger,  doorWestWall,  doorWestCrosses,  west);
    }

    void ConfigureDoor(GameObject door, GameObject closed, Collider trigger, GameObject wall, MeshRenderer[] crosses, bool hasExit)
    {
        if (door != null) door.SetActive(hasExit);
        if (wall != null) wall.SetActive(!hasExit);
        if (!hasExit) return;

        if (closed != null) closed.SetActive(false);
        if (trigger != null) trigger.enabled = true;
        SetDoorwayMaterial(crosses, false);
    }

    public void StartEncounter()
    {
        if (RoomManager.Instance.IsRoomCleared(RoomManager.Instance.CurrentCellPublic))
        {
            UnlockDoors();
            return;
        }
    }

    public void TriggerEncounter()
    {
        if (enemyCount <= 0)
        {
            UnlockDoors();
            return;
        }

        LockDoors();
        remainingEnemies = enemyCount;
        encounterActive = true;
        Debug.Log($"Encounter triggered: {remainingEnemies} fjender");
    }

    public void OnEnemyDied()
    {
        if (!encounterActive) return;
        remainingEnemies--;
        Debug.Log($"Fjende dræbt. {remainingEnemies} tilbage.");

        if (remainingEnemies <= 0)
        {
            encounterActive = false;
            UnlockDoors();
        }
    }

    public void LockDoors()
    {
        LockDoor(doorNorthClosed, doorNorthTrigger, doorNorthCrosses);
        LockDoor(doorSouthClosed, doorSouthTrigger, doorSouthCrosses);
        LockDoor(doorEastClosed,  doorEastTrigger,  doorEastCrosses);
        LockDoor(doorWestClosed,  doorWestTrigger,  doorWestCrosses);
    }

    void LockDoor(GameObject closed, Collider trigger, MeshRenderer[] crosses)
    {
        if (closed != null) closed.SetActive(true);
        if (trigger != null) trigger.enabled = false;
        SetDoorwayMaterial(crosses, true);
    }

    public void UnlockDoors()
    {
        UnlockDoor(doorNorthClosed, doorNorthTrigger, doorNorthCrosses);
        UnlockDoor(doorSouthClosed, doorSouthTrigger, doorSouthCrosses);
        UnlockDoor(doorEastClosed,  doorEastTrigger,  doorEastCrosses);
        UnlockDoor(doorWestClosed,  doorWestTrigger,  doorWestCrosses);
        RoomManager.Instance.MarkRoomCleared(RoomManager.Instance.CurrentCellPublic);

        if (isBossRoom && levelExitSpawnPoint != null)
            RoomManager.Instance.SpawnLevelExit(levelExitSpawnPoint.position);

        Debug.Log("Rum cleared og døre låst op!");
    }

    void UnlockDoor(GameObject closed, Collider trigger, MeshRenderer[] crosses)
    {
        if (closed != null) closed.SetActive(false);
        if (trigger != null) trigger.enabled = true;
        SetDoorwayMaterial(crosses, false);
    }

    void SetDoorwayMaterial(MeshRenderer[] crosses, bool locked)
    {
        if (crosses == null) return;
        Material mat = locked ? doorwayLit : doorwayDim;
        foreach (var r in crosses)
            if (r != null) r.material = mat;
    }

    public Transform GetSpawnPoint(Direction from)
    {
        return from switch
        {
            Direction.North => spawnSouth,
            Direction.South => spawnNorth,
            Direction.East  => spawnWest,
            Direction.West  => spawnEast,
            _ => spawnSouth
        };
    }
}
