using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Procedurally generates a dungeon layout inspired by The Binding of Isaac.
///
/// The dungeon is represented as a flat integer grid where each cell's coordinates
/// are encoded as: cellIndex = (y * GridWidth) + x.
/// Adjacency is therefore: North = +GridWidth, South = -GridWidth, East = +1, West = -1.
///
/// Generation steps:
///   1. BFS expansion from a fixed start cell to fill a target room count.
///   2. Validation (enough dead ends, boss room not adjacent to start).
///   3. Special room assignment (Boss, Treasure, Shop) at dead ends.
///   4. Straight-through Normal rooms are promoted to Corridor types.
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>
    /// Width of the logical grid. Cells are addressed as y * GridWidth + x.
    /// Must stay at 10 to keep the integer-encoding scheme valid.
    /// </summary>
    private const int GridWidth = 10;

    /// <summary>The cell index used as the player's starting room.</summary>
    private const int StartCell = 35;

    /// <summary>
    /// Offsets for the four cardinal directions in cell-index space.
    /// North/South step by <see cref="GridWidth"/>; East/West step by 1.
    /// </summary>
    private static readonly int[] Directions = { GridWidth, -GridWidth, 1, -1 };

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------

    /// <summary>
    /// The generated dungeon, keyed by cell index with each cell's <see cref="RoomType"/>.
    /// Populated after a successful call to <see cref="Generate"/>.
    /// </summary>
    public Dictionary<int, RoomType> DungeonMap { get; private set; } = new();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a complete dungeon for the given <paramref name="level"/>.
    /// Re-runs BFS until a valid layout is produced, then assigns special rooms
    /// and corridor types.
    /// </summary>
    /// <param name="level">Current dungeon depth; higher values yield more rooms.</param>
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

    /// <summary>
    /// Returns all rooms directly adjacent (N/S/E/W) to <paramref name="cell"/>
    /// that exist in the current <see cref="DungeonMap"/>, keyed by direction.
    /// </summary>
    public Dictionary<Direction, int> GetNeighbours(int cell)
    {
        var result = new Dictionary<Direction, int>();

        if (DungeonMap.ContainsKey(cell + GridWidth))  result[Direction.North] = cell + GridWidth;
        if (DungeonMap.ContainsKey(cell - GridWidth))  result[Direction.South] = cell - GridWidth;
        if (DungeonMap.ContainsKey(cell + 1))          result[Direction.East]  = cell + 1;
        if (DungeonMap.ContainsKey(cell - 1))          result[Direction.West]  = cell - 1;

        return result;
    }

    // -------------------------------------------------------------------------
    // Generation pipeline
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs a single BFS expansion attempt and validates the result.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the generated layout meets all validity constraints;
    /// <c>false</c> if the caller should retry.
    /// </returns>
    private bool TryGenerate(int level)
    {
        int targetRooms = baseRoomCount
            + Random.Range(0, roomCountVariance + 1)
            + Mathf.FloorToInt(level * roomScalePerLevel);

        targetRooms = Mathf.Clamp(targetRooms, minRooms, maxRooms);

        // BFS from the start cell — using Queue for O(1) dequeue (vs List.RemoveAt(0) which is O(n))
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

    /// <summary>
    /// Validates the generated layout:
    ///   - At least two dead ends must exist (one for boss, one for treasure/shop).
    ///   - The boss room candidate must not be directly adjacent to the start.
    /// </summary>
    private bool IsLayoutValid()
    {
        List<int> deadEnds = GetDeadEnds();
        if (deadEnds.Count < 2) return false;

        int bossCandidate    = FarthestCell(deadEnds, StartCell);
        bool bossAdjacentToStart = Directions.Any(d => bossCandidate + d == StartCell);

        return !bossAdjacentToStart;
    }

    /// <summary>
    /// Promotes dead-end rooms to special types: Boss (farthest from start),
    /// then Treasure and Shop from the remaining dead ends.
    /// </summary>
    private void AssignSpecialRooms()
    {
        // GetDeadEnds excludes the start cell by definition
        List<int> deadEnds = GetDeadEnds();

        int bossCell = FarthestCell(deadEnds, StartCell);
        DungeonMap[bossCell] = RoomType.Boss;
        deadEnds.Remove(bossCell);

        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Treasure; deadEnds.RemoveAt(0); }
        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Shop;     deadEnds.RemoveAt(0); }
    }

    /// <summary>
    /// Promotes Normal rooms that form straight north–south or east–west corridors
    /// (exactly two opposing connections) to the appropriate <see cref="RoomType"/> corridor variant.
    /// </summary>
    private void AssignCorridors()
    {
        // ToList() snapshots the keys before iteration — necessary because assigning a new
        // RoomType value invalidates the dictionary's enumerator, even though no keys change.
        foreach (int cell in DungeonMap.Keys.ToList())
        {
            if (DungeonMap[cell] != RoomType.Normal) continue;

            bool n = DungeonMap.ContainsKey(cell + GridWidth);
            bool s = DungeonMap.ContainsKey(cell - GridWidth);
            bool e = DungeonMap.ContainsKey(cell + 1);
            bool w = DungeonMap.ContainsKey(cell - 1);

            int connections = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

            if (connections != 2) continue;

            if (n && s) DungeonMap[cell] = RoomType.CorridorNS;
            else if (e && w) DungeonMap[cell] = RoomType.CorridorEW;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all rooms that have exactly one neighbour and are not the start room.
    /// These are candidate locations for boss, treasure, and shop rooms.
    /// </summary>
    private List<int> GetDeadEnds()
    {
        return DungeonMap.Keys
            .Where(c => CountNeighbours(c) == 1 && DungeonMap[c] != RoomType.Start)
            .ToList();
    }

    /// <summary>
    /// Returns the number of cardinal neighbours of <paramref name="cell"/>
    /// that exist in the current <see cref="DungeonMap"/>.
    /// </summary>
    private int CountNeighbours(int cell)
    {
        return Directions.Count(d => DungeonMap.ContainsKey(cell + d));
    }

    /// <summary>
    /// Returns the cell from <paramref name="candidates"/> with the greatest
    /// Manhattan distance from <paramref name="origin"/>.
    /// </summary>
    private int FarthestCell(IEnumerable<int> candidates, int origin)
    {
        return candidates.OrderByDescending(c => ManhattanDistance(c, origin)).First();
    }

    /// <summary>
    /// Computes the Manhattan distance between two cell indices.
    ///
    /// ⚠️ Assumes all cell indices are in the range [0, 99] (a 10×10 grid).
    /// Values outside this range will produce incorrect results because the
    /// x-coordinate is derived via modulo 10.
    /// </summary>
    private int ManhattanDistance(int a, int b)
    {
        int ax = a % GridWidth, ay = a / GridWidth;
        int bx = b % GridWidth, by = b / GridWidth;
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }
}