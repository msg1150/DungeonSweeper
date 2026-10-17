using System.Collections.Generic;
using UnityEngine;

/// <summary>넓은 방과 짧은 복도로 이어진 던전 평면을 런타임에 생성한다.</summary>
public static class DungeonLayoutFactory
{
    private static int Columns = 20;
    private static int Rows = 12;
    private const float CellSize = 1.45f;
    private static readonly bool[,] floor = new bool[DungeonFloorMap.MaximumSize, DungeonFloorMap.MaximumSize];
    private static DungeonFloorMap activeMap;
    private static int layoutIndex;
    private static Sprite floorSprite;
    private static Sprite wallSprite;
    private static readonly string[] layoutNames = { "갈림길 저장고", "고리형 회랑", "계단식 묘실" };
    // AI 탐색은 메인 스레드에서 순차 실행하며 최대 64×64칸의 작업 공간을 공유한다.
    private static readonly int[,] searchDistances = new int[DungeonFloorMap.MaximumSize, DungeonFloorMap.MaximumSize];
    private static readonly Vector2Int[,] searchPrevious = new Vector2Int[DungeonFloorMap.MaximumSize, DungeonFloorMap.MaximumSize];
    private static readonly Vector2Int[] searchQueue = new Vector2Int[DungeonFloorMap.MaximumSize * DungeonFloorMap.MaximumSize];
    private static readonly Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
    private static readonly List<Vector2> roamCandidates = new(Columns * Rows), roamDistant = new(Columns * Rows);
    private static readonly List<Vector2> spawnFloorPoints = new(), spawnCandidates = new();
    private static readonly List<float> spawnEntranceDistancesSquared = new();
    private static readonly List<Vector2> spawnOccupiedPoints = new();
    private static readonly List<float> spawnClearancesSquared = new();
    private static bool spawnPointsReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        layoutIndex = 0;
        Columns = 20; Rows = 12; activeMap = null;
        floorSprite = null;
        wallSprite = null;
        System.Array.Clear(floor, 0, floor.Length);
        spawnPointsReady = false;
        spawnFloorPoints.Clear(); spawnCandidates.Clear(); spawnEntranceDistancesSquared.Clear();
        spawnOccupiedPoints.Clear(); spawnClearancesSquared.Clear();
    }

    public static Vector2 Entrance => activeMap == null ? CellCenter(2, 1) : activeMap.Center(activeMap.entrance);
    public static DungeonFloorMap ActiveMap => activeMap;
    public static bool IsReserved(Vector2 point) => activeMap == null ? Vector2.Distance(point, Entrance) < .1f : activeMap.Reserved(WorldToCell(point));
    public static bool IsSolidWallCell(int x, int y) => activeMap != null && activeMap.Tile(x, y) == DungeonTile.Wall;
    public static int Width => Columns;
    public static int Height => Rows;
    public static int LayoutIndex => layoutIndex;
    public static string LayoutName => layoutNames[layoutIndex];

    public static void CreateLayout(int savedLayoutIndex = -1)
        => CreateLayout(null, savedLayoutIndex);

    public static void CreateLayout(DungeonFloorMap map, int savedLayoutIndex = 0)
    {
        if (GameObject.Find("Runtime Dungeon Layout") != null) return;
        spawnPointsReady = false;
        System.Array.Clear(floor, 0, floor.Length);
        if (map == null) BuildFloorPlan(savedLayoutIndex);
        else
        {
            if (!map.IsValid(out string error)) throw new System.InvalidOperationException(error);
            activeMap = map; Columns = map.width; Rows = map.height; layoutIndex = Mathf.Clamp(savedLayoutIndex, 0, 2);
            for (int x = 0; x < Columns; x++) for (int y = 0; y < Rows; y++) floor[x, y] = map.Tile(x, y) == DungeonTile.Floor;
        }
        floorSprite = CasualArtLibrary.LoadFull("Sprites/Environment/dungeon-floor-casual", 100f);
        wallSprite = CasualArtLibrary.LoadFull("Sprites/Environment/dungeon-wall-casual", 100f);
        GameObject root = new GameObject("Runtime Dungeon Layout");

        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (IsSolidWallCell(x, y)) CreateWall(root.transform, $"Painted Wall {x}-{y}", CellCenter(x, y), new Vector2(CellSize, CellSize));
            if (!floor[x, y]) continue;
            Vector2 center = CellCenter(x, y);
            CreateFloor(root.transform, center);
            if (!IsFloor(x - 1, y)) CreateWall(root.transform, $"West {x}-{y}", center + Vector2.left * CellSize * .5f, new Vector2(.18f, CellSize + .18f));
            if (!IsFloor(x + 1, y)) CreateWall(root.transform, $"East {x}-{y}", center + Vector2.right * CellSize * .5f, new Vector2(.18f, CellSize + .18f));
            if (!IsFloor(x, y - 1)) CreateWall(root.transform, $"South {x}-{y}", center + Vector2.down * CellSize * .5f, new Vector2(CellSize + .18f, .18f));
            if (!IsFloor(x, y + 1)) CreateWall(root.transform, $"North {x}-{y}", center + Vector2.up * CellSize * .5f, new Vector2(CellSize + .18f, .18f));
        }
    }

    public static Vector2 RandomGateSpawn()
    {
        return RandomFloorPosition(16f);
    }

    public static Vector2 RandomFloorPosition(float minimumDistance)
    {
        EnsureSpawnPoints();
        spawnCandidates.Clear();
        float minimumSquared = Mathf.Max(0f, minimumDistance) * Mathf.Max(0f, minimumDistance);
        float farthestSquared = 0f;
        for (int i = 0; i < spawnFloorPoints.Count; i++)
        {
            float distanceSquared = spawnEntranceDistancesSquared[i];
            farthestSquared = Mathf.Max(farthestSquared, distanceSquared);
            if (distanceSquared >= minimumSquared) spawnCandidates.Add(spawnFloorPoints[i]);
        }

        // Extra monster types may request a distance beyond the map's bounds.
        // Keep their spawns in the deeper part of the available layout instead.
        if (spawnCandidates.Count == 0)
            for (int i = 0; i < spawnFloorPoints.Count; i++)
                if (spawnEntranceDistancesSquared[i] >= farthestSquared * .75f * .75f)
                    spawnCandidates.Add(spawnFloorPoints[i]);

        if (spawnCandidates.Count == 0) return Entrance;
        return spawnCandidates[Random.Range(0, spawnCandidates.Count)];
    }

    public static Vector2 RandomRoamPosition(Vector2 origin, Vector2? previousOrigin = null, float? minimumOverride = null)
    {
        var candidates = roamCandidates; candidates.Clear();
        var distant = roamDistant; distant.Clear();
        var distances = ReachableDistances(ToFloorCell(origin));
        float minimum = minimumOverride ?? DungeonTuning.Active.patrolTravelDistance;
        Vector2 farthest = origin;
        float farthestDistance = -1f;
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (distances[x, y] < 0) continue;
            Vector2 point = CellCenter(x, y);
            if (IsReserved(point)) continue;
            float distance = Vector2.Distance(point, origin);
            if (distance > farthestDistance) { farthest = point; farthestDistance = distance; }
            if (distance < minimum || distances[x, y] * CellSize < minimum || Vector2.Distance(point, Entrance) < 5f) continue;
            distant.Add(point);
            if (!previousOrigin.HasValue || Vector2.Distance(point, previousOrigin.Value) >= minimum * .6f) candidates.Add(point);
        }
        if (candidates.Count == 0) candidates = distant;
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : farthest;
    }

    public static void RandomPatrolPoints(float minimumDistance, out Vector2 pointA, out Vector2 pointB)
    {
        pointA = RandomFloorPosition(minimumDistance);
        pointB = RandomRoamPosition(pointA);
    }

    public static Vector2 PickSpawnPosition(float minimumDistance, float maximumDistance, IReadOnlyList<Vector2> occupied, float separation)
    {
        EnsureSpawnPoints();
        UpdateSpawnClearances(occupied);
        spawnCandidates.Clear();
        float minimumSquared = Mathf.Max(0f, minimumDistance) * Mathf.Max(0f, minimumDistance);
        float maximumSquared = Mathf.Max(0f, maximumDistance) * Mathf.Max(0f, maximumDistance);
        float separationSquared = Mathf.Max(0f, separation) * Mathf.Max(0f, separation);
        Vector2 fallback = Entrance;
        float bestGapSquared = -1f;
        for (int i = 0; i < spawnFloorPoints.Count; i++)
        {
            Vector2 point = spawnFloorPoints[i];
            float entranceSquared = spawnEntranceDistancesSquared[i];
            if (IsReserved(point)) continue;
            float gapSquared = spawnClearancesSquared[i];
            if (gapSquared < .0001f) continue; // 밀집 설정에서도 같은 칸을 중복 사용하지 않는다.
            if (gapSquared > bestGapSquared) { fallback = point; bestGapSquared = gapSquared; }
            if (entranceSquared >= minimumSquared && entranceSquared <= maximumSquared && gapSquared >= separationSquared) spawnCandidates.Add(point);
        }
        return spawnCandidates.Count > 0 ? spawnCandidates[Random.Range(0, spawnCandidates.Count)] : fallback;
    }

    private static void EnsureSpawnPoints()
    {
        if (spawnPointsReady) return;
        spawnFloorPoints.Clear(); spawnEntranceDistancesSquared.Clear();
        spawnOccupiedPoints.Clear(); spawnClearancesSquared.Clear();
        // 입구에서 닿는 바닥은 층 구성당 한 번만 찾는다. AI의 공용 BFS 버퍼와 분리해
        // 순찰/추적 탐색이 실행되어도 스폰 후보 캐시가 덮어써지지 않게 한다.
        var distances = ReachableDistances(ToFloorCell(Entrance));
        Vector2 entrance = Entrance;
        for (int x = 0; x < Columns; x++) for (int y = 0; y < Rows; y++)
        {
            if (distances[x, y] < 0) continue;
            Vector2 point = CellCenter(x, y);
            float distanceSquared = (point - entrance).sqrMagnitude;
            spawnFloorPoints.Add(point); spawnEntranceDistancesSquared.Add(distanceSquared); spawnClearancesSquared.Add(distanceSquared);
        }
        spawnPointsReady = true;
    }

    private static void UpdateSpawnClearances(IReadOnlyList<Vector2> occupied)
    {
        int count = occupied?.Count ?? 0;
        bool rebuild = count < spawnOccupiedPoints.Count;
        for (int i = 0; !rebuild && i < spawnOccupiedPoints.Count; i++)
            rebuild = !spawnOccupiedPoints[i].Equals(occupied[i]);
        if (rebuild)
        {
            spawnOccupiedPoints.Clear();
            for (int i = 0; i < spawnFloorPoints.Count; i++) spawnClearancesSquared[i] = spawnEntranceDistancesSquared[i];
        }
        // 스폰 계획은 기존 배치 뒤에 하나씩 추가한다. 새 위치만 반영하여 큰 맵에서도
        // 같은 개체와의 거리를 모든 후속 스폰에서 다시 계산하지 않는다.
        // 목록이 축소되거나 기존 위치가 바뀐 경우에는 위에서 안전하게 다시 계산한다.
        for (int j = spawnOccupiedPoints.Count; j < count; j++)
        {
            Vector2 point = occupied[j]; spawnOccupiedPoints.Add(point);
            for (int i = 0; i < spawnFloorPoints.Count; i++)
                spawnClearancesSquared[i] = Mathf.Min(spawnClearancesSquared[i], (spawnFloorPoints[i] - point).sqrMagnitude);
        }
    }

    private static int[,] ReachableDistances(Vector2Int start)
    {
        Search(start);
        return searchDistances; // 다음 탐색에서 덮어쓰므로 호출자가 보관하거나 재진입해서 사용하지 않는다.
    }

    private static void Search(Vector2Int start, Vector2Int? end = null)
    {
        for (int x = 0; x < Columns; x++) for (int y = 0; y < Rows; y++) searchDistances[x, y] = -1;
        if (!IsFloor(start.x, start.y)) return;
        int head = 0, tail = 0;
        searchQueue[tail++] = start; searchDistances[start.x, start.y] = 0; searchPrevious[start.x, start.y] = start;
        while (head < tail)
        {
            var cell = searchQueue[head++];
            if (end.HasValue && cell == end.Value) break;
            foreach (var direction in directions)
            {
                var next = cell + direction;
                if (!IsFloor(next.x, next.y) || searchDistances[next.x, next.y] >= 0) continue;
                searchPrevious[next.x, next.y] = cell;
                searchDistances[next.x, next.y] = searchDistances[cell.x, cell.y] + 1;
                searchQueue[tail++] = next;
            }
        }
    }

    public static bool TryBuildPath(Vector2 from, Vector2 destination, List<Vector2> path)
    {
        path.Clear();
        if (!GameSaveData.Finite(from) || !GameSaveData.Finite(destination)) return false;
        Vector2Int start = ToFloorCell(from), end = ToFloorCell(destination);
        Search(start, end);
        if (searchDistances[end.x, end.y] < 0) return false;
        Vector2Int step = end;
        while (step != start) { path.Add(CellCenter(step.x, step.y)); step = searchPrevious[step.x, step.y]; }
        path.Add(CellCenter(start.x, start.y)); path.Reverse();
        path.Add(IsWalkablePosition(destination) ? destination : CellCenter(end.x, end.y));
        return true;
    }

    public static Vector2Int WorldToCell(Vector2 position) => ToFloorCell(position);
    public static bool IsWalkableCell(int x, int y) => IsFloor(x, y);
    public static bool IsWalkablePosition(Vector2 position) => GameSaveData.Finite(position) && IsFloor(
        Mathf.RoundToInt(position.x / CellSize + (Columns - 1) * .5f),
        Mathf.RoundToInt(position.y / CellSize + (Rows - 1) * .5f));

    public static Vector2 CellCenter(int x, int y) => new Vector2((x - (Columns - 1) * .5f) * CellSize, (y - (Rows - 1) * .5f) * CellSize);

    private static void BuildFloorPlan(int savedLayoutIndex)
    {
        spawnPointsReady = false;
        activeMap = null; Columns = 20; Rows = 12;
        layoutIndex = savedLayoutIndex >= 0 && savedLayoutIndex < 3 ? savedLayoutIndex : Random.Range(0, 3);
        AddRoom(0, 0, 4, 3); // 모든 템플릿은 같은 시작 방을 공유한다.
        if (layoutIndex == 0) BuildForkVault();
        else if (layoutIndex == 1) BuildRingCorridor();
        else BuildSteppedCrypt();
    }

    // Compute capacity from reachable cells without changing the active run's geometry.
    public static int PopulationCapacity(int index)
    {
        if (index < 0 || index > 2) return 0;
        bool[,] previous = (bool[,])floor.Clone();
        int previousIndex = layoutIndex;
        int previousWidth = Columns, previousHeight = Rows; var previousMap = activeMap;
        try
        {
            System.Array.Clear(floor, 0, floor.Length);
            BuildFloorPlan(index);
            var reachable = ReachableDistances(new Vector2Int(2, 1));
            int count = 0;
            for (int x = 0; x < Columns; x++) for (int y = 0; y < Rows; y++) if (reachable[x,y] >= 0) count++;
            return Mathf.Max(0, count - 2); // Entrance and special gate each reserve one cell.
        }
        finally { System.Array.Copy(previous, floor, floor.Length); layoutIndex = previousIndex; Columns = previousWidth; Rows = previousHeight; activeMap = previousMap; }
    }

    public static DungeonFloorMap LegacyMap(int index)
    {
        var previous = (bool[,])floor.Clone(); int previousIndex = layoutIndex, previousWidth = Columns, previousHeight = Rows;
        var previousMap = activeMap;
        try
        {
            System.Array.Clear(floor, 0, floor.Length); BuildFloorPlan(index);
            var map = new DungeonFloorMap();
            for (int x = 0; x < Columns; x++) for (int y = 0; y < Rows; y++) if (floor[x, y]) map.tiles[y * Columns + x] = 1;
            return map;
        }
        finally { System.Array.Copy(previous, floor, floor.Length); layoutIndex = previousIndex; Columns = previousWidth; Rows = previousHeight; activeMap = previousMap; }
    }

    private static void BuildForkVault()
    {
        AddRoom(6, 0, 10, 3); AddRoom(13, 0, 18, 4); AddRoom(1, 6, 6, 10); AddRoom(8, 5, 13, 9); AddRoom(15, 7, 19, 11);
        AddHall(5, 1, 6, 1); AddHall(11, 1, 13, 1); AddHall(3, 4, 3, 6); AddHall(9, 4, 9, 5); AddHall(16, 5, 16, 7); AddHall(7, 7, 8, 7); AddHall(14, 8, 15, 8);
    }
    private static void BuildRingCorridor()
    {
        AddRoom(7, 0, 12, 3); AddRoom(14, 3, 19, 7); AddRoom(9, 8, 14, 11); AddRoom(1, 6, 6, 10); AddRoom(7, 5, 10, 7);
        AddHall(5, 1, 7, 1); AddHall(10, 4, 10, 5); AddHall(12, 2, 15, 4); AddHall(16, 7, 16, 8); AddHall(14, 9, 16, 9); AddHall(6, 7, 7, 7); AddHall(4, 4, 4, 6); AddHall(7, 9, 9, 9);
    }
    private static void BuildSteppedCrypt()
    {
        AddRoom(1, 5, 7, 8); AddRoom(9, 7, 15, 11); AddRoom(14, 1, 19, 5); AddRoom(8, 1, 12, 4); AddRoom(16, 7, 19, 10);
        AddHall(3, 4, 3, 5); AddHall(7, 6, 9, 6); AddHall(11, 5, 11, 7); AddHall(12, 2, 14, 2); AddHall(16, 5, 16, 7); AddHall(15, 9, 16, 9); AddHall(5, 3, 8, 3);
    }

    private static void AddRoom(int minX, int minY, int maxX, int maxY) => AddHall(minX, minY, maxX, maxY);
    private static void AddHall(int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++) floor[x, y] = true;
    }
    private static bool IsFloor(int x, int y) => x >= 0 && x < Columns && y >= 0 && y < Rows && floor[x, y];
    private static Vector2Int ToFloorCell(Vector2 position)
    {
        Vector2Int nearest = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(position.x / CellSize + (Columns - 1) * .5f), 0, Columns - 1),
            Mathf.Clamp(Mathf.RoundToInt(position.y / CellSize + (Rows - 1) * .5f), 0, Rows - 1));
        if (IsFloor(nearest.x, nearest.y)) return nearest;
        for (int radius = 1; radius < Mathf.Max(Columns, Rows); radius++)
        for (int x = Mathf.Max(0, nearest.x - radius); x <= Mathf.Min(Columns - 1, nearest.x + radius); x++)
        for (int y = Mathf.Max(0, nearest.y - radius); y <= Mathf.Min(Rows - 1, nearest.y + radius); y++)
            if ((Mathf.Abs(x - nearest.x) == radius || Mathf.Abs(y - nearest.y) == radius) && IsFloor(x, y))
                return new Vector2Int(x, y);
        return new Vector2Int(2, 1);
    }
    private static void CreateFloor(Transform parent, Vector2 position)
    {
        // 입체 표시에서는 합친 메시가 바닥을 그린다. 큰 도면에서 숨겨진 타일 객체 수천 개를 만들지 않는다.
        if (ThreeDPresentationConfig.Enabled) return;
        GameObject tile = new GameObject("Dungeon Floor");
        tile.transform.SetParent(parent);
        tile.transform.position = position;
        tile.transform.localScale = new Vector3(CellSize, CellSize, 1f);
        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = SafeSprite(floorSprite);
        renderer.color = Color.white;
        renderer.sortingOrder = 0;
        if (renderer.sprite == null) return;
        float unit = Mathf.Max(.01f, renderer.sprite.bounds.size.x);
        tile.transform.localScale = new Vector3(CellSize / unit, CellSize / unit, 1f);
    }
    private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = new Vector3(size.x, size.y, 1f);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        if (ThreeDPresentationConfig.Enabled) return; // 논리 충돌체만 남기고 표시는 합친 벽 메시가 담당한다.
        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = SafeSprite(wallSprite);
        renderer.color = Color.white;
        renderer.sortingOrder = 1;
        if (renderer.sprite == null) return;
        Vector2 bounds = renderer.sprite.bounds.size;
        bounds.x = Mathf.Max(.01f, bounds.x); bounds.y = Mathf.Max(.01f, bounds.y);
        // Collider coordinates use the same local space as the sprite.
        collider.size = bounds;
        wall.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
    }
    private static Sprite SafeSprite(Sprite candidate)
    {
        Sprite result = candidate != null ? candidate : CasualArtLibrary.WhiteSprite;
        if (result == null) Debug.LogError("Dungeon tile sprite creation failed; using no visual for this tile.");
        return result;
    }
}
