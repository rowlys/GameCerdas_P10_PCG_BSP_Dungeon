// Strategi penempatan Goal (Modul bagian 108 - Eksperimen 6)
public enum GoalPlacementStrategy
{
    // Strategi A: Start = room pertama, Goal = room terakhir
    LastRoom,

    // Strategi B: Goal = room terjauh secara geometris (Euclidean)
    FarthestEuclidean,

    // Strategi C: Goal = room dengan jarak shortest-path (BFS) terpanjang
    FarthestPath
}
