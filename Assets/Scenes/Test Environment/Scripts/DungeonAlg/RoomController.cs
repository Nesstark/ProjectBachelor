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

    [Header("Doorway Visuals")]
    public MeshRenderer[] doorNorthCrosses;
    public MeshRenderer[] doorSouthCrosses;
    public MeshRenderer[] doorEastCrosses;
    public MeshRenderer[] doorWestCrosses;
    public Material doorwayLit;
    public Material doorwayDim;

    [Header("Enemy Spawn Points")]
    public Transform[] enemySpawnPoints;

    public void SetDoors(bool north, bool south, bool east, bool west)
    {
        ConfigureDoor(doorNorth, doorNorthClosed, doorNorthTrigger, doorNorthWall, doorNorthCrosses, north);
        ConfigureDoor(doorSouth, doorSouthClosed, doorSouthTrigger, doorSouthWall, doorSouthCrosses, south);
        ConfigureDoor(doorEast,  doorEastClosed,  doorEastTrigger,  doorEastWall,  doorEastCrosses,  east);
        ConfigureDoor(doorWest,  doorWestClosed,  doorWestTrigger,  doorWestWall,  doorWestCrosses,  west);
    }

    void ConfigureDoor(GameObject door, GameObject closed, Collider trigger, GameObject wall, MeshRenderer[] crosses, bool hasExit)
    {
        // Hele dør-slottet aktiv/inaktiv baseret på om der er en nabo
        if (door != null) door.SetActive(hasExit);

        // Wall block lukker åbningen hvis ingen exit
        if (wall != null) wall.SetActive(!hasExit);

        if (!hasExit) return;

        // Dør starter altid locked — UnlockDoors() kaldes når fjender er besejret
        if (closed != null) closed.SetActive(true);
        if (trigger != null) trigger.enabled = false;

        // Start med lit materiale (låst tilstand)
        SetDoorwayMaterial(crosses, true);
    }

    public void UnlockDoors()
    {
        UnlockDoor(doorNorthClosed, doorNorthTrigger, doorNorthCrosses);
        UnlockDoor(doorSouthClosed, doorSouthTrigger, doorSouthCrosses);
        UnlockDoor(doorEastClosed,  doorEastTrigger,  doorEastCrosses);
        UnlockDoor(doorWestClosed,  doorWestTrigger,  doorWestCrosses);
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