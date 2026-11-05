using System.Collections.Generic;
using UnityEngine;

// Aturan spawn (Constraint-Based PCG) dan logika gameplay sederhana:
// zona aman Start, jalur wajib tidak terhalang, enemy density,
// Boss Room, Treasure Room, pickup item, dan Goal tercapai.
public partial class DungeonGenerator
{
    [Header("Spawn Rules (Constraint)")]
    [Tooltip("Enemy tidak boleh berada dalam radius ini dari Start (cell).")]
    [SerializeField] private float safeRadius = 8f;
    [Tooltip("Enemy & item hanya di dalam room, tidak di corridor.")]
    [SerializeField] private bool spawnInRoomsOnly = true;
    [Tooltip("Enemy & item tidak boleh di jalur wajib Start -> Goal.")]
    [SerializeField] private bool keepMainPathClear = true;
    [SerializeField] private bool useEnemyDensity = false;
    [Tooltip("1 enemy setiap N floor tile (jika useEnemyDensity aktif).")]
    [SerializeField] private int floorTilesPerEnemy = 50;

    // Challenge (pengembangan): default mati agar jumlah enemy/item
    // sama persis dengan Enemy Count / Item Count seperti di modul.
    [Header("Special Rooms (Challenge)")]
    [SerializeField] private bool spawnBoss = false;
    [SerializeField] private bool spawnTreasure = false;
    [SerializeField] private float bossScale = 1.6f;

    [Header("Gameplay")]
    [SerializeField] private float pickupRadius = 0.6f;

    private readonly HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
    private readonly HashSet<DungeonRoom> safeRooms = new HashSet<DungeonRoom>();
    private readonly List<Vector2Int> enemySpawnTiles = new List<Vector2Int>();
    private readonly List<Vector2Int> itemSpawnTiles = new List<Vector2Int>();
    private readonly List<GameObject> itemObjects = new List<GameObject>();

    private GameObject playerInstance;
    private GameObject treasureObject;

    private DungeonRoom startRoom;
    private DungeonRoom goalRoom;
    private DungeonRoom treasureRoom;

    private bool hasBoss;
    private bool hasTreasure;
    private Vector2Int bossTile;
    private Vector2Int treasureTile;

    private int itemsCollected;
    private bool treasureCollected;
    private bool goalReached;

    private void PrepareSpawnRules()
    {
        occupiedTiles.Clear();
        enemySpawnTiles.Clear();
        itemSpawnTiles.Clear();
        itemObjects.Clear();

        occupiedTiles.Add(startPosition);
        occupiedTiles.Add(goalPosition);

        itemsCollected = 0;
        treasureCollected = false;
        goalReached = false;
        hasBoss = false;
        hasTreasure = false;
        treasureObject = null;

        startRoom = FindRoomAt(startPosition);
        goalRoom = FindRoomAt(goalPosition);

        // safeRoomCount: N room pertama (dimulai dari Start Room)
        // bebas enemy. Goal Room tidak pernah menjadi safe room.
        safeRooms.Clear();

        for (int i = 0; i < Mathf.Min(safeRoomCount, rooms.Count); i++)
        {
            if (rooms[i] != goalRoom)
            {
                safeRooms.Add(rooms[i]);
            }
        }

        treasureRoom = spawnTreasure ? ChooseTreasureRoom() : null;
    }

    private int GetTargetEnemyCount()
    {
        if (!useEnemyDensity)
            return enemyCount;

        return Mathf.Max(
            1,
            CountTiles(TileType.Floor) / Mathf.Max(1, floorTilesPerEnemy)
        );
    }

    // Semua cell yang LOLOS constraint dikumpulkan dulu, lalu dipilih
    // secara acak. Tidak ada lagi loop i-- yang bisa berjalan tanpa akhir.
    private List<Vector2Int> CollectSpawnCandidates(
        bool forEnemy,
        DungeonRoom onlyRoom)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 1; x < mapWidth - 1; x++)
        {
            for (int y = 1; y < mapHeight - 1; y++)
            {
                // Hanya Floor (otomatis bukan Wall/Start/Goal)
                if (grid[x, y] != TileType.Floor)
                    continue;

                Vector2Int position = new Vector2Int(x, y);

                if (occupiedTiles.Contains(position))
                    continue;

                DungeonRoom room = FindRoomAt(position);

                if (onlyRoom != null && room != onlyRoom)
                    continue;

                if (spawnInRoomsOnly && room == null)
                    continue;

                if (keepMainPathClear && mainPathSet.Contains(position))
                    continue;

                float distanceFromStartCell =
                    Vector2Int.Distance(position, startPosition);

                if (forEnemy)
                {
                    if (room != null && safeRooms.Contains(room))
                        continue;

                    if (distanceFromStartCell < safeRadius)
                        continue;

                    // Enemy (collider padat) tidak boleh menutup pintu room.
                    if (IsDoorway(position, room))
                        continue;
                }
                else if (distanceFromStartCell < 1.5f)
                {
                    continue;
                }

                candidates.Add(position);
            }
        }

        return candidates;
    }

    private bool TakeRandomTile(
        List<Vector2Int> candidates,
        out Vector2Int position)
    {
        position = default;

        if (candidates.Count == 0)
            return false;

        int index = Random.Range(0, candidates.Count);
        position = candidates[index];

        // swap-remove agar O(1)
        candidates[index] = candidates[candidates.Count - 1];
        candidates.RemoveAt(candidates.Count - 1);

        occupiedTiles.Add(position);
        return true;
    }

    // Treasure Room: room samping (tidak dilewati jalur wajib),
    // bukan Start/Goal, dan paling jauh dari Start.
    private DungeonRoom ChooseTreasureRoom()
    {
        DungeonRoom best = null;
        int bestScore = int.MinValue;

        foreach (DungeonRoom room in rooms)
        {
            if (room == startRoom || room == goalRoom)
                continue;

            bool onMainPath = false;

            foreach (Vector2Int tile in mainPath)
            {
                if (FindRoomAt(tile) == room)
                {
                    onMainPath = true;
                    break;
                }
            }

            Vector2Int center = room.Center;
            int score = distanceFromStart[center.x, center.y] +
                        (onMainPath ? 0 : 10000);

            if (score > bestScore)
            {
                bestScore = score;
                best = room;
            }
        }

        return best;
    }

    private void SpawnSpecialRooms()
    {
        SpawnBossInGoalRoom();
        SpawnTreasureInSideRoom();
    }

    // Boss hanya boleh spawn di Goal Room dan tidak menempel jalur wajib.
    private void SpawnBossInGoalRoom()
    {
        if (!spawnBoss || enemyPrefab == null || goalRoom == null)
            return;

        List<Vector2Int> candidates = CollectSpawnCandidates(true, goalRoom);
        candidates.RemoveAll(IsNextToMainPath);

        if (!TakeRandomTile(candidates, out bossTile))
        {
            Debug.LogWarning("Boss tidak muat di Goal Room.");
            return;
        }

        hasBoss = true;

        GameObject boss = Instantiate(
            enemyPrefab,
            GridToWorld(bossTile.x, bossTile.y) + Vector3.up * bossScale,
            Quaternion.identity,
            levelRoot
        );

        boss.name = "Boss";
        boss.transform.localScale *= bossScale;
        Tint(boss, new Color(0.45f, 0.05f, 0.1f));
    }

    private void SpawnTreasureInSideRoom()
    {
        if (treasureRoom == null || itemPrefab == null)
            return;

        List<Vector2Int> candidates =
            CollectSpawnCandidates(false, treasureRoom);

        if (candidates.Count == 0)
            return;

        // Treasure diletakkan sedekat mungkin dengan pusat room.
        Vector2Int center = treasureRoom.Center;
        candidates.Sort((a, b) =>
            Vector2Int.Distance(a, center).CompareTo(
                Vector2Int.Distance(b, center)));

        treasureTile = candidates[0];
        occupiedTiles.Add(treasureTile);
        hasTreasure = true;

        treasureObject = Instantiate(
            itemPrefab,
            GridToWorld(treasureTile.x, treasureTile.y) + Vector3.up * 0.5f,
            Quaternion.identity,
            levelRoot
        );

        treasureObject.name = "Treasure";
        treasureObject.transform.localScale *= 2f;
        Tint(treasureObject, new Color(1f, 0.75f, 0.1f));
        MakeTrigger(treasureObject);
    }

    // Pintu = cell yang bersebelahan dengan floor di luar room-nya
    // (mulut corridor). Untuk cell corridor: corridor selebar 1 cell
    // selalu dianggap pintu karena enemy di sana pasti menutup jalan.
    private bool IsDoorway(Vector2Int position, DungeonRoom room)
    {
        if (room == null)
            return corridorWidth == 1;

        foreach (Vector2Int direction in FourDirections)
        {
            Vector2Int next = position + direction;

            if (!IsInsideGrid(next.x, next.y) ||
                grid[next.x, next.y] == TileType.Wall)
            {
                continue;
            }

            if (FindRoomAt(next) != room)
                return true;
        }

        return false;
    }

    private bool IsNextToMainPath(Vector2Int position)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (mainPathSet.Contains(position + new Vector2Int(dx, dy)))
                    return true;
            }
        }

        return false;
    }

    private int CountTiles(TileType type)
    {
        int count = 0;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (grid[x, y] == type)
                    count++;
            }
        }

        return count;
    }

    private static void MakeTrigger(GameObject target)
    {
        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            collider.isTrigger = true;
        }
    }

    private static void Tint(GameObject target, Color color)
    {
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            renderer.material.color = color;
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Dipanggil dari Update(): pickup item/treasure dan cek Goal.
    private void UpdateGameplay()
    {
        if (playerInstance == null || !isValid)
            return;

        Vector3 playerPosition = playerInstance.transform.position;

        for (int i = itemObjects.Count - 1; i >= 0; i--)
        {
            GameObject item = itemObjects[i];

            if (item == null)
            {
                itemObjects.RemoveAt(i);
                continue;
            }

            if (FlatDistance(playerPosition, item.transform.position) < pickupRadius)
            {
                itemsCollected++;
                itemObjects.RemoveAt(i);
                Destroy(item);
            }
        }

        if (treasureObject != null && !treasureCollected &&
            FlatDistance(playerPosition, treasureObject.transform.position) < pickupRadius)
        {
            treasureCollected = true;
            Destroy(treasureObject);
            Debug.Log("Treasure ditemukan!");
        }

        if (!goalReached &&
            FlatDistance(playerPosition, GridToWorld(goalPosition.x, goalPosition.y)) < pickupRadius)
        {
            goalReached = true;

            Debug.Log(
                "GOAL TERCAPAI! Seed " + seed +
                " | Item " + itemsCollected + "/" + itemSpawnTiles.Count +
                " | Treasure " + (treasureCollected ? "ya" : "tidak")
            );
        }
    }
}
