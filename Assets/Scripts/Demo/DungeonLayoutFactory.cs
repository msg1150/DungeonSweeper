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

    public static Vector2 Entrance => CellCenter(2, 1);
    public static int Width => Columns;
    public static int Height => Rows;
    public static string LayoutName => new[] { "갈림길 저장고", "고리형 회랑", "계단식 묘실" }[layoutIndex];

    public static void CreateLayout()
    {
        if (GameObject.Find("Runtime Dungeon Layout") != null) return;
        System.Array.Clear(floor, 0, floor.Length);
        BuildFloorPlan();
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
        List<Vector2> candidates = new List<Vector2>();
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
            if (floor[x, y] && Vector2.Distance(CellCenter(x, y), Entrance) > 16f) candidates.Add(CellCenter(x, y));
        return candidates[Random.Range(0, candidates.Count)];
    }

    public static Vector2 RandomFloorPosition(float minimumDistance)
    {
        List<Vector2> candidates = new List<Vector2>();
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
            if (floor[x, y] && Vector2.Distance(CellCenter(x, y), Entrance) >= minimumDistance) candidates.Add(CellCenter(x, y));
        return candidates[Random.Range(0, candidates.Count)];
    }

    public static Vector2 RandomRoamPosition(Vector2 origin)
    {
        List<Vector2> candidates = new List<Vector2>();
        for (int x = 0; x < Columns; x++)
        for (int y = 0; y < Rows; y++)
        {
            if (!floor[x, y]) continue;
            float distance = Vector2.Distance(CellCenter(x, y), origin);
            if (distance >= 3f && distance <= 13f) candidates.Add(CellCenter(x, y));
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : RandomFloorPosition(0f);
    }

    public static void RandomPatrolPoints(float minimumDistance, out Vector2 pointA, out Vector2 pointB)
    {
        pointA = RandomFloorPosition(minimumDistance);
        Vector2Int cell = ToFloorCell(pointA);
        List<Vector2Int> neighbours = new List<Vector2Int>(Neighbours(cell));
        if (neighbours.Count == 0) { pointB = pointA; return; }
        Vector2Int next = neighbours[Random.Range(0, neighbours.Count)];
        pointB = CellCenter(next.x, next.y);
    }

    public static Vector2Int WorldToCell(Vector2 position) => ToFloorCell(position);
    public static bool IsWalkableCell(int x, int y) => IsFloor(x, y);

    public static Vector2 CellCenter(int x, int y) => new Vector2((x - (Columns - 1) * .5f) * CellSize, (y - (Rows - 1) * .5f) * CellSize);

    public static Vector2 GetNextPathPoint(Vector2 from, Vector2 destination)
    {
        Vector2Int start = ToFloorCell(from);
        Vector2Int end = ToFloorCell(destination);
        if (start == end) return destination;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == end) break;
            foreach (Vector2Int next in Neighbours(current))
                if (!previous.ContainsKey(next)) { previous[next] = current; queue.Enqueue(next); }
        }
        if (!previous.ContainsKey(end)) return CellCenter(start.x, start.y);
        Vector2Int step = end;
        while (previous[step] != start) step = previous[step];
        return CellCenter(step.x, step.y);
    }

    private static void BuildFloorPlan()
    {
        layoutIndex = Random.Range(0, 3);
        AddRoom(0, 0, 4, 3); // 모든 템플릿은 같은 시작 방을 공유한다.
        if (layoutIndex == 0) BuildForkVault();
        else if (layoutIndex == 1) BuildRingCorridor();
        else BuildSteppedCrypt();
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
    private static IEnumerable<Vector2Int> Neighbours(Vector2Int cell)
    {
        if (IsFloor(cell.x - 1, cell.y)) yield return cell + Vector2Int.left;
        if (IsFloor(cell.x + 1, cell.y)) yield return cell + Vector2Int.right;
        if (IsFloor(cell.x, cell.y - 1)) yield return cell + Vector2Int.down;
        if (IsFloor(cell.x, cell.y + 1)) yield return cell + Vector2Int.up;
    }
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
        renderer.sprite = RuntimeSprite.Value;
        renderer.color = new Color(.38f, .43f, .48f, 1f);
        renderer.sortingOrder = 0;
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
        renderer.sprite = RuntimeSprite.Value;
        renderer.color = new Color(.13f, .18f, .24f, 1f);
        renderer.sortingOrder = 1;
    }
    private static class RuntimeSprite
    {
        public static readonly Sprite Value = Create();
        private static Sprite Create()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
    }
}
