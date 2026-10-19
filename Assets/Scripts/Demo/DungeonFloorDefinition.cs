using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonTile { Empty, Floor, Wall }

/// <summary>씬 객체와 독립적인 층 도면. 저장 파일에도 복사하여 제작 중 도면 변경으로 기존 탐험이 깨지지 않게 한다.</summary>
[Serializable]
public sealed class DungeonFloorMap
{
    public const int MaximumSize = 64;
    private static readonly Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
    public int width = 20, height = 12;
    public int[] tiles = new int[240];
    public Vector2Int entrance = new(2, 1), stairsUp = new(2, 2), stairsDown = new(17, 9);
    public bool hasStairsUp, hasStairsDown;
    public const int MaximumHidingSpots = 64;
    public List<Vector2Int> hidingSpots = new();
    public DungeonTile Tile(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || tiles == null || tiles.Length != width * height
        ? DungeonTile.Empty : (DungeonTile)tiles[y * width + x];
    public bool Walkable(Vector2Int cell) => Tile(cell.x, cell.y) == DungeonTile.Floor;
    public Vector2 Center(Vector2Int cell) => new((cell.x - (width - 1) * .5f) * 1.45f, (cell.y - (height - 1) * .5f) * 1.45f);
    public bool HasHidingSpot(Vector2Int cell) => hidingSpots != null && hidingSpots.Contains(cell);
    public bool Reserved(Vector2Int cell) => cell == entrance || (hasStairsUp && cell == stairsUp) || (hasStairsDown && cell == stairsDown) || HasHidingSpot(cell);
    public DungeonFloorMap Copy() => new() { width = width, height = height, tiles = tiles == null ? null : (int[])tiles.Clone(),
        entrance = entrance, stairsUp = stairsUp, stairsDown = stairsDown, hasStairsUp = hasStairsUp, hasStairsDown = hasStairsDown,
        hidingSpots = hidingSpots == null ? new List<Vector2Int>() : new List<Vector2Int>(hidingSpots) };

    public bool IsValid(out string error)
    {
        error = null;
        if (width < 4 || height < 4 || width > MaximumSize || height > MaximumSize || tiles == null || tiles.Length != width * height)
        { error = "도면 크기는 4~64칸이며 타일 데이터 길이가 일치해야 합니다."; return false; }
        int count = 0;
        foreach (int tile in tiles) { if (tile < 0 || tile > 2) { error = "알 수 없는 타일 종류입니다."; return false; } if (tile == 1) count++; }
        if (count < 4 || !Walkable(entrance) || (hasStairsUp && !Walkable(stairsUp)) || (hasStairsDown && !Walkable(stairsDown)))
        { error = "입구와 계단은 바닥 위에 있어야 하며 바닥이 4칸 이상 필요합니다."; return false; }
        if ((hasStairsUp && stairsUp == entrance) || (hasStairsDown && stairsDown == entrance)
            || (hasStairsUp && hasStairsDown && stairsUp == stairsDown))
        { error = "입구와 위/아래 계단의 위치는 서로 달라야 합니다."; return false; }
        if (hidingSpots != null)
        {
            if (hidingSpots.Count > MaximumHidingSpots) { error = "은신처는 층마다 최대 64개입니다."; return false; }
            var unique = new HashSet<Vector2Int>();
            foreach (var cell in hidingSpots)
                if (!Walkable(cell) || cell == entrance || (hasStairsUp && cell == stairsUp) || (hasStairsDown && cell == stairsDown) || !unique.Add(cell))
                { error = "은신처는 입구·계단과 겹치지 않는 바닥 위에 하나씩 배치하세요."; return false; }
        }
        var visited = new HashSet<Vector2Int> { entrance }; var queue = new Queue<Vector2Int>(); queue.Enqueue(entrance);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var delta in directions)
            { var next = cell + delta; if (Walkable(next) && visited.Add(next)) queue.Enqueue(next); }
        }
        if (visited.Count != count) { error = "입구에서 도달할 수 없는 바닥이 있습니다. 통로를 연결하세요."; return false; }
        return true;
    }
    public int PopulationCapacity
    {
        get { int count = 0; if (tiles != null) foreach (int tile in tiles) if (tile == 1) count++;
            return Mathf.Max(0, count - 2 - (hasStairsUp ? 1 : 0) - (hasStairsDown ? 1 : 0) - (hidingSpots?.Count ?? 0)); }
    }
}

public sealed class DungeonFloorDefinition : ScriptableObject
{
    public string floorId = "floor.custom", displayName = "새 층";
    public DungeonFloorMap map = new();
    public MonsterSpawnEntry[] monsters = Array.Empty<MonsterSpawnEntry>();
    public CorpseSpawnEntry[] corpses = Array.Empty<CorpseSpawnEntry>();
}
