using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public int seed = 0;
    public bool useRandomSeed = true;

    public Dictionary<int, RoomType> DungeonMap { get; private set; } = new();

    public void Generate(int level)
    {
        if (useRandomSeed) seed = Random.Range(0, 99999);
        Random.InitState(seed);

        bool valid = false;
        while (!valid)
        {
            DungeonMap.Clear();
            valid = TryGenerate(level);
        }

        AssignSpecialRooms();
        AssignCorridors();
    }

    bool TryGenerate(int level)
    {
        int targetRooms = Random.Range(0, 2) + 5 + Mathf.FloorToInt(level * 2.6f);
        targetRooms = Mathf.Clamp(targetRooms, 5, 20);

        List<int> queue = new() { 35 };
        DungeonMap[35] = RoomType.Start;

        while (queue.Count > 0 && DungeonMap.Count < targetRooms)
        {
            int cell = queue[0];
            queue.RemoveAt(0);

            foreach (int dir in new[] { 10, -10, 1, -1 })
            {
                int neighbour = cell + dir;
                if (DungeonMap.ContainsKey(neighbour)) continue;
                if (CountNeighbours(neighbour) > 1) continue;
                if (DungeonMap.Count >= targetRooms) break;
                if (Random.value < 0.5f) continue;

                DungeonMap[neighbour] = RoomType.Normal;
                queue.Add(neighbour);
            }
        }

        List<int> deadEnds = GetDeadEnds();
        if (deadEnds.Count < 2) return false;

        int bossCell = deadEnds.OrderByDescending(c => ManhattanDistance(c, 35)).First();
        bool bossNextToStart = new[] { 10, -10, 1, -1 }.Any(d => bossCell + d == 35);
        if (bossNextToStart) return false;

        return true;
    }

    void AssignSpecialRooms()
    {
        List<int> deadEnds = GetDeadEnds();
        deadEnds.Remove(35);

        int bossCell = deadEnds.OrderByDescending(c => ManhattanDistance(c, 35)).First();
        DungeonMap[bossCell] = RoomType.Boss;
        deadEnds.Remove(bossCell);

        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Treasure; deadEnds.RemoveAt(0); }
        if (deadEnds.Count > 0) { DungeonMap[deadEnds[0]] = RoomType.Shop; deadEnds.RemoveAt(0); }
    }

    void AssignCorridors()
    {
        foreach (int cell in DungeonMap.Keys.ToList())
        {
            if (DungeonMap[cell] != RoomType.Normal) continue;

            bool n = DungeonMap.ContainsKey(cell + 10);
            bool s = DungeonMap.ContainsKey(cell - 10);
            bool e = DungeonMap.ContainsKey(cell + 1);
            bool w = DungeonMap.ContainsKey(cell - 1);
            int connections = new[] { n, s, e, w }.Count(x => x);

            if (connections == 2)
            {
                if (n && s) DungeonMap[cell] = RoomType.CorridorNS;
                else if (e && w) DungeonMap[cell] = RoomType.CorridorEW;
            }
        }
    }

    List<int> GetDeadEnds()
    {
        return DungeonMap.Keys
            .Where(c => CountNeighbours(c) == 1 && DungeonMap[c] != RoomType.Start)
            .ToList();
    }

    int CountNeighbours(int cell)
    {
        return new[] { 10, -10, 1, -1 }.Count(d => DungeonMap.ContainsKey(cell + d));
    }

    int ManhattanDistance(int a, int b)
    {
        int ax = a % 10, ay = a / 10;
        int bx = b % 10, by = b / 10;
        return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
    }

    public Dictionary<Direction, int> GetNeighbours(int cell)
    {
        var result = new Dictionary<Direction, int>();
        if (DungeonMap.ContainsKey(cell + 10)) result[Direction.North] = cell + 10;
        if (DungeonMap.ContainsKey(cell - 10)) result[Direction.South] = cell - 10;
        if (DungeonMap.ContainsKey(cell + 1))  result[Direction.East]  = cell + 1;
        if (DungeonMap.ContainsKey(cell - 1))  result[Direction.West]  = cell - 1;
        return result;
    }
}