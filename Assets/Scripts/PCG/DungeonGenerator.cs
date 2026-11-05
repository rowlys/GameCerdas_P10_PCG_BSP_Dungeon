using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DungeonGenerator : MonoBehaviour
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
    [SerializeField] private bool farthestGoal = false;

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
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            seed++;
            GenerateDungeon();
        }
    }

    public void GenerateDungeon()
    {
        ClearDungeon();

        SetupSeed();

        InitializeGrid();

        CreateBSP();

        CreateRooms();

        CreateCorridors();

        if (farthestGoal)
        {
            PlaceStartAndFarthestGoal();
        }
        else
        {
            PlaceStartAndGoal();
        }

        if (!HasPathToGoal())
        {
            Debug.LogWarning(
                "Dungeon tidak valid: Goal tidak dapat dicapai."
            );

            return;
        }

        BuildDungeon();

        SpawnGameplayObjects();

        Debug.Log("Dungeon generated with seed: " + seed);
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
            if (IsInsideGrid(x, y))
            {
                grid[x, y] = TileType.Floor;
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
            if (IsInsideGrid(x, y))
            {
                grid[x, y] = TileType.Floor;
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
        SpawnPlayer();
        SpawnEnemies();
        SpawnItems();
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

        Instantiate(
            playerPrefab,
            position + Vector3.up * 1f,
            Quaternion.identity
        );
    }

    private Vector2Int GetRandomFloorPosition()
    {
        for (int attempt = 0;
            attempt < 100;
            attempt++)
        {
            int x = Random.Range(1, mapWidth - 1);
            int y = Random.Range(1, mapHeight - 1);

            if (grid[x, y] == TileType.Floor)
            {
                return new Vector2Int(x, y);
            }
        }

        return startPosition;
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null)
            return;

        for (int i = 0; i < enemyCount; i++)
        {
            Vector2Int gridPosition =
                GetRandomFloorPosition();

            float distanceFromStart =
                Vector2Int.Distance(
                    gridPosition,
                    startPosition
                );

            if (distanceFromStart < 8f)
            {
                i--;
                continue;
            }

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

        for (int i = 0; i < itemCount; i++)
        {
            Vector2Int gridPosition =
                GetRandomFloorPosition();

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
        }
    }

    private void ClearDungeon()
    {
        GameObject existing =
            GameObject.Find("GeneratedLevel");

        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject oldPlayer =
            GameObject.FindWithTag("Player");

        if (oldPlayer != null)
        {
            Destroy(oldPlayer);
        }
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
    }

    private bool ValidateDungeon()
    {
        if (rooms == null || rooms.Count < 2)
        {
            Debug.LogWarning(
                "Dungeon invalid: jumlah room terlalu sedikit."
            );

            return false;
        }

        if (!IsInsideGrid(
                startPosition.x,
                startPosition.y))
        {
            return false;
        }

        if (!IsInsideGrid(
                goalPosition.x,
                goalPosition.y))
        {
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

        return false;
    }


}

