using System.Collections.Generic;
using UnityEngine;

/// <summary>넓은 방과 짧은 복도로 이어진 던전 평면을 런타임에 생성한다.</summary>
public static class DungeonLayoutFactory
{
    private const int Columns = 20;
    private const int Rows = 12;
    private const float CellSize = 1.45f;
    private static readonly bool[,] floor = new bool[Columns, Rows];
    private static int layoutIndex;
    private static Sprite floorSprite;
    private static Sprite wallSprite;
    private static readonly string[] layoutNames = { "갈림길 저장고", "고리형 회랑", "계단식 묘실" };
    // AI 탐색은 Unity 메인 스레드에서 순차 실행한다. 최대 240칸의 작업 공간을 공유한다.
    private static readonly int[,] searchDistances = new int[Columns, Rows];
    private static readonly Vector2Int[,] searchPrevious = new Vector2Int[Columns, Rows];
    private static readonly Vector2Int[] searchQueue = new Vector2Int[Columns * Rows];
    private static readonly Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
    private static readonly List<Vector2> roamCandidates = new(Columns * Rows), roamDistant = new(Columns * Rows);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        layoutIndex = 0;
        floorSprite = null;
        wallSprite = null;
        System.Array.Clear(floor, 0, floor.Length);
    }

    public static Vector2 Entrance => CellCenter(2, 1);
    public static int Width => Columns;
    public static int Height => Rows;
    public static int LayoutIndex => layoutIndex;
    public static string LayoutName => layoutNames[layoutIndex];

    public static void CreateLayout(int savedLayoutIndex = -1)
    {
        if (GameObject.Find("Runtime Dungeon Layout") != null) return;
        System.Array.Clear(floor, 0, floor.Length);
        BuildFloorPlan(savedLayoutIndex);
        floorSprite = CasualArtLibrary.LoadFull("Sprites/Environment/dungeon-floor-casual", 100f);
        wallSprite = CasualArtLibrary.LoadFull("Sprites/Environment/dungeon-wall-casual", 100f);
        GameObject root = new GameObject("Runtime Dungeon Layout");

        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
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
        List<Vector2> candidates = new List<Vector2>();
        List<Vector2> available = new List<Vector2>();
        float farthestDistance = 0f;
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (!floor[x, y]) continue;
            Vector2 position = CellCenter(x, y);
            float distance = Vector2.Distance(position, Entrance);
            available.Add(position);
            farthestDistance = Mathf.Max(farthestDistance, distance);
            if (distance >= minimumDistance) candidates.Add(position);
        }

        // Extra monster types may request a distance beyond the map's bounds.
        // Keep their spawns in the deeper part of the available layout instead.
        if (candidates.Count == 0)
            foreach (Vector2 position in available)
                if (Vector2.Distance(position, Entrance) >= farthestDistance * .75f)
                    candidates.Add(position);

        if (candidates.Count == 0) return Entrance;
        return candidates[Random.Range(0, candidates.Count)];
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
        var distances = ReachableDistances(ToFloorCell(Entrance));
        var candidates = new List<Vector2>();
        Vector2 fallback = Entrance;
        float bestGap = -1f;
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (distances[x, y] < 0) continue;
            Vector2 point = CellCenter(x, y);
            float entranceDistance = Vector2.Distance(point, Entrance);
            float gap = entranceDistance;
            if (occupied != null) foreach (Vector2 other in occupied) gap = Mathf.Min(gap, Vector2.Distance(point, other));
            if (gap > bestGap) { fallback = point; bestGap = gap; }
            if (entranceDistance >= minimumDistance && entranceDistance <= maximumDistance && gap >= separation) candidates.Add(point);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : fallback;
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

    public static Vector2 GetNextPathPoint(Vector2 from, Vector2 destination)
    {
        if (!GameSaveData.Finite(from) || !GameSaveData.Finite(destination)) return from;
        Vector2Int start = ToFloorCell(from);
        Vector2Int end = ToFloorCell(destination);
        if (start == end) return IsWalkablePosition(destination) ? destination : CellCenter(end.x, end.y);
        Search(start, end);
        if (searchDistances[end.x, end.y] < 0) return CellCenter(start.x, start.y);
        Vector2Int step = end;
        while (searchPrevious[step.x, step.y] != start) step = searchPrevious[step.x, step.y];
        return CellCenter(step.x, step.y);
    }

    private static void BuildFloorPlan(int savedLayoutIndex)
    {
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
        try
        {
            System.Array.Clear(floor, 0, floor.Length);
            BuildFloorPlan(index);
            var reachable = ReachableDistances(new Vector2Int(2, 1));
            int count = 0;
            foreach (int distance in reachable) if (distance >= 0) count++;
            return Mathf.Max(0, count - 2); // Entrance and special gate each reserve one cell.
        }
        finally { System.Array.Copy(previous, floor, floor.Length); layoutIndex = previousIndex; }
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
        for (int radius = 1; radius < Columns; radius++)
        for (int x = Mathf.Max(0, nearest.x - radius); x <= Mathf.Min(Columns - 1, nearest.x + radius); x++)
        for (int y = Mathf.Max(0, nearest.y - radius); y <= Mathf.Min(Rows - 1, nearest.y + radius); y++)
            if (IsFloor(x, y)) return new Vector2Int(x, y);
        return new Vector2Int(2, 1);
    }
    private static void CreateFloor(Transform parent, Vector2 position)
    {
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
