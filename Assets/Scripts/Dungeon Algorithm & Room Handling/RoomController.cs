using UnityEngine;
using System.Collections.Generic;

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

    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;
    public GameObject[] bossPrefabs;

    [Header("Encounter Settings")]
    // Normal rooms: 3-6 enemies. Shop rooms: 4-8 (ambush).
    // Boss rooms always spawn exactly 1. No public count needed.
    int remainingEnemies = 0;
    bool encounterActive = false;

    [Header("Boss Settings")]
    public bool isBossRoom = false;
    public Transform levelExitSpawnPoint;

    [Header("Room Type")]
    public RoomType roomType = RoomType.Normal;

    [Header("Treasure Room")]
    [Tooltip("Drag all 4 pickup prefabs here. Two will be chosen randomly.")]
    public GameObject[] pickupPrefabs;

    [Tooltip("Two empty child GameObjects placed where the items should appear (e.g. on pedestals).")]
    public Transform[] treasureSpawnPoints;

    // ── Private state ─────────────────────────────────────────
    private GameObject _treasureA;
    private GameObject _treasureB;
    private bool _treasureChosen = false;

    // Static list of every enemy spawned this floor.
    // Cleared by CleanupForNextLevel() before loading a new floor.
    private static readonly List<GameObject> _allSpawnedEnemies = new List<GameObject>();

    // ─────────────────────────────────────────────────────────
    // DOOR SETUP
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // ENCOUNTER
    // ─────────────────────────────────────────────────────────

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
        GameObject[] prefabsToUse = isBossRoom ? bossPrefabs : enemyPrefabs;

        if (prefabsToUse == null || prefabsToUse.Length == 0 ||
            enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            UnlockDoors();
            return;
        }

        LockDoors();

        // Boss: always 1. Shop: ambush (4-8). Normal: 3-6.
        int spawnCount;
        if      (isBossRoom)               spawnCount = 1;
        else if (roomType == RoomType.Shop) spawnCount = Mathf.Min(Random.Range(4, 9), enemySpawnPoints.Length);
        else                               spawnCount = Mathf.Min(Random.Range(3, 7), enemySpawnPoints.Length);

        remainingEnemies = spawnCount;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = prefabsToUse[Random.Range(0, prefabsToUse.Length)];
            GameObject enemy  = Instantiate(prefab, enemySpawnPoints[i].position, enemySpawnPoints[i].rotation);
            _allSpawnedEnemies.Add(enemy);
        }

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

    // ─────────────────────────────────────────────────────────
    // DOOR LOCK / UNLOCK
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // LEVEL CLEANUP
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by RoomManager.DoLevelUpTransition() before generating
    /// the next floor. Destroys every enemy spawned this level so
    /// none carry over into the new floor's start room.
    /// </summary>
    public static void CleanupForNextLevel()
    {
        foreach (GameObject enemy in _allSpawnedEnemies)
            if (enemy != null) Destroy(enemy);

        _allSpawnedEnemies.Clear();
        Debug.Log("[RoomController] Spawned enemies cleaned up for next level.");
    }

    // ─────────────────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // TREASURE ROOM
    // ─────────────────────────────────────────────────────────

    public void TriggerTreasure()
    {
        if (RoomManager.Instance.IsRoomCleared(RoomManager.Instance.CurrentCellPublic))
        {
            UnlockDoors();
            return;
        }

        if (pickupPrefabs == null || pickupPrefabs.Length < 2 ||
            treasureSpawnPoints == null || treasureSpawnPoints.Length < 2)
        {
            Debug.LogWarning("[TreasureRoom] pickupPrefabs or treasureSpawnPoints not assigned — unlocking.", this);
            UnlockDoors();
            return;
        }

        _treasureChosen = false;
        LockDoors();

        int idxA = Random.Range(0, pickupPrefabs.Length);
        int idxB;
        do { idxB = Random.Range(0, pickupPrefabs.Length); } while (idxB == idxA);

        _treasureA = Instantiate(pickupPrefabs[idxA], treasureSpawnPoints[0].position, Quaternion.identity);
        _treasureB = Instantiate(pickupPrefabs[idxB], treasureSpawnPoints[1].position, Quaternion.identity);

        PickupBase.OnAnyPickupCollected -= HandleTreasurePickup;
        PickupBase.OnAnyPickupCollected += HandleTreasurePickup;

        Debug.Log($"[TreasureRoom] Offering: {pickupPrefabs[idxA].name} vs {pickupPrefabs[idxB].name}");
    }

    private void HandleTreasurePickup(PickupBase collected)
    {
        if (_treasureChosen) return;

        bool isA = _treasureA != null && collected.gameObject == _treasureA;
        bool isB = _treasureB != null && collected.gameObject == _treasureB;
        if (!isA && !isB) return;

        _treasureChosen = true;
        PickupBase.OnAnyPickupCollected -= HandleTreasurePickup;

        GameObject unchosen = isA ? _treasureB : _treasureA;
        if (unchosen != null) Destroy(unchosen);

        UnlockDoors();
        Debug.Log($"[TreasureRoom] Player picked {collected.gameObject.name} — unchosen item removed, doors unlocked.");
    }

    private void OnDestroy()
    {
        PickupBase.OnAnyPickupCollected -= HandleTreasurePickup;
    }
}