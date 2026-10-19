using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DungeonGenerationSettings
{
    public int floorCount = 3, width = 32, height = 20, seed = 12345;
    public bool individualSizes;
    [Range(0, 16)] public int hidingSpotsPerFloor = 2;
    public List<Vector2Int> floorSizes = new();
    public Vector2Int Size(int index) => individualSizes ? floorSizes[index] : new Vector2Int(width,height);
    public bool IsValid(out string error)
    {
        error = null;
        if (hidingSpotsPerFloor < 0 || hidingSpotsPerFloor > 16)
        { error = "자동 생성 은신처 수는 층마다 0~16개입니다."; return false; }
        if (floorCount < 1 || floorCount > DungeonExpeditionData.MaximumFloors)
        { error = "층 수는 1~16 사이로 입력하세요."; return false; }
        if (individualSizes && (floorSizes == null || floorSizes.Count != floorCount))
        { error = "모든 층의 가로·세로 칸 수를 입력하세요."; return false; }
        for (int i = 0; i < floorCount; i++)
        {
            var size = Size(i);
            if (size.x < 4 || size.y < 4 || size.x > DungeonFloorMap.MaximumSize || size.y > DungeonFloorMap.MaximumSize)
            { error = $"{i+1}층의 가로·세로 칸 수는 각각 4~64 사이여야 합니다."; return false; }
        }
        return true;
    }
}

/// <summary>
/// 제작 시에만 호출하는 순수 도면 생성기. 영역 분할로 만든 방을 연결하므로 모든 바닥이 이어진다.
/// 독립 PRNG를 사용해 미리보기를 만들 때 진행 중 게임의 Unity 난수 상태를 바꾸지 않는다.
/// </summary>
public static class DungeonMapGenerator
{
    public const int Version = 2;
    private static readonly Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
    public static DungeonFloorMap[] Generate(DungeonGenerationSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (!settings.IsValid(out string error)) throw new ArgumentException(error);
        var random = new System.Random(settings.seed);
        var result = new DungeonFloorMap[settings.floorCount];
        for (int i = 0; i < result.Length; i++)
        {
            var size = settings.Size(i);
            var map = new DungeonFloorMap { width = size.x, height = size.y, tiles = new int[size.x*size.y],
                hasStairsUp = i > 0, hasStairsDown = i < result.Length - 1 };
            var rooms = new List<Vector2Int>();
            Split(map, new RectInt(1,1,size.x-2,size.y-2), random, rooms);
            // 트리 통로에 일부 순환 통로를 추가해 모든 방이 막다른 길이 되는 것을 줄인다.
            for (int loop = 0; loop < rooms.Count / 4; loop++) Connect(map, rooms[random.Next(rooms.Count)], rooms[random.Next(rooms.Count)], random);
            map.entrance = Entrance(map);
            int[] distances = Distances(map, map.entrance);
            map.stairsUp = PickMarker(map, distances, false, random);
            map.stairsDown = PickMarker(map, distances, true, random);
            AddHidingSpots(map, settings.hidingSpotsPerFloor, random);
            AddWalls(map);
            if (!map.IsValid(out error)) throw new InvalidOperationException("생성 도면 검증 실패: " + error);
            result[i] = map;
        }
        return result;
    }
    private static Vector2Int Split(DungeonFloorMap map, RectInt bounds, System.Random random, List<Vector2Int> rooms)
    {
        bool splitX = bounds.width >= 12, splitY = bounds.height >= 12;
        if (splitX || splitY)
        {
            bool horizontal = splitX && (!splitY || (bounds.width > bounds.height * 1.4f) || (bounds.height <= bounds.width * 1.4f && random.Next(2) == 0));
            RectInt a, b;
            if (horizontal)
            {
                int cut = random.Next(5,bounds.width-4);
                a = new RectInt(bounds.x,bounds.y,cut,bounds.height); b = new RectInt(bounds.x+cut,bounds.y,bounds.width-cut,bounds.height);
            }
            else
            {
                int cut = random.Next(5,bounds.height-4);
                a = new RectInt(bounds.x,bounds.y,bounds.width,cut); b = new RectInt(bounds.x,bounds.y+cut,bounds.width,bounds.height-cut);
            }
            var first = Split(map,a,random,rooms); var second = Split(map,b,random,rooms);
            Connect(map,first,second,random); return random.Next(2) == 0 ? first : second;
        }
        int padX = bounds.width >= 5 ? 1 : 0, padY = bounds.height >= 5 ? 1 : 0;
        int maxWidth = bounds.width - 2*padX, maxHeight = bounds.height - 2*padY;
        int width = random.Next(Mathf.Max(2,maxWidth/2),maxWidth+1), height = random.Next(Mathf.Max(2,maxHeight/2),maxHeight+1);
        int x = random.Next(bounds.x+padX,bounds.xMax-padX-width+1), y = random.Next(bounds.y+padY,bounds.yMax-padY-height+1);
        for (int ix = x; ix < x+width; ix++) for (int iy = y; iy < y+height; iy++) map.tiles[iy*map.width+ix] = 1;
        var center = new Vector2Int(x+width/2,y+height/2); rooms.Add(center); return center;
    }
    private static void Connect(DungeonFloorMap map, Vector2Int from, Vector2Int to, System.Random random)
    {
        var corner = random.Next(2) == 0 ? new Vector2Int(from.x,to.y) : new Vector2Int(to.x,from.y);
        CarveLine(map,from,corner); CarveLine(map,corner,to);
    }
    private static void CarveLine(DungeonFloorMap map, Vector2Int from, Vector2Int to)
    {
        while (true)
        {
            map.tiles[from.y*map.width+from.x] = 1;
            if (from == to) break;
            from += new Vector2Int(Math.Sign(to.x-from.x),Math.Sign(to.y-from.y));
        }
    }
    private static Vector2Int Entrance(DungeonFloorMap map)
    {
        Vector2Int best = Vector2Int.zero; int score = int.MaxValue;
        for (int x = 1; x < map.width-1; x++) for (int y = 1; y < map.height-1; y++)
            if (map.Walkable(new Vector2Int(x,y)) && x+y < score) { best = new Vector2Int(x,y); score = x+y; }
        return best;
    }
    private static int[] Distances(DungeonFloorMap map, Vector2Int start)
    {
        var distances = new int[map.tiles.Length]; Array.Fill(distances,-1);
        var queue = new Queue<Vector2Int>(); queue.Enqueue(start); distances[start.y*map.width+start.x] = 0;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var delta in directions)
            {
                var next = cell+delta; if (!map.Walkable(next)) continue;
                int index = next.y*map.width+next.x;
                if (distances[index] >= 0) continue;
                distances[index] = distances[cell.y*map.width+cell.x]+1; queue.Enqueue(next);
            }
        }
        return distances;
    }
    private static Vector2Int PickMarker(DungeonFloorMap map, int[] distances, bool far, System.Random random)
    {
        var candidates = new List<Vector2Int>(); int best = far ? -1 : int.MaxValue;
        for (int x = 0; x < map.width; x++) for (int y = 0; y < map.height; y++)
        {
            var cell = new Vector2Int(x,y); int distance = distances[y*map.width+x];
            if (distance <= 0 || (far && cell == map.stairsUp)) continue;
            // 위층에서 내려올 도착점은 입구 근처, 아래층 계단은 통로 거리상 먼 곳에 둔다.
            int score = far ? distance : Mathf.Abs(distance-2);
            if ((far && score > best) || (!far && score < best)) { best = score; candidates.Clear(); }
            if (score == best) candidates.Add(cell);
        }
        return candidates[random.Next(candidates.Count)];
    }
    private static void AddHidingSpots(DungeonFloorMap map, int requested, System.Random random)
    {
        var candidates = new List<Vector2Int>();
        for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
        {
            var cell = new Vector2Int(x, y);
            if (map.Walkable(cell) && !map.Reserved(cell)) candidates.Add(cell);
        }
        // 추가 탈출 포탈에 필요한 한 칸은 남긴다. 작은 도면에는 가능한 수만 배치한다.
        int count = Mathf.Min(requested, Mathf.Max(0, candidates.Count - 1));
        for (int i = 0; i < count; i++)
        {
            int bestIndex = -1; float bestScore = float.NegativeInfinity;
            for (int j = 0; j < candidates.Count; j++)
            {
                var cell = candidates[j];
                float separation = Vector2Int.Distance(cell, map.entrance);
                foreach (var existing in map.hidingSpots) separation = Mathf.Min(separation, Vector2Int.Distance(cell, existing));
                int edges = 0; foreach (var delta in directions) if (!map.Walkable(cell + delta)) edges++;
                float score = separation + edges * .75f + (float)random.NextDouble() * .3f;
                if (score > bestScore) { bestScore = score; bestIndex = j; }
            }
            map.hidingSpots.Add(candidates[bestIndex]); candidates.RemoveAt(bestIndex);
        }
    }
    private static void AddWalls(DungeonFloorMap map)
    {
        for (int x = 0; x < map.width; x++) for (int y = 0; y < map.height; y++)
        {
            if (map.Tile(x,y) != DungeonTile.Empty) continue;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                if (map.Tile(x+dx,y+dy) == DungeonTile.Floor) map.tiles[y*map.width+x] = 2;
        }
    }
}
