using UnityEngine;
using System.Collections.Generic;

public class RoomController : MonoBehaviour
{
    [Header("Door Slots")]
    public GameObject doorNorth;
    public GameObject doorSouth;
    public GameObject doorEast;
    public GameObject doorWest;

    [Header("Spawn Points (på hver dør)")]
    public Transform spawnNorth;
    public Transform spawnSouth;
    public Transform spawnEast;
    public Transform spawnWest;

    [Header("Enemy Spawn Points")]
    public Transform[] enemySpawnPoints;

    public void SetDoors(bool north, bool south, bool east, bool west)
    {
        SetDoor(doorNorth, north);
        SetDoor(doorSouth, south);
        SetDoor(doorEast, east);
        SetDoor(doorWest, west);
    }

    void SetDoor(GameObject door, bool open)
    {
        if (door == null) return;
        // Aktivér/deaktivér glow og åben visuel
        door.SetActive(open);
    }

    public Transform GetSpawnPoint(Direction from)
    {
        return from switch
        {
            Direction.North => spawnSouth, // kommer fra nord → spawn ved syd-dør
            Direction.South => spawnNorth,
            Direction.East  => spawnWest,
            Direction.West  => spawnEast,
            _ => spawnSouth
        };
    }
}