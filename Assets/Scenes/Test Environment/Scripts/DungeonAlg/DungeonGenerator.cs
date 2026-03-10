using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Procedurally generates a dungeon layout inspired by The Binding of Isaac.
//
// The dungeon is a flat integer grid: cellIndex = (y * GridWidth) + x.
// Adjacency offsets: North = +GridWidth, South = -GridWidth, East = +1, West = -1.
//
// Pipeline: BFS expansion -> validation -> special room assignment -> corridor promotion.
public class DungeonGenerator : MonoBehaviour
{
    [Header("Seed")]
    public int  seed          = 0;
    public bool useRandomSeed = true;

    [Header("Room Count")]
    [Tooltip("Base number of rooms before level scaling is applied.")]
    public int baseRoomCount = 5;

    [Tooltip("Multiplier applied to the level to scale room count.")]
    public float roomScalePerLevel = 2.6f;

    [Tooltip("Random bonus rooms added on top of the scaled count (0 to this value).")]
    public int roomCountVariance = 2;

    [Tooltip("Hard clamp on the total number of rooms generated.")]
    public int minRooms = 5;
    public int maxRooms = 20;

    [Header("Generation")]
    [Tooltip("Probability (0-1) that a candidate neighbour cell is added during BFS expansion.")]
    [Range(0f, 1f)]
    public float expansionChance = 0.5f;

    [Tooltip("Maximum attempts to generate a valid map before giving up.")]
    public int maxGenerationAttempts = 100;

    // Grid width must stay at 10 to keep the integer-encoding scheme valid.
    private const int GridWidth = 10;
    private const int StartCell = 35;

    private static readonly int[] Directions = { GridWidth, -GridWidth, 1, -1 };

    public Dictionary<int, RoomType> DungeonMap { get; private set; } = new();

    public void Generate(int level)
    {
        if (useRandomSeed)
            seed = Random.Range(0, 99999);

        Random.InitState(seed);

        int attempts = 0;
        bool valid   = false;

        while (!valid)
        {
            if (++attempts > maxGenerationAttempts)
            {
                Debug.LogError($"[DungeonGenerator] Failed to generate a valid dungeon after {maxGenerationAttempts} attempts (seed={seed}, level={level}).");
                return;
            }

            DungeonMap.Clear();
            valid = TryGenerate(level);
        }

        AssignSpecialRooms();
        AssignCorridors();
    }

    public Dictionary<Direction, int> GetNeighbours(int cell)
    {
        var result = new Dictionary<Direction, int>();

        if (DungeonMap.ContainsKey(cell + GridWidth))  result[Direction.North] = cell + GridWidth;
        if (DungeonMap.ContainsKey(cell - GridWidth))  result[Direction.South] = cell - GridWidth;
        if (DungeonMap.ContainsKey(cell + 1))          result[Direction.East]  = cell + 1;
        if (DungeonMap.ContainsKey(cell - 1))          result[Direction.West]  = cell - 1;

        return result;
    }

    private bool TryGenerate(int level)
    {
        int targetRooms = baseRoomCount
            + Random.Range(0, roomCountVariance + 1)
            + Mathf.FloorToInt(level * roomScalePerLevel);

        targetRooms = Mathf.Clamp(targetRooms, minRooms, maxRooms);

        // Queue gives O(1) dequeue; List.RemoveAt(0) would be O(n).
        var queue = new Queue<int>();
        queue.Enqueue(StartCell);
        DungeonMap[StartCell] = RoomType.Start;

        while (queue.Count > 0 && DungeonMap.Count < targetRooms)
        {
            int cell = queue.Dequeue();

            foreach (int dir in Directions)
            {
                if (DungeonMap.Count >= targetRooms) break;

                int neighbour = cell + dir;

                if (DungeonMap.ContainsKey(neighbour)) continue;   // already visited
                if (CountNeighbours(neighbour) > 1)    continue;   // would create a loop
                if (Random.value < expansionChance)    continue;   // stochastic branching

                DungeonMap[neighbour] = RoomType.Normal;
                queue.Enqueue(neighbour);
            }
        }

        return IsLayoutValid();
    }

    private bool IsLayoutValid()
    {
        List<int> deadEnds = GetDeadEnds();
        if (deadEnds.Count < 2) return false;

        int  bossCandidate       = FarthestCell(deadEnds, StartCell);
        bool bossAdjacentToStart = Directions.Any(d => bossCandidate + d == StartCell);

        return !bossAdjacentToStart;
    }

    // Boss goes to the dead end farthest from start; Treasure and Shop take the next available.
    private void AssignSpecialRooms()
    {
        List<int> deadEnds = GetDeadEnds();

        int bossCell = FarthestCell(deadEnds, StartCell);
        DungeonMap[bossCell] = RoomType.Boss;
        deadEnds.Remove(bossCell);

        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Treasure; deadEnds.RemoveAt(0); }
        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Shop;     deadEnds.RemoveAt(0); }
    }

    private void AssignCorridors()
    {
        // ToList() snapshots keys first -- modifying a value still invalidates the enumerator.
        foreach (int cell in DungeonMap.Keys.ToList())
        {
            if (DungeonMap[cell] != RoomType.Normal) continue;

            bool n = DungeonMap.ContainsKey(cell + GridWidth);
            bool s = DungeonMap.ContainsKey(cell - GridWidth);
            bool e = DungeonMap.ContainsKey(cell + 1);
            bool w = DungeonMap.ContainsKey(cell - 1);

            int connections = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

            if (connections != 2) continue;

            if      (n && s) DungeonMap[cell] = RoomType.CorridorNS;
            else if (e && w) DungeonMap[cell] = RoomType.CorridorEW;
        }
    }

    private List<int> GetDeadEnds()
    {
        return DungeonMap.Keys
            .Where(c => CountNeighbours(c) == 1 && DungeonMap[c] != RoomType.Start)
            .ToList();
    }

    private int CountNeighbours(int cell)
    {
        return Directions.Count(d => DungeonMap.ContainsKey(cell + d));
    }

    private int FarthestCell(IEnumerable<int> candidates, int origin)
    {
        return candidates.OrderByDescending(c => ManhattanDistance(c, origin)).First();
    }

    // NOTE: Only valid for cell indices in range [0, 99]. The x-coordinate is derived
    // via modulo 10, so values outside that range will produce incorrect results.
    private int ManhattanDistance(int a, int b)
    {
        int ax = a % GridWidth, ay = a / GridWidth;
        int bx = b % GridWidth, by = b / GridWidth;
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }
}