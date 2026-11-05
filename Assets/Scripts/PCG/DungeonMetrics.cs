// Dungeon Metrics (Modul bagian 120): evaluasi kuantitatif hasil PCG.
[System.Serializable]
public class DungeonMetrics
{
    public int seed;
    public int requestedSeed;
    public int attempts;
    public bool valid;
    public string validationMessage = "-";

    public int mapWidth;
    public int mapHeight;
    public int rooms;
    public int reachableRooms;
    public int corridors;

    public int floorTiles;
    public int roomFloorTiles;
    public int corridorTiles;
    public float floorPercent;
    public int wallObjects;

    public int startGoalPathLength;
    public float startGoalEuclidean;

    public int enemies;
    public int items;
    public bool boss;
    public bool treasure;

    public float generationMs;

    public string ToLogString()
    {
        return
            "Seed           : " + seed +
            (requestedSeed != seed ? " (diminta " + requestedSeed + ")" : "") + "\n" +
            "Valid          : " + (valid ? "Ya" : "Tidak - " + validationMessage) +
            " (percobaan " + attempts + ")\n" +
            "Map            : " + mapWidth + " x " + mapHeight + "\n" +
            "Rooms          : " + rooms + " (terjangkau " + reachableRooms + ")\n" +
            "Corridors      : " + corridors + "\n" +
            "Floor Tiles    : " + floorTiles + " (room " + roomFloorTiles +
            ", corridor " + corridorTiles + ")\n" +
            "Floor %        : " + floorPercent.ToString("0.0") + "%\n" +
            "Wall Objects   : " + wallObjects + "\n" +
            "StartGoalDist  : " + startGoalPathLength + " sel (BFS), " +
            startGoalEuclidean.ToString("0.0") + " (Euclid)\n" +
            "Enemies        : " + enemies + (boss ? " + Boss" : "") + "\n" +
            "Items          : " + items + (treasure ? " + Treasure" : "") + "\n" +
            "Gen Time       : " + generationMs.ToString("0.0") + " ms";
    }
}
