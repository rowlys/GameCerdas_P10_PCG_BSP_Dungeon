using UnityEngine;
using UnityEngine.InputSystem;

// Panel kontrol untuk presentasi (IMGUI, tanpa perlu setup Canvas).
// Ditambahkan otomatis oleh DungeonGenerator saat Play.
//
// Hotkey:
//   Tab  : tampil/sembunyikan panel     R : seed berikutnya (seed+1)
//   N    : seed acak                    G : generate ulang seed yang sama
//   P    : jalur BFS                    B : batas room
//   V    : info spawn & zona aman       C : kamera overview / follow
//   M    : minimap (mode follow)
[DisallowMultipleComponent]
public class DungeonControlPanel : MonoBehaviour
{
    public static bool IsTyping { get; private set; }

    private enum CameraMode { Overview, Follow }

    [SerializeField] private float panelWidth = 350f;
    [SerializeField] private float referenceHeight = 900f;
    [SerializeField] private float followHeight = 13f;
    [SerializeField] private float followDistance = 7f;

    private const string SeedFieldName = "SeedField";

    private DungeonGenerator generator;
    private Camera mainCamera;
    private Camera minimapCamera;

    private CameraMode cameraMode = CameraMode.Overview;
    private bool panelVisible = true;
    private bool showMinimap = true;

    private bool foldConfig = true;
    private bool foldSpawn = false;
    private bool foldVisual = true;
    private bool foldMetrics = false;

    private string seedInput = "";
    private Vector2 scroll;
    private float guiScale = 1f;

    private Settings pending;
    private Settings defaults;
    private readonly System.Random seedRandom = new System.Random();

    private GUIStyle titleStyle, headerStyle, labelStyle, valueStyle,
        smallStyle, bannerStyle, bannerSubStyle, boxStyle, okStyle, badStyle;
    private Texture2D panelTexture, bannerTexture;

    // ---------------------------------------------------------------

    private void Start()
    {
        generator = GetComponent<DungeonGenerator>();
        mainCamera = Camera.main;

        defaults = Settings.Capture(generator);
        pending = Settings.Capture(generator);

        CreateMinimapCamera();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || generator == null)
            return;

        // Saat mengetik seed, hotkey dimatikan (Enter ditangani di OnGUI).
        if (IsTyping)
            return;

        if (keyboard.tabKey.wasPressedThisFrame) panelVisible = !panelVisible;
        if (keyboard.nKey.wasPressedThisFrame) GenerateWithSeed(seedRandom.Next(1, 1000000));
        if (keyboard.gKey.wasPressedThisFrame) GenerateWithSeed(generator.Seed);
        if (keyboard.pKey.wasPressedThisFrame) generator.ShowMainPath = !generator.ShowMainPath;
        if (keyboard.bKey.wasPressedThisFrame) generator.ShowRoomBounds = !generator.ShowRoomBounds;
        if (keyboard.vKey.wasPressedThisFrame) generator.ShowSpawnInfo = !generator.ShowSpawnInfo;
        if (keyboard.cKey.wasPressedThisFrame) ToggleCameraMode();
        if (keyboard.mKey.wasPressedThisFrame) showMinimap = !showMinimap;
    }

    private void LateUpdate()
    {
        if (generator == null)
            return;

        UpdateMainCamera();
        UpdateMinimapCamera();
    }

    // ---------------------------------------------------------------
    // Generate helpers

    private void GenerateWithSeed(int seed)
    {
        generator.Seed = seed;
        generator.RandomSeedEachGenerate = false;
        generator.GenerateDungeon();
        seedInput = generator.Seed.ToString();
    }

    private void GenerateFromSeedInput()
    {
        GUI.FocusControl(null);
        IsTyping = false;

        if (int.TryParse(seedInput.Trim(), out int parsed))
        {
            GenerateWithSeed(parsed);
        }
        else
        {
            seedInput = generator.Seed.ToString();
        }
    }

    private void ApplyPendingAndGenerate()
    {
        pending.Apply(generator);
        generator.GenerateDungeon();

        // Generator bisa mengoreksi parameter yang saling bertabrakan.
        pending = Settings.Capture(generator);
        seedInput = generator.Seed.ToString();
    }

    // ---------------------------------------------------------------
    // Camera

    private void ToggleCameraMode()
    {
        cameraMode = cameraMode == CameraMode.Overview
            ? CameraMode.Follow
            : CameraMode.Overview;
    }

    private float PanelScreenWidth => panelVisible ? (panelWidth + 16f) * guiScale : 0f;

    private void UpdateMainCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Area kamera digeser agar dungeon tidak tertutup panel.
        float left = Mathf.Clamp01(PanelScreenWidth / Mathf.Max(1, Screen.width));
        mainCamera.rect = new Rect(left, 0f, 1f - left, 1f);

        Transform player = generator.PlayerTransform;

        if (cameraMode == CameraMode.Follow && player != null)
        {
            Vector3 target = player.position +
                             new Vector3(0f, followHeight, -followDistance);

            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                target,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

            mainCamera.transform.LookAt(player.position);
            return;
        }

        // Overview: kamera top-down yang menyesuaikan ukuran peta.
        float tile = generator.TileSize;
        float width = generator.MapWidth * tile;
        float height = generator.MapHeight * tile;

        Vector3 center = new Vector3((width - tile) / 2f, 0f, (height - tile) / 2f);

        mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Max(
                height / 2f, width / 2f / mainCamera.aspect) + 1.5f;

            mainCamera.transform.position = center + Vector3.up * 50f;
        }
        else
        {
            float tanHalf = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float distance = Mathf.Max(
                (height / 2f + 1.5f) / tanHalf,
                (width / 2f + 1.5f) / (tanHalf * mainCamera.aspect));

            mainCamera.transform.position = center + Vector3.up * distance;
        }
    }

    private void CreateMinimapCamera()
    {
        // Kamera latar: membersihkan area layar di belakang panel
        // (main camera hanya merender area di sebelah kanan panel).
        GameObject backgroundObject = new GameObject("BackgroundCamera");
        Camera background = backgroundObject.AddComponent<Camera>();
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = new Color(0.03f, 0.04f, 0.07f);
        background.cullingMask = 0;
        background.depth = (mainCamera != null ? mainCamera.depth : 0f) - 1f;

        GameObject minimapObject = new GameObject("MinimapCamera");
        minimapCamera = minimapObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
        minimapCamera.depth = (mainCamera != null ? mainCamera.depth : 0f) + 1f;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.enabled = false;
    }

    private void UpdateMinimapCamera()
    {
        if (minimapCamera == null)
            return;

        bool active = showMinimap && cameraMode == CameraMode.Follow;
        minimapCamera.enabled = active;

        if (!active)
            return;

        float tile = generator.TileSize;
        float width = generator.MapWidth * tile;
        float height = generator.MapHeight * tile;

        float rectWidth = 0.24f;
        float rectHeight = rectWidth * Screen.width / Mathf.Max(1, Screen.height) *
                           (height / width);
        rectHeight = Mathf.Min(rectHeight, 0.4f);

        minimapCamera.rect = new Rect(1f - rectWidth - 0.01f, 1f - rectHeight - 0.02f,
                                      rectWidth, rectHeight);

        minimapCamera.orthographicSize =
            Mathf.Max(height / 2f, width / 2f / minimapCamera.aspect) + 0.5f;

        minimapCamera.transform.position =
            new Vector3((width - tile) / 2f, 60f, (height - tile) / 2f);
    }

    // ---------------------------------------------------------------
    // GUI

    private void OnGUI()
    {
        if (generator == null)
            return;

        InitStyles();

        guiScale = Mathf.Clamp(Screen.height / referenceHeight, 0.75f, 2.5f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        float screenWidth = Screen.width / guiScale;
        float screenHeight = Screen.height / guiScale;

        if (!IsTyping && GUI.GetNameOfFocusedControl() != SeedFieldName)
        {
            seedInput = generator.Seed.ToString();
        }

        if (panelVisible)
        {
            DrawPanel(screenHeight);
        }
        else
        {
            if (GUI.Button(new Rect(8, 8, 150, 28), "Panel  [Tab]"))
                panelVisible = true;

            IsTyping = false;
        }

        DrawBanner(screenWidth, screenHeight);
    }

    private void DrawPanel(float screenHeight)
    {
        GUILayout.BeginArea(new Rect(8, 8, panelWidth, screenHeight - 16), boxStyle);
        scroll = GUILayout.BeginScrollView(scroll);

        DrawHeader();
        DrawSeedControls();
        DrawGeneratorConfig();
        DrawSpawnConfig();
        DrawVisualConfig();
        DrawMetrics();
        DrawHelp();

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        IsTyping = GUI.GetNameOfFocusedControl() == SeedFieldName;
    }

    private void DrawHeader()
    {
        DungeonMetrics m = generator.Metrics;

        GUILayout.Label("BSP DUNGEON GENERATOR", titleStyle);
        GUILayout.Label("Praktikum 10  /  PCG  /  Unity 6", smallStyle);
        GUILayout.Space(6);

        GUILayout.Label("Seed " + generator.Seed + "   |   " +
                        m.mapWidth + " x " + m.mapHeight, labelStyle);

        GUILayout.Label(generator.RoomCount + " room   |   " +
                        m.corridors + " corridor   |   lebar " +
                        generator.CorridorWidth, labelStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Jalur " + m.startGoalPathLength + " sel   |   BFS: ", labelStyle,
                        GUILayout.ExpandWidth(false));
        GUILayout.Label(generator.IsValid ? "VALID" : "INVALID",
                        generator.IsValid ? okStyle : badStyle);
        GUILayout.EndHorizontal();

        if (generator.AttemptsUsed > 1 || !generator.IsValid)
        {
            GUILayout.Label("Percobaan: " + generator.AttemptsUsed +
                            (m.requestedSeed != m.seed ? "  (seed awal " + m.requestedSeed + ")" : "") +
                            (generator.IsValid ? "" : "\n" + generator.ValidationMessage),
                            smallStyle);
        }

        GUILayout.Space(4);

        string treasure = generator.HasTreasure
            ? (generator.TreasureCollected ? "   |   Treasure: OK" : "   |   Treasure: -")
            : "";

        GUILayout.Label("Item " + generator.ItemsCollected + "/" + generator.ItemsTotal +
                        treasure + "   |   Goal: " +
                        (generator.GoalReached ? "TERCAPAI" : "belum"), labelStyle);

        GUILayout.Space(8);
    }

    private void DrawSeedControls()
    {
        Event current = Event.current;

        if (current.type == EventType.KeyDown &&
            (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() == SeedFieldName)
        {
            current.Use();
            GenerateFromSeedInput();
        }

        GUILayout.BeginHorizontal();

        GUI.SetNextControlName(SeedFieldName);
        seedInput = GUILayout.TextField(seedInput, 10, GUILayout.Width(110));

        if (GUILayout.Button("Generate"))
            GenerateFromSeedInput();

        if (GUILayout.Button("Seed Acak [N]"))
            GenerateWithSeed(seedRandom.Next(1, 1000000));

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Seed +1 [R]"))
            GenerateWithSeed(generator.Seed + 1);

        if (GUILayout.Button("Ulang seed sama [G]"))
            GenerateWithSeed(generator.Seed);

        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }

    private void DrawGeneratorConfig()
    {
        foldConfig = Foldout(foldConfig, "KONFIGURASI GENERATOR");
        if (!foldConfig) return;

        GUILayout.Label("01 / UKURAN PETA", headerStyle);
        pending.mapWidth = IntSlider("Lebar (sel)", pending.mapWidth, 30, 120);
        pending.mapHeight = IntSlider("Tinggi (sel)", pending.mapHeight, 20, 90);

        GUILayout.Label("02 / BSP, ROOM & CORRIDOR", headerStyle);
        pending.minLeafSize = IntSlider("Ukuran leaf minimum", pending.minLeafSize, 6, 30);
        pending.maxLeafSize = IntSlider("Ukuran leaf maksimum", pending.maxLeafSize, 8, 40);
        pending.minRoomSize = IntSlider("Ukuran room minimum", pending.minRoomSize, 3, 15);
        pending.roomMargin = IntSlider("Margin room", pending.roomMargin, 1, 6);
        pending.corridorWidth = IntSlider("Lebar corridor", pending.corridorWidth, 1, 3);

        GUILayout.Label("03 / START - GOAL & VALIDASI", headerStyle);
        GUILayout.Label("Strategi Goal", labelStyle);
        pending.goalStrategy = (GoalPlacementStrategy)GUILayout.Toolbar(
            (int)pending.goalStrategy,
            new[] { "A: Room akhir", "B: Euclid", "C: BFS" });

        pending.maxAttempts = IntSlider("Maks. percobaan generate", pending.maxAttempts, 1, 30);
        pending.requireAllRooms = GUILayout.Toggle(pending.requireAllRooms,
            " Semua room wajib terjangkau");

        GUILayout.Label(
            "Leaf yang lebih kecil dari room minimum + 2x margin tidak mendapat room. " +
            "Jika room < 2 atau ada yang terputus, dungeon ditolak dan seed berikutnya dicoba.",
            smallStyle);

        if (GUILayout.Button("Preset uji validasi (sering gagal)"))
        {
            // Hasil simulasi: +-35% seed menghasilkan < 2 room,
            // sehingga Generate-and-Test terlihat bekerja.
            pending.mapWidth = 34;
            pending.mapHeight = 24;
            pending.minLeafSize = 9;
            pending.maxLeafSize = 24;
            pending.minRoomSize = 6;
            pending.roomMargin = 3;
        }

        DrawApplyButtons();
    }

    private void DrawSpawnConfig()
    {
        foldSpawn = Foldout(foldSpawn, "SPAWN & CONSTRAINT");
        if (!foldSpawn) return;

        pending.useEnemyDensity = GUILayout.Toggle(pending.useEnemyDensity,
            " Enemy berdasarkan density floor");

        if (pending.useEnemyDensity)
            pending.floorTilesPerEnemy = IntSlider("1 enemy per N floor", pending.floorTilesPerEnemy, 15, 150);
        else
            pending.enemyCount = IntSlider("Jumlah enemy", pending.enemyCount, 0, 40);

        pending.itemCount = IntSlider("Jumlah item", pending.itemCount, 0, 30);
        pending.safeRoomCount = IntSlider("Safe room (tanpa enemy)", pending.safeRoomCount, 0, 4);
        pending.safeRadius = IntSlider("Radius aman Start (sel)", Mathf.RoundToInt(pending.safeRadius), 0, 20);

        pending.spawnInRoomsOnly = GUILayout.Toggle(pending.spawnInRoomsOnly,
            " Spawn hanya di dalam room");
        pending.keepMainPathClear = GUILayout.Toggle(pending.keepMainPathClear,
            " Jalur wajib Start-Goal bebas objek");
        pending.spawnBoss = GUILayout.Toggle(pending.spawnBoss, " Boss di Goal Room");
        pending.spawnTreasure = GUILayout.Toggle(pending.spawnTreasure, " Treasure di room samping");

        DrawApplyButtons();
    }

    private void DrawApplyButtons()
    {
        Settings applied = Settings.Capture(generator);
        applied.seed = pending.seed;
        bool dirty = !pending.Equals(applied);

        if (dirty)
            GUILayout.Label("* Ada perubahan yang belum diterapkan", badStyle);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Default", GUILayout.Width(80)))
        {
            Settings restored = defaults;
            restored.seed = generator.Seed;
            pending = restored;
        }

        if (GUILayout.Button("Terapkan & Generate"))
            ApplyPendingAndGenerate();

        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }

    private void DrawVisualConfig()
    {
        foldVisual = Foldout(foldVisual, "BANTUAN VISUAL & KAMERA");
        if (!foldVisual) return;

        generator.ShowMainPath = GUILayout.Toggle(generator.ShowMainPath,
            " [P] Jalur BFS Start-Goal (kuning)");
        generator.ShowRoomBounds = GUILayout.Toggle(generator.ShowRoomBounds,
            " [B] Batas room BSP");
        generator.ShowSpawnInfo = GUILayout.Toggle(generator.ShowSpawnInfo,
            " [V] Titik spawn & zona aman");

        GUILayout.Space(4);
        GUILayout.Label("[C] Kamera", labelStyle);
        cameraMode = (CameraMode)GUILayout.Toolbar((int)cameraMode,
            new[] { "Overview", "Follow Player" });

        if (cameraMode == CameraMode.Follow)
            showMinimap = GUILayout.Toggle(showMinimap, " [M] Minimap");

        GUILayout.Space(4);
        GUILayout.Label(
            "Warna room: hijau = safe, emas = goal,\n" +
            "ungu = treasure, biru = biasa.\n" +
            "Kotak merah = spawn enemy, tosca = item.",
            smallStyle);

        GUILayout.Space(8);
    }

    private void DrawMetrics()
    {
        foldMetrics = Foldout(foldMetrics, "DUNGEON METRICS");
        if (!foldMetrics) return;

        GUILayout.Label(generator.Metrics.ToLogString(), smallStyle);

        if (GUILayout.Button("Cetak metrics ke Console"))
            Debug.Log("=== Dungeon Metrics ===\n" + generator.Metrics.ToLogString());

        GUILayout.Space(8);
    }

    private void DrawHelp()
    {
        GUILayout.Label("KONTROL", headerStyle);
        GUILayout.Label(
            "WASD / panah : gerak player\n" +
            "R : seed +1     N : seed acak     G : ulang\n" +
            "P : jalur BFS   B : batas room    V : spawn\n" +
            "C : kamera      M : minimap       Tab : panel",
            smallStyle);
    }

    private void DrawBanner(float screenWidth, float screenHeight)
    {
        string title = null;
        string subtitle = null;
        GUIStyle style = bannerStyle;

        if (!generator.IsValid)
        {
            title = "DUNGEON TIDAK VALID";
            subtitle = generator.ValidationMessage + " - ubah parameter lalu Generate";
        }
        else if (generator.GoalReached)
        {
            title = "GOAL TERCAPAI!";
            subtitle = "Item " + generator.ItemsCollected + "/" + generator.ItemsTotal +
                       (generator.HasTreasure && generator.TreasureCollected ? "  +  Treasure" : "") +
                       "   -   tekan R untuk dungeon berikutnya";
        }

        float left = panelVisible ? panelWidth + 16f : 0f;
        float areaWidth = screenWidth - left;

        if (title != null)
        {
            Rect rect = new Rect(left + areaWidth / 2f - 260f, 24f, 520f, 84f);
            GUI.DrawTexture(rect, bannerTexture);
            GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 40f), title, style);
            GUI.Label(new Rect(rect.x, rect.y + 50f, rect.width, 24f), subtitle, bannerSubStyle);
        }

        // Bar bawah selalu tampil (juga saat panel disembunyikan),
        // sehingga seed yang dipakai selalu terlihat di layar.
        string status =
            "SEED " + generator.Seed +
            "   |   " + generator.RoomCount + " room" +
            "   |   Item " + generator.ItemsCollected + "/" + generator.ItemsTotal +
            "   |   " + (generator.GoalReached ? "Goal tercapai" : "Capai Goal (portal)");

        Rect bar = new Rect(left + areaWidth / 2f - 280f, screenHeight - 42f, 560f, 30f);
        GUI.DrawTexture(bar, bannerTexture);
        GUI.Label(bar, status, bannerSubStyle);
    }

    // ---------------------------------------------------------------
    // GUI helpers

    private bool Foldout(bool open, string title)
    {
        if (GUILayout.Button((open ? "v  " : ">  ") + title, headerStyle))
            open = !open;

        return open;
    }

    private int IntSlider(string label, int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, labelStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(value.ToString(), valueStyle, GUILayout.Width(36));
        GUILayout.EndHorizontal();

        return Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
    }

    private void InitStyles()
    {
        if (titleStyle != null)
            return;

        panelTexture = MakeTexture(new Color(0.05f, 0.07f, 0.11f, 0.92f));
        bannerTexture = MakeTexture(new Color(0.03f, 0.04f, 0.07f, 0.85f));

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10)
        };
        boxStyle.normal.background = panelTexture;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 6, 2)
        };
        headerStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
        headerStyle.hover.textColor = new Color(0.35f, 0.85f, 1f);

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        labelStyle.normal.textColor = new Color(0.88f, 0.9f, 0.95f);

        valueStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight };

        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        smallStyle.normal.textColor = new Color(0.65f, 0.7f, 0.8f);

        okStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
        okStyle.normal.textColor = new Color(0.35f, 1f, 0.5f);

        badStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, wordWrap = true };
        badStyle.normal.textColor = new Color(1f, 0.4f, 0.35f);

        bannerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bannerStyle.normal.textColor = new Color(1f, 0.82f, 0.2f);

        bannerSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        bannerSubStyle.normal.textColor = Color.white;
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // ---------------------------------------------------------------
    // Snapshot parameter generator (untuk tombol Default & Terapkan)

    private struct Settings
    {
        public int seed;
        public int mapWidth, mapHeight;
        public int minLeafSize, maxLeafSize, minRoomSize, roomMargin, corridorWidth;
        public GoalPlacementStrategy goalStrategy;
        public int maxAttempts;
        public bool requireAllRooms;
        public int enemyCount, itemCount, safeRoomCount, floorTilesPerEnemy;
        public float safeRadius;
        public bool useEnemyDensity, spawnInRoomsOnly, keepMainPathClear, spawnBoss, spawnTreasure;

        public static Settings Capture(DungeonGenerator g)
        {
            return new Settings
            {
                seed = g.Seed,
                mapWidth = g.MapWidth,
                mapHeight = g.MapHeight,
                minLeafSize = g.MinLeafSize,
                maxLeafSize = g.MaxLeafSize,
                minRoomSize = g.MinRoomSize,
                roomMargin = g.RoomMargin,
                corridorWidth = g.CorridorWidth,
                goalStrategy = g.GoalStrategy,
                maxAttempts = g.MaxGenerationAttempts,
                requireAllRooms = g.RequireAllRoomsReachable,
                enemyCount = g.EnemyCount,
                itemCount = g.ItemCount,
                safeRoomCount = g.SafeRoomCount,
                floorTilesPerEnemy = g.FloorTilesPerEnemy,
                safeRadius = g.SafeRadius,
                useEnemyDensity = g.UseEnemyDensity,
                spawnInRoomsOnly = g.SpawnInRoomsOnly,
                keepMainPathClear = g.KeepMainPathClear,
                spawnBoss = g.SpawnBoss,
                spawnTreasure = g.SpawnTreasure
            };
        }

        public void Apply(DungeonGenerator g)
        {
            g.MapWidth = mapWidth;
            g.MapHeight = mapHeight;
            g.MinLeafSize = minLeafSize;
            g.MaxLeafSize = maxLeafSize;
            g.MinRoomSize = minRoomSize;
            g.RoomMargin = roomMargin;
            g.CorridorWidth = corridorWidth;
            g.GoalStrategy = goalStrategy;
            g.MaxGenerationAttempts = maxAttempts;
            g.RequireAllRoomsReachable = requireAllRooms;
            g.EnemyCount = enemyCount;
            g.ItemCount = itemCount;
            g.SafeRoomCount = safeRoomCount;
            g.FloorTilesPerEnemy = floorTilesPerEnemy;
            g.SafeRadius = safeRadius;
            g.UseEnemyDensity = useEnemyDensity;
            g.SpawnInRoomsOnly = spawnInRoomsOnly;
            g.KeepMainPathClear = keepMainPathClear;
            g.SpawnBoss = spawnBoss;
            g.SpawnTreasure = spawnTreasure;
        }
    }
}
