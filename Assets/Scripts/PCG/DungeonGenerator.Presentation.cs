using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Presentasi: metrics, bantuan visual di Game View (LineRenderer),
// Gizmos tambahan di Scene View, dan API untuk DungeonControlPanel.
public partial class DungeonGenerator
{
    [Header("Debug Visual (Game View)")]
    [SerializeField] private bool showRoomBounds = true;
    [SerializeField] private bool showMainPath = true;
    [SerializeField] private bool showSpawnInfo = true;

    [Header("Presentation")]
    [Tooltip("Tambahkan panel kontrol otomatis saat Play.")]
    [SerializeField] private bool autoAddControlPanel = true;

    private readonly DungeonMetrics metrics = new DungeonMetrics();

    private GameObject roomBoundsGroup;
    private GameObject mainPathGroup;
    private GameObject spawnInfoGroup;

    private static Material lineMaterial;

    private const float OverlayHeight = 0.12f;

    private static readonly Color RoomColor = new Color(0.3f, 0.85f, 1f);
    private static readonly Color SafeColor = new Color(0.3f, 1f, 0.45f);
    private static readonly Color GoalColor = new Color(1f, 0.8f, 0.15f);
    private static readonly Color TreasureColor = new Color(0.8f, 0.4f, 1f);
    private static readonly Color PathColor = new Color(1f, 0.95f, 0.2f);
    private static readonly Color EnemyColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color ItemColor = new Color(0.3f, 1f, 0.8f);

    private void Awake()
    {
        if (autoAddControlPanel &&
            GetComponent<DungeonControlPanel>() == null)
        {
            gameObject.AddComponent<DungeonControlPanel>();
        }
    }

    // ===================== METRICS =====================

    private void ComputeMetrics(System.Diagnostics.Stopwatch stopwatch)
    {
        metrics.seed = seed;
        metrics.requestedSeed = requestedSeed;
        metrics.attempts = attemptsUsed;
        metrics.valid = isValid;
        metrics.validationMessage = validationMessage;
        metrics.mapWidth = mapWidth;
        metrics.mapHeight = mapHeight;
        metrics.rooms = rooms.Count;
        metrics.reachableRooms = isValid ? reachableRoomCount : 0;
        metrics.corridors = Mathf.Max(0, rooms.Count - 1);

        int floor = 0;
        int roomFloor = 0;
        int walls = 0;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (grid[x, y] == TileType.Wall)
                {
                    if (IsWallVisible(x, y))
                        walls++;

                    continue;
                }

                floor++;

                if (FindRoomAt(new Vector2Int(x, y)) != null)
                    roomFloor++;
            }
        }

        metrics.floorTiles = floor;
        metrics.roomFloorTiles = roomFloor;
        metrics.corridorTiles = floor - roomFloor;
        metrics.floorPercent = 100f * floor / (mapWidth * mapHeight);
        metrics.wallObjects = walls;

        metrics.startGoalPathLength = isValid ? mainPath.Count - 1 : 0;
        metrics.startGoalEuclidean = Vector2Int.Distance(startPosition, goalPosition);

        metrics.enemies = isValid ? enemySpawnTiles.Count : 0;
        metrics.items = isValid ? itemSpawnTiles.Count : 0;
        metrics.boss = isValid && hasBoss;
        metrics.treasure = isValid && hasTreasure;

        stopwatch.Stop();
        metrics.generationMs = (float)stopwatch.Elapsed.TotalMilliseconds;
    }

    // ===================== OVERLAY (GAME VIEW) =====================

    private void BuildDebugOverlay()
    {
        if (levelRoot == null)
            return;

        Transform overlayRoot = new GameObject("DebugOverlay").transform;
        overlayRoot.SetParent(levelRoot, false);

        roomBoundsGroup = CreateGroup(overlayRoot, "RoomBounds");
        mainPathGroup = CreateGroup(overlayRoot, "MainPath");
        spawnInfoGroup = CreateGroup(overlayRoot, "SpawnInfo");

        // Batas room: hijau = safe room, emas = goal room,
        // ungu = treasure room, biru = room biasa.
        for (int i = 0; i < rooms.Count; i++)
        {
            DungeonRoom room = rooms[i];

            Color color = RoomColor;

            if (room == goalRoom) color = GoalColor;
            else if (room == treasureRoom) color = TreasureColor;
            else if (safeRooms.Contains(room)) color = SafeColor;

            DrawRect(
                roomBoundsGroup.transform,
                "Room_" + i,
                room.x, room.y, room.width, room.height,
                color, 0.08f
            );
        }

        // Jalur wajib hasil BFS (hanya titik belok agar ringan)
        List<Vector3> pathPoints = new List<Vector3>();

        for (int i = 0; i < mainPath.Count; i++)
        {
            bool isCorner =
                i == 0 || i == mainPath.Count - 1 ||
                (mainPath[i] - mainPath[i - 1]) != (mainPath[i + 1] - mainPath[i]);

            if (isCorner)
            {
                pathPoints.Add(OverlayPoint(mainPath[i], 0.03f));
            }
        }

        CreateLine(mainPathGroup.transform, "BFS_Path", pathPoints, PathColor, 0.18f, false);

        // Zona aman di sekitar Start
        List<Vector3> circle = new List<Vector3>();
        Vector3 startWorld = OverlayPoint(startPosition, 0f);

        for (int i = 0; i < 48; i++)
        {
            float angle = i / 48f * Mathf.PI * 2f;
            circle.Add(startWorld + new Vector3(
                Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * safeRadius * tileSize);
        }

        CreateLine(spawnInfoGroup.transform, "SafeRadius", circle, SafeColor, 0.08f, true);

        DrawTileMarker(spawnInfoGroup.transform, "Start", startPosition, SafeColor);
        DrawTileMarker(spawnInfoGroup.transform, "Goal", goalPosition, GoalColor);

        foreach (Vector2Int tile in enemySpawnTiles)
            DrawTileMarker(spawnInfoGroup.transform, "EnemySpawn", tile, EnemyColor);

        foreach (Vector2Int tile in itemSpawnTiles)
            DrawTileMarker(spawnInfoGroup.transform, "ItemSpawn", tile, ItemColor);

        if (hasBoss)
            DrawTileMarker(spawnInfoGroup.transform, "BossSpawn", bossTile, EnemyColor, 1.6f);

        if (hasTreasure)
            DrawTileMarker(spawnInfoGroup.transform, "TreasureSpawn", treasureTile, TreasureColor);

        ApplyOverlayVisibility();
    }

    private void ApplyOverlayVisibility()
    {
        if (roomBoundsGroup != null) roomBoundsGroup.SetActive(showRoomBounds);
        if (mainPathGroup != null) mainPathGroup.SetActive(showMainPath);
        if (spawnInfoGroup != null) spawnInfoGroup.SetActive(showSpawnInfo);
    }

    private static GameObject CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group;
    }

    private Vector3 OverlayPoint(Vector2Int tile, float extraHeight)
    {
        return GridToWorld(tile.x, tile.y) +
               Vector3.up * (OverlayHeight + extraHeight);
    }

    private void DrawRect(
        Transform parent, string name,
        float x, float y, float width, float height,
        Color color, float lineWidth)
    {
        float half = 0.5f;

        List<Vector3> corners = new List<Vector3>
        {
            new Vector3((x - half) * tileSize, OverlayHeight, (y - half) * tileSize),
            new Vector3((x + width - half) * tileSize, OverlayHeight, (y - half) * tileSize),
            new Vector3((x + width - half) * tileSize, OverlayHeight, (y + height - half) * tileSize),
            new Vector3((x - half) * tileSize, OverlayHeight, (y + height - half) * tileSize)
        };

        CreateLine(parent, name, corners, color, lineWidth, true);
    }

    private void DrawTileMarker(
        Transform parent, string name, Vector2Int tile,
        Color color, float size = 0.8f)
    {
        float offset = (size - 1f) / 2f;

        DrawRect(
            parent, name,
            tile.x - offset, tile.y - offset, size, size,
            color, 0.06f
        );
    }

    private static void CreateLine(
        Transform parent, string name, List<Vector3> points,
        Color color, float width, bool loop)
    {
        if (points.Count < 2)
            return;

        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetLineMaterial(color);
        line.startColor = color;
        line.endColor = color;
    }

    private static Material GetLineMaterial(Color color)
    {
        // Sprites/Default memakai vertex color -> satu material untuk semua.
        Shader sprite = Shader.Find("Sprites/Default");

        if (sprite != null)
        {
            if (lineMaterial == null)
                lineMaterial = new Material(sprite);

            return lineMaterial;
        }

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(unlit);
        material.color = color;
        return material;
    }

    // ===================== GIZMOS (SCENE VIEW) =====================

    private void DrawExtraGizmos()
    {
        if (!isValid || grid == null)
            return;

        Gizmos.color = PathColor;

        for (int i = 1; i < mainPath.Count; i++)
        {
            Gizmos.DrawLine(
                OverlayPoint(mainPath[i - 1], 0.1f),
                OverlayPoint(mainPath[i], 0.1f));
        }

        Gizmos.color = SafeColor;
        Gizmos.DrawWireSphere(OverlayPoint(startPosition, 0f), safeRadius * tileSize);

        Gizmos.color = EnemyColor;
        foreach (Vector2Int tile in enemySpawnTiles)
            Gizmos.DrawWireCube(OverlayPoint(tile, 0.3f), Vector3.one * 0.6f * tileSize);

        Gizmos.color = ItemColor;
        foreach (Vector2Int tile in itemSpawnTiles)
            Gizmos.DrawWireSphere(OverlayPoint(tile, 0.3f), 0.3f * tileSize);
    }

    // ===================== API UNTUK CONTROL PANEL =====================

    public DungeonMetrics Metrics => metrics;
    public bool IsValid => isValid;
    public string ValidationMessage => validationMessage;
    public int AttemptsUsed => attemptsUsed;
    public int RoomCount => rooms.Count;
    public int ItemsCollected => isValid ? itemsCollected : 0;
    public int ItemsTotal => isValid ? itemSpawnTiles.Count : 0;
    public bool HasTreasure => hasTreasure;
    public bool TreasureCollected => treasureCollected;
    public bool GoalReached => goalReached;
    public Transform PlayerTransform => playerInstance != null ? playerInstance.transform : null;
    public float TileSize => tileSize;

    public int Seed { get => seed; set => seed = value; }
    public bool RandomSeedEachGenerate { get => randomSeed; set => randomSeed = value; }

    public int MapWidth { get => mapWidth; set => mapWidth = value; }
    public int MapHeight { get => mapHeight; set => mapHeight = value; }
    public int MinLeafSize { get => minLeafSize; set => minLeafSize = value; }
    public int MaxLeafSize { get => maxLeafSize; set => maxLeafSize = value; }
    public int MinRoomSize { get => minRoomSize; set => minRoomSize = value; }
    public int RoomMargin { get => roomMargin; set => roomMargin = value; }
    public int CorridorWidth { get => corridorWidth; set => corridorWidth = value; }
    public GoalPlacementStrategy GoalStrategy { get => goalStrategy; set => goalStrategy = value; }

    public int EnemyCount { get => enemyCount; set => enemyCount = value; }
    public int ItemCount { get => itemCount; set => itemCount = value; }
    public int SafeRoomCount { get => safeRoomCount; set => safeRoomCount = value; }
    public float SafeRadius { get => safeRadius; set => safeRadius = value; }
    public bool SpawnInRoomsOnly { get => spawnInRoomsOnly; set => spawnInRoomsOnly = value; }
    public bool KeepMainPathClear { get => keepMainPathClear; set => keepMainPathClear = value; }
    public bool UseEnemyDensity { get => useEnemyDensity; set => useEnemyDensity = value; }
    public int FloorTilesPerEnemy { get => floorTilesPerEnemy; set => floorTilesPerEnemy = value; }
    public bool SpawnBoss { get => spawnBoss; set => spawnBoss = value; }
    public bool SpawnTreasure { get => spawnTreasure; set => spawnTreasure = value; }

    public int MaxGenerationAttempts { get => maxGenerationAttempts; set => maxGenerationAttempts = value; }
    public bool RequireAllRoomsReachable { get => requireAllRoomsReachable; set => requireAllRoomsReachable = value; }

    public bool ShowRoomBounds
    {
        get => showRoomBounds;
        set { showRoomBounds = value; ApplyOverlayVisibility(); }
    }

    public bool ShowMainPath
    {
        get => showMainPath;
        set { showMainPath = value; ApplyOverlayVisibility(); }
    }

    public bool ShowSpawnInfo
    {
        get => showSpawnInfo;
        set { showSpawnInfo = value; ApplyOverlayVisibility(); }
    }
}
