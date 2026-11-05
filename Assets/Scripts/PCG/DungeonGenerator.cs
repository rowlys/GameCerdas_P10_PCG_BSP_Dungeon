using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// partial: fitur tambahan (validasi, aturan spawn, panel/overlay)
// dipisah ke file DungeonGenerator.*.cs agar kode modul tetap utuh.
public partial class DungeonGenerator : MonoBehaviour
{
    [Header("Dungeon Size")]
    [SerializeField] private int mapWidth = 60;
    [SerializeField] private int mapHeight = 40;
    [SerializeField] private float tileSize = 1f;

    [Header("BSP Settings")]
    [SerializeField] private int minLeafSize = 12;
    [SerializeField] private int maxLeafSize = 20;

    [Header("Room Settings")]
    [SerializeField] private int minRoomSize = 5;
    [SerializeField] private int roomMargin = 2;

    // Menggantikan bool farthestGoal: A = room terakhir,
    // B = terjauh (Euclidean, rekomendasi modul bagian 64),
    // C = terjauh (jarak BFS / shortest path, challenge bagian 108).
    [SerializeField] private GoalPlacementStrategy goalStrategy =
        GoalPlacementStrategy.FarthestEuclidean;

    [Header("Generation")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool randomSeed = false;

    [Header("Prefabs")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject goalPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject itemPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int enemyCount = 8;
    [SerializeField] private int itemCount = 5;
    [SerializeField] private int safeRoomCount = 1;

    private TileType[,] grid;

    private BSPNode rootNode;

    private List<BSPNode> leafNodes = new List<BSPNode>();
    private List<DungeonRoom> rooms = new List<DungeonRoom>();

    private Transform levelRoot;

    private Vector2Int startPosition;
    private Vector2Int goalPosition;

    private void Start()
    {
        GenerateDungeon();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame &&
            !DungeonControlPanel.IsTyping)
        {
            seed++;
            GenerateDungeon();
        }

        UpdateGameplay();
    }

    public void GenerateDungeon()
    {
        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        ClearDungeon();

        ApplyParameterConstraints();

        SetupSeed();

        requestedSeed = seed;
        isValid = false;

        // Generate-and-Test: Generate -> Validate -> (gagal? seed+1, ulangi)
        for (attemptsUsed = 1;
            attemptsUsed <= maxGenerationAttempts;
            attemptsUsed++)
        {
            if (attemptsUsed > 1)
            {
                seed++;
                Random.InitState(seed);
            }

            InitializeGrid();

            CreateBSP();

            CreateRooms();

            CreateCorridors();

            PlaceStartAndGoalByStrategy();

            if (ValidateDungeon() &&
                HasPathToGoal() &&
                ValidateReachability())
            {
                isValid = true;
                break;
            }

            Debug.LogWarning(
                "Percobaan " + attemptsUsed + " (seed " + seed +
                ") tidak valid: " + validationMessage +
                ". Generate ulang..."
            );
        }

        if (!isValid)
        {
            attemptsUsed = maxGenerationAttempts;

            Debug.LogError(
                "Dungeon gagal dibuat setelah " +
                maxGenerationAttempts +
                " percobaan. Ubah parameter generator."
            );

            ComputeMetrics(stopwatch);
            return;
        }

        BuildDungeon();

        SpawnGameplayObjects();

        BuildDebugOverlay();

        ComputeMetrics(stopwatch);

        Debug.Log("Dungeon generated with seed: " + seed +
                  " (percobaan ke-" + attemptsUsed + ")\n" +
                  metrics.ToLogString());
    }

    private void SetupSeed()
    {
        if (randomSeed)
        {
            seed = System.DateTime.Now.GetHashCode();
        }

        Random.InitState(seed);
    }

    private void InitializeGrid()
    {
        grid = new TileType[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                grid[x, y] = TileType.Wall;
            }
        }
    }

    private void CreateBSP()
    {
        rootNode = new BSPNode(
            1,
            1,
            mapWidth - 2,
            mapHeight - 2
        );

        leafNodes.Clear();

        SplitNode(rootNode);
    }

    private void SplitNode(BSPNode node)
    {
        if (node.width <= maxLeafSize &&
            node.height <= maxLeafSize)
        {
            leafNodes.Add(node);
            return;
        }

        bool splitHorizontal;

        if (node.width > node.height * 1.25f)
        {
            splitHorizontal = false;
        }
        else if (node.height > node.width * 1.25f)
        {
            splitHorizontal = true;
        }
        else
        {
            splitHorizontal = Random.value > 0.5f;
        }

        if (splitHorizontal)
        {
            SplitHorizontal(node);
        }
        else
        {
            SplitVertical(node);
        }
    }

    private void SplitHorizontal(BSPNode node)
    {
        if (node.height < minLeafSize * 2)
        {
            leafNodes.Add(node);
            return;
        }

        int split = Random.Range(
            minLeafSize,
            node.height - minLeafSize
        );

        node.left = new BSPNode(
            node.x,
            node.y,
            node.width,
            split
        );

        node.right = new BSPNode(
            node.x,
            node.y + split,
            node.width,
            node.height - split
        );

        SplitNode(node.left);
        SplitNode(node.right);
    }

    private void SplitVertical(BSPNode node)
    {
        if (node.width < minLeafSize * 2)
        {
            leafNodes.Add(node);
            return;
        }

        int split = Random.Range(
            minLeafSize,
            node.width - minLeafSize
        );

        node.left = new BSPNode(
            node.x,
            node.y,
            split,
            node.height
        );

        node.right = new BSPNode(
            node.x + split,
            node.y,
            node.width - split,
            node.height
        );

        SplitNode(node.left);
        SplitNode(node.right);
    }

    private void CreateRooms()
    {
        rooms.Clear();

        foreach (BSPNode leaf in leafNodes)
        {
            int maxWidth = leaf.width - roomMargin * 2;
            int maxHeight = leaf.height - roomMargin * 2;

            if (maxWidth < minRoomSize ||
                maxHeight < minRoomSize)
            {
                continue;
            }

            int roomWidth = Random.Range(
                minRoomSize,
                maxWidth + 1
            );

            int roomHeight = Random.Range(
                minRoomSize,
                maxHeight + 1
            );

            int maxXOffset =
                leaf.width - roomWidth - roomMargin;

            int maxYOffset =
                leaf.height - roomHeight - roomMargin;

            int roomX = leaf.x +
                        Random.Range(
                            roomMargin,
                            maxXOffset + 1
                        );

            int roomY = leaf.y +
                        Random.Range(
                            roomMargin,
                            maxYOffset + 1
                        );

            DungeonRoom room =
                new DungeonRoom(
                    roomX,
                    roomY,
                    roomWidth,
                    roomHeight
                );

            leaf.room = room;

            rooms.Add(room);

            CarveRoom(room);
        }
    }

    private void CarveRoom(DungeonRoom room)
    {
        for (int x = room.x;
            x < room.x + room.width;
            x++)
        {
            for (int y = room.y;
                y < room.y + room.height;
                y++)
            {
                if (IsInsideGrid(x, y))
                {
                    grid[x, y] = TileType.Floor;
                }
            }
        }
    }

    private bool IsInsideGrid(int x, int y)
    {
        return x >= 0 &&
            x < mapWidth &&
            y >= 0 &&
            y < mapHeight;
    }

    private void CreateCorridors()
    {
        if (rooms.Count < 2)
            return;

        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Vector2Int from = rooms[i].Center;
            Vector2Int to = rooms[i + 1].Center;

            CreateLCorridor(from, to);
        }
    }

    private void CreateLCorridor(
        Vector2Int from,
        Vector2Int to)
    {
        bool horizontalFirst =
            Random.value > 0.5f;

        if (horizontalFirst)
        {
            CarveHorizontal(
                from.x,
                to.x,
                from.y
            );

            CarveVertical(
                from.y,
                to.y,
                to.x
            );
        }
        else
        {
            CarveVertical(
                from.y,
                to.y,
                from.x
            );

            CarveHorizontal(
                from.x,
                to.x,
                to.y
            );
        }
    }

    private void CarveHorizontal(
        int x1,
        int x2,
        int y)
    {
        int start = Mathf.Min(x1, x2);
        int end = Mathf.Max(x1, x2);

        for (int x = start; x <= end; x++)
        {
            // corridorWidth: 1 = modul dasar, 2-3 = corridor lebar
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                int targetY = y + offset - (corridorWidth - 1) / 2;

                if (IsInsideInterior(x, targetY))
                {
                    grid[x, targetY] = TileType.Floor;
                }
            }
        }
    }

    private void CarveVertical(
        int y1,
        int y2,
        int x)
    {
        int start = Mathf.Min(y1, y2);
        int end = Mathf.Max(y1, y2);

        for (int y = start; y <= end; y++)
        {
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                int targetX = x + offset - (corridorWidth - 1) / 2;

                if (IsInsideInterior(targetX, y))
                {
                    grid[targetX, y] = TileType.Floor;
                }
            }
        }
    }

    private void PlaceStartAndGoal()
    {
        if (rooms.Count == 0)
            return;

        startPosition = rooms[0].Center;
        goalPosition = rooms[rooms.Count - 1].Center;

        grid[startPosition.x, startPosition.y]
            = TileType.Start;

        grid[goalPosition.x, goalPosition.y]
            = TileType.Goal;
    }

    private void PlaceStartAndFarthestGoal()
    {
        if (rooms.Count == 0)
            return;

        startPosition = rooms[0].Center;

        DungeonRoom farthestRoom =
            FindFarthestRoom(startPosition);

        goalPosition = farthestRoom.Center;

        grid[startPosition.x, startPosition.y]
            = TileType.Start;

        grid[goalPosition.x, goalPosition.y]
            = TileType.Goal;
    }

    private DungeonRoom FindFarthestRoom(
        Vector2Int position)
    {
        DungeonRoom farthest = rooms[0];
        float farthestDistance = 0f;

        foreach (DungeonRoom room in rooms)
        {
            float distance =
                Vector2Int.Distance(
                    position,
                    room.Center
                );

            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthest = room;
            }
        }

        return farthest;
    }

    private void CreateLevelRoot()
    {
        GameObject root =
            new GameObject("GeneratedLevel");

        levelRoot = root.transform;
    }

    private void BuildDungeon()
    {
        CreateLevelRoot();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3 worldPosition =
                    GridToWorld(x, y);

                switch (grid[x, y])
                {
                    case TileType.Floor:
                    case TileType.Start:
                    case TileType.Goal:

                        Instantiate(
                            floorPrefab,
                            worldPosition,
                            Quaternion.identity,
                            levelRoot
                        );

                        break;
                }

                if (grid[x, y] == TileType.Wall &&
                    IsWallVisible(x, y))
                {
                    Instantiate(
                        wallPrefab,
                        worldPosition +
                        Vector3.up * 0.5f,
                        Quaternion.identity,
                        levelRoot
                    );
                }

                if (grid[x, y] == TileType.Goal)
                {
                    Instantiate(
                        goalPrefab,
                        worldPosition +
                        Vector3.up * 0.15f,
                        Quaternion.identity,
                        levelRoot
                    );
                }
            }
        }
    }

    private Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            x * tileSize,
            0f,
            y * tileSize
        );
    }

    private bool IsWallVisible(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (!IsInsideGrid(nx, ny))
                    continue;

                if (grid[nx, ny] != TileType.Wall)
                    return true;
            }
        }

        return false;
    }

    private void SpawnGameplayObjects()
    {
        PrepareSpawnRules();
        SpawnPlayer();
        SpawnEnemies();
        SpawnItems();
        SpawnSpecialRooms();
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
            return;

        Vector3 position =
            GridToWorld(
                startPosition.x,
                startPosition.y
            );

        playerInstance = Instantiate(
            playerPrefab,
            position + Vector3.up * 1f,
            Quaternion.identity
        );
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null)
            return;

        // CATATAN: GetRandomFloorPosition() dari modul (bagian 78) diganti.
        // Versi modul mengembalikan startPosition setelah 100x gagal, dan
        // loop "i--" di sini lalu bisa berputar tanpa akhir (Unity hang).
        // Sekarang semua cell yang lolos constraint dikumpulkan dulu, lalu
        // dipilih acak; jika habis, spawn berhenti dengan peringatan.
        //
        // Constraint: Floor di dalam room, bukan safe room, di luar
        // safeRadius, bukan pintu room, tidak di jalur wajib Start->Goal,
        // dan belum terisi objek lain.
        List<Vector2Int> candidates =
            CollectSpawnCandidates(true, null);

        int targetCount = GetTargetEnemyCount();

        for (int i = 0; i < targetCount; i++)
        {
            Vector2Int gridPosition;

            if (!TakeRandomTile(candidates, out gridPosition))
            {
                Debug.LogWarning(
                    "Tempat valid habis: hanya " + i + " dari " +
                    targetCount + " enemy yang dapat ditempatkan."
                );
                break;
            }

            enemySpawnTiles.Add(gridPosition);

            Vector3 worldPosition =
                GridToWorld(
                    gridPosition.x,
                    gridPosition.y
                );

            GameObject enemy =
                Instantiate(
                    enemyPrefab,
                    worldPosition +
                    Vector3.up * 1f,
                    Quaternion.identity,
                    levelRoot
                );

            enemy.name = "Enemy_" + i;
        }
    }

    private void SpawnItems()
    {
        if (itemPrefab == null)
            return;

        List<Vector2Int> candidates =
            CollectSpawnCandidates(false, null);

        for (int i = 0; i < itemCount; i++)
        {
            Vector2Int gridPosition;

            if (!TakeRandomTile(candidates, out gridPosition))
            {
                Debug.LogWarning(
                    "Tempat valid habis: hanya " + i + " dari " +
                    itemCount + " item yang dapat ditempatkan."
                );
                break;
            }

            itemSpawnTiles.Add(gridPosition);

            Vector3 worldPosition =
                GridToWorld(
                    gridPosition.x,
                    gridPosition.y
                );

            GameObject item =
                Instantiate(
                    itemPrefab,
                    worldPosition +
                    Vector3.up * 0.5f,
                    Quaternion.identity,
                    levelRoot
                );

            item.name = "Item_" + i;

            // Item tidak boleh menghalangi gerak player.
            MakeTrigger(item);
            itemObjects.Add(item);
        }
    }

    private void ClearDungeon()
    {
        GameObject existing =
            GameObject.Find("GeneratedLevel");

        if (existing != null)
        {
            // Destroy() baru dieksekusi di akhir frame, jadi ganti nama
            // agar tidak tertukar dengan level baru di frame yang sama.
            existing.name = "GeneratedLevel_Old";
            Destroy(existing);
        }

        levelRoot = null;

        GameObject oldPlayer =
            GameObject.FindWithTag("Player");

        if (oldPlayer != null)
        {
            oldPlayer.tag = "Untagged";
            Destroy(oldPlayer);
        }

        playerInstance = null;
    }

    private void OnDrawGizmos()
    {
        if (rooms == null)
            return;

        Gizmos.color = Color.yellow;

        foreach (DungeonRoom room in rooms)
        {
            Vector3 center =
                new Vector3(
                    (room.x + room.width / 2f) * tileSize,
                    0.2f,
                    (room.y + room.height / 2f) * tileSize
                );

            Vector3 size =
                new Vector3(
                    room.width * tileSize,
                    0.2f,
                    room.height * tileSize
                );

            Gizmos.DrawWireCube(
                center,
                size
            );
        }

        DrawExtraGizmos();
    }

    private bool ValidateDungeon()
    {
        if (rooms == null || rooms.Count < 2)
        {
            validationMessage = "jumlah room terlalu sedikit";
            return false;
        }

        if (!IsInsideGrid(
                startPosition.x,
                startPosition.y))
        {
            validationMessage = "Start di luar grid";
            return false;
        }

        if (!IsInsideGrid(
                goalPosition.x,
                goalPosition.y))
        {
            validationMessage = "Goal di luar grid";
            return false;
        }

        // Hard constraint: Start & Goal berada di floor dan berbeda.
        if (grid[startPosition.x, startPosition.y] != TileType.Start ||
            grid[goalPosition.x, goalPosition.y] != TileType.Goal ||
            startPosition == goalPosition)
        {
            validationMessage = "Start/Goal tidak valid";
            return false;
        }

        return true;
    }

    private bool HasPathToGoal()
    {
        bool[,] visited =
            new bool[mapWidth, mapHeight];

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        queue.Enqueue(startPosition);

        visited[
            startPosition.x,
            startPosition.y
        ] = true;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        while (queue.Count > 0)
        {
            Vector2Int current =
                queue.Dequeue();

            if (current == goalPosition)
            {
                return true;
            }

            foreach (Vector2Int direction
                    in directions)
            {
                Vector2Int next =
                    current + direction;

                if (!IsInsideGrid(
                        next.x,
                        next.y))
                {
                    continue;
                }

                if (visited[
                        next.x,
                        next.y])
                {
                    continue;
                }

                if (grid[
                        next.x,
                        next.y]
                    == TileType.Wall)
                {
                    continue;
                }

                visited[
                    next.x,
                    next.y
                ] = true;

                queue.Enqueue(next);
            }
        }

        validationMessage = "Goal tidak dapat dicapai (BFS)";
        return false;
    }
}

