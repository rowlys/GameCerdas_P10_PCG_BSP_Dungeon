using System.Collections.Generic;
using UnityEngine;

// Validasi dungeon: Generate-and-Test, flood fill (BFS) untuk
// keterjangkauan Goal & seluruh room, serta strategi Goal C.
public partial class DungeonGenerator
{
    [Header("Corridor")]
    [SerializeField, Range(1, 3)] private int corridorWidth = 1;

    [Header("Validation (Generate-and-Test)")]
    [SerializeField, Range(1, 50)] private int maxGenerationAttempts = 10;
    [SerializeField] private bool requireAllRoomsReachable = true;

    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // Hasil BFS dari Start: jarak tiap cell (-1 = tidak terjangkau)
    private int[,] distanceFromStart;
    private Vector2Int[,] bfsParent;

    // Jalur wajib (shortest path) Start -> Goal
    private readonly List<Vector2Int> mainPath = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> mainPathSet = new HashSet<Vector2Int>();

    private bool isValid;
    private int attemptsUsed;
    private int requestedSeed;
    private int reachableRoomCount;
    private string validationMessage = "-";

    // Hanya batas teknis agar program tidak error / rekursi tidak macet.
    // Kombinasi parameter yang "buruk" (mis. leaf terlalu kecil untuk
    // room + margin) SENGAJA tidak dikoreksi: leaf tersebut tidak
    // mendapat room (seperti di modul), lalu validasi menolak dungeon
    // dan Generate-and-Test mencoba seed berikutnya.
    private void ApplyParameterConstraints()
    {
        corridorWidth = Mathf.Clamp(corridorWidth, 1, 3);
        minRoomSize = Mathf.Max(1, minRoomSize);
        roomMargin = Mathf.Max(0, roomMargin);
        minLeafSize = Mathf.Max(1, minLeafSize);   // 0 -> rekursi tak berujung
        maxLeafSize = Mathf.Max(1, maxLeafSize);
        mapWidth = Mathf.Max(8, mapWidth);
        mapHeight = Mathf.Max(8, mapHeight);
        maxGenerationAttempts = Mathf.Max(1, maxGenerationAttempts);
        tileSize = Mathf.Max(0.1f, tileSize);
    }

    // Border peta (baris/kolom 0 dan terakhir) selalu Wall.
    private bool IsInsideInterior(int x, int y)
    {
        return x >= 1 &&
               x < mapWidth - 1 &&
               y >= 1 &&
               y < mapHeight - 1;
    }

    private void PlaceStartAndGoalByStrategy()
    {
        switch (goalStrategy)
        {
            case GoalPlacementStrategy.LastRoom:
                PlaceStartAndGoal();
                break;

            case GoalPlacementStrategy.FarthestEuclidean:
                PlaceStartAndFarthestGoal();
                break;

            default:
                PlaceStartAndPathFarthestGoal();
                break;
        }
    }

    // Strategi C: Goal = room yang jarak BFS-nya paling jauh dari Start.
    private void PlaceStartAndPathFarthestGoal()
    {
        if (rooms.Count == 0)
            return;

        startPosition = rooms[0].Center;

        ComputeDistanceMap(startPosition);

        DungeonRoom farthestRoom = rooms[rooms.Count - 1];
        int farthestDistance = -1;

        foreach (DungeonRoom room in rooms)
        {
            Vector2Int center = room.Center;
            int distance = distanceFromStart[center.x, center.y];

            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestRoom = room;
            }
        }

        goalPosition = farthestRoom.Center;

        grid[startPosition.x, startPosition.y] = TileType.Start;
        grid[goalPosition.x, goalPosition.y] = TileType.Goal;
    }

    // Flood fill / BFS dari origin ke seluruh cell non-Wall.
    private int ComputeDistanceMap(Vector2Int origin)
    {
        distanceFromStart = new int[mapWidth, mapHeight];
        bfsParent = new Vector2Int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                distanceFromStart[x, y] = -1;
            }
        }

        if (!IsInsideGrid(origin.x, origin.y) ||
            grid[origin.x, origin.y] == TileType.Wall)
        {
            return 0;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(origin);
        distanceFromStart[origin.x, origin.y] = 0;
        bfsParent[origin.x, origin.y] = origin;

        int reachableTiles = 1;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int next = current + direction;

                if (!IsInsideGrid(next.x, next.y) ||
                    grid[next.x, next.y] == TileType.Wall ||
                    distanceFromStart[next.x, next.y] >= 0)
                {
                    continue;
                }

                distanceFromStart[next.x, next.y] =
                    distanceFromStart[current.x, current.y] + 1;

                bfsParent[next.x, next.y] = current;
                reachableTiles++;

                queue.Enqueue(next);
            }
        }

        return reachableTiles;
    }

    // Hard constraint: Goal dan seluruh room (area wajib) terjangkau
    // dari Start. Sekaligus menyimpan jalur wajib Start -> Goal.
    private bool ValidateReachability()
    {
        ComputeDistanceMap(startPosition);

        if (distanceFromStart[goalPosition.x, goalPosition.y] < 0)
        {
            validationMessage = "Goal tidak terjangkau";
            return false;
        }

        reachableRoomCount = 0;

        foreach (DungeonRoom room in rooms)
        {
            Vector2Int center = room.Center;

            if (distanceFromStart[center.x, center.y] >= 0)
            {
                reachableRoomCount++;
            }
        }

        if (requireAllRoomsReachable &&
            reachableRoomCount < rooms.Count)
        {
            validationMessage =
                (rooms.Count - reachableRoomCount) +
                " room terputus";
            return false;
        }

        BuildMainPath();

        validationMessage = "VALID";
        return true;
    }

    private void BuildMainPath()
    {
        mainPath.Clear();
        mainPathSet.Clear();

        Vector2Int current = goalPosition;

        while (current != startPosition)
        {
            mainPath.Add(current);
            current = bfsParent[current.x, current.y];
        }

        mainPath.Add(startPosition);
        mainPath.Reverse();

        foreach (Vector2Int tile in mainPath)
        {
            mainPathSet.Add(tile);
        }
    }

    private DungeonRoom FindRoomAt(Vector2Int position)
    {
        foreach (DungeonRoom room in rooms)
        {
            if (position.x >= room.x &&
                position.x < room.x + room.width &&
                position.y >= room.y &&
                position.y < room.y + room.height)
            {
                return room;
            }
        }

        return null;
    }
}
