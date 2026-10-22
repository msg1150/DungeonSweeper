using UnityEngine;

/// <summary>구 저장과 초기 도면 변환 전용 템플릿. 현재 런의 지도·탐색 버퍼를 수정하지 않는다.</summary>
public static class LegacyDungeonLayouts
{
    private static readonly DungeonFloorMap[] templates = BuildTemplates();
    private static readonly int[] capacities = { templates[0].PopulationCapacity, templates[1].PopulationCapacity, templates[2].PopulationCapacity };

    public static DungeonFloorMap Create(int index)
        => templates[index >= 0 && index < templates.Length ? index : Random.Range(0, templates.Length)].Copy();

    public static int PopulationCapacity(int index)
        => index >= 0 && index < capacities.Length ? capacities[index] : 0;

    private static DungeonFloorMap[] BuildTemplates()
    {
        var maps = new[] { new DungeonFloorMap(), new DungeonFloorMap(), new DungeonFloorMap() };
        foreach (var map in maps) AddRoom(map, 0, 0, 4, 3);
        BuildForkVault(maps[0]); BuildRingCorridor(maps[1]); BuildSteppedCrypt(maps[2]);
        return maps;
    }
    private static void BuildForkVault(DungeonFloorMap map)
    {
        AddRoom(map, 6, 0, 10, 3); AddRoom(map, 13, 0, 18, 4); AddRoom(map, 1, 6, 6, 10); AddRoom(map, 8, 5, 13, 9); AddRoom(map, 15, 7, 19, 11);
        AddHall(map, 5, 1, 6, 1); AddHall(map, 11, 1, 13, 1); AddHall(map, 3, 4, 3, 6); AddHall(map, 9, 4, 9, 5); AddHall(map, 16, 5, 16, 7); AddHall(map, 7, 7, 8, 7); AddHall(map, 14, 8, 15, 8);
    }
    private static void BuildRingCorridor(DungeonFloorMap map)
    {
        AddRoom(map, 7, 0, 12, 3); AddRoom(map, 14, 3, 19, 7); AddRoom(map, 9, 8, 14, 11); AddRoom(map, 1, 6, 6, 10); AddRoom(map, 7, 5, 10, 7);
        AddHall(map, 5, 1, 7, 1); AddHall(map, 10, 4, 10, 5); AddHall(map, 12, 2, 15, 4); AddHall(map, 16, 7, 16, 8); AddHall(map, 14, 9, 16, 9); AddHall(map, 6, 7, 7, 7); AddHall(map, 4, 4, 4, 6); AddHall(map, 7, 9, 9, 9);
    }
    private static void BuildSteppedCrypt(DungeonFloorMap map)
    {
        AddRoom(map, 1, 5, 7, 8); AddRoom(map, 9, 7, 15, 11); AddRoom(map, 14, 1, 19, 5); AddRoom(map, 8, 1, 12, 4); AddRoom(map, 16, 7, 19, 10);
        AddHall(map, 3, 4, 3, 5); AddHall(map, 7, 6, 9, 6); AddHall(map, 11, 5, 11, 7); AddHall(map, 12, 2, 14, 2); AddHall(map, 16, 5, 16, 7); AddHall(map, 15, 9, 16, 9); AddHall(map, 5, 3, 8, 3);
    }

    private static void AddRoom(DungeonFloorMap map, int minX, int minY, int maxX, int maxY) => AddHall(map, minX, minY, maxX, maxY);
    private static void AddHall(DungeonFloorMap map, int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++) map.tiles[y * map.width + x] = 1;
    }
}
