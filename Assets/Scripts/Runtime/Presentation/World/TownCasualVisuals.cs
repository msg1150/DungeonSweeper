using UnityEngine;

public static class TownCasualVisuals
{
    public static void Apply()
    {
        var art = TownPresentationConfig.Active;
        if (art != null && art.IsValid() && (!ThreeDPresentationConfig.Enabled || ThreeDPresentationConfig.SpritesEnabled))
        { ApplyArt(art, Application.isPlaying && ThreeDPresentationConfig.SpritesEnabled); return; }
        ApplyLegacy();
    }

    public static void ApplyArt(TownPresentationConfig art, bool depth)
    {
        var ground = GameObject.Find("Town Ground");
        if (ground != null)
        {
            var renderer = ground.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = art.background; renderer.color = Color.white; renderer.sortingOrder = -10;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(art.ViewWidth / art.background.bounds.size.x,
                art.ViewHeight / art.DepthScale(depth) / art.background.bounds.size.y, 1f);
        }
        var path = GameObject.Find("Main Path"); if (path != null) path.SetActive(false);
        foreach (var row in art.facilities)
        {
            var facility = GameObject.Find(row.objectName);
            if (facility == null) facility = new GameObject(row.objectName);
            var renderer = facility.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = facility.AddComponent<SpriteRenderer>();
            renderer.sprite = row.sprite; renderer.color = Color.white; renderer.sortingOrder = 1;
            float width = art.ViewWidth * row.visibleWidth;
            float scale = width / (row.sprite.bounds.size.x * row.imageWidthFraction);
            facility.transform.SetPositionAndRotation(art.ToWorld(row.feetAnchor, depth), Quaternion.identity);
            facility.transform.localScale = new Vector3(scale, scale, 1f);
            var view = facility.GetComponent<TownFacilityView>();
            if (view == null) view = facility.AddComponent<TownFacilityView>();
            view.Configure(width);
            // 예전 사각 판정을 남기면 외곽선 밖에서도 플레이어가 멈춘다.
            var oldBox = facility.GetComponent<BoxCollider2D>();
            if (oldBox != null) oldBox.enabled = false;
            var body = facility.GetComponent<PolygonCollider2D>();
            if (body == null) body = facility.AddComponent<PolygonCollider2D>();
            body.enabled = true; body.isTrigger = false; body.offset = Vector2.zero;
            var outline = new Vector2[row.footprintOutline.Length];
            Vector2 origin = art.ToWorld(row.feetAnchor, depth);
            for (int i = 0; i < outline.Length; i++) outline[i] = (art.FootprintPoint(row, row.footprintOutline[i], depth) - origin) / scale;
            body.pathCount = 1; body.SetPath(0, outline);
        }
        var boundary = GameObject.Find("Town Walkable Boundary");
        if (boundary == null) boundary = new GameObject("Town Walkable Boundary");
        var edge = boundary.GetComponent<EdgeCollider2D>();
        if (edge == null) edge = boundary.AddComponent<EdgeCollider2D>();
        var points = new Vector2[art.walkableOutline.Length + 1];
        for (int i = 0; i < art.walkableOutline.Length; i++) points[i] = art.ToWorld(art.walkableOutline[i], depth);
        points[points.Length - 1] = points[0]; edge.points = points;
        if (!Application.isPlaying && Camera.main != null)
        {
            Camera.main.orthographicSize = art.ViewHeight * .5f;
            Camera.main.rect = TownPresentationConfig.FitViewport((float)Screen.width / Mathf.Max(1, Screen.height), art.referenceAspect);
            Camera.main.aspect = art.referenceAspect;
        }
    }

    /// <summary>기존 저장 좌표가 새 건물·물가와 겹치는 경우에만 안전한 광장으로 옮긴다.</summary>
    public static Vector2 ResolvePlayerPosition(Vector2 desired, BoxCollider2D body, bool depth)
    {
        var art = TownPresentationConfig.Active;
        if (art == null) return desired;
        Vector2 size = body != null ? Vector2.Scale(body.size, body.transform.lossyScale) : Vector2.one;
        size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) + Vector2.one * .1f;
        Vector2 offset = body != null ? Vector2.Scale(body.offset, body.transform.lossyScale) : Vector2.zero;
        if (IsSafe(art, desired, size, offset, depth)) return desired;
        Vector2 spawn = art.ToWorld(art.playerSpawn, depth);
        if (IsSafe(art, spawn, size, offset, depth)) return spawn;
        // 커스텀 배치에서 광장까지 막혔으면 가까운 통행 지점을 찾는다.
        for (float radius = .5f; radius <= 10f; radius += .5f)
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            Vector2 candidate = spawn + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (IsSafe(art, candidate, size, offset, depth)) return candidate;
        }
        return spawn;
    }

    private static bool IsSafe(TownPresentationConfig art, Vector2 point, Vector2 size, Vector2 offset, bool depth)
    {
        if (!ValueValidation.Finite(point)) return false;
        var rect = new Rect(point + offset - size * .5f, size);
        if (!art.Contains(rect.min, depth) || !art.Contains(rect.max, depth)
            || !art.Contains(new Vector2(rect.xMin, rect.yMax), depth) || !art.Contains(new Vector2(rect.xMax, rect.yMin), depth)) return false;
        foreach (var row in art.facilities) if (art.OverlapsFootprint(row, rect, depth)) return false;
        return true;
    }

    private static void ApplyLegacy()
    {
        GameObject warehouse = GameObject.Find("Town Warehouse");
        if (warehouse == null)
        {
            warehouse = new GameObject("Town Warehouse");
            warehouse.transform.position = new Vector3(-.4f, -2.6f, 0);
            SpriteRenderer marker = warehouse.AddComponent<SpriteRenderer>();
            marker.sprite = CasualArtLibrary.WhiteSprite; marker.color = new Color(.2f, .5f, .45f);
            marker.sortingOrder = 1; warehouse.transform.localScale = new Vector3(1.3f, .8f, 1);
        }
        Sprite background = CasualArtLibrary.LoadFull("Sprites/Environment/town-casual", 100f);
        Sprite[] structures = CasualArtLibrary.LoadSheet("Sprites/Environment/town-structures", 2, 2, 180f, true);
        SetBackgroundCover("Town Ground", background);
        GameObject path = GameObject.Find("Main Path"); if (path != null) path.SetActive(false);
        if (structures.Length >= 4)
        {
            // 시트 상단: 길드·거래소 / 하단: 게이트·창고. LoadSheet는 하단부터 읽는다.
            SetSprite("Salvager Guild", structures[2], 4.8f, 1);
            SetSprite("Supply Shop", structures[3], 4.1f, 1);
            SetSprite("Dungeon Entrance", structures[0], 3.1f, 1);
            SetSprite("Town Warehouse", structures[1], 3.2f, 1);
        }
    }

    public static void RefreshBackground()
    {
        // 새 배치는 월드 좌표를 유지한다. 화면 비율 변경은 카메라 프레이밍만 갱신한다.
        if (TownPresentationConfig.Active != null) return;
        SetBackgroundCover("Town Ground", CasualArtLibrary.LoadFull("Sprites/Environment/town-casual", 100f));
    }

    private static void SetBackgroundCover(string objectName, Sprite sprite)
    {
        GameObject obj = GameObject.Find(objectName);
        Camera camera = Camera.main;
        if (obj == null || sprite == null || camera == null) return;
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = -10;
        float viewHeight = camera.orthographicSize * 2f;
        float viewWidth = viewHeight * camera.aspect;
        float scale = Mathf.Max(viewWidth / Mathf.Max(.01f, sprite.bounds.size.x), viewHeight / Mathf.Max(.01f, sprite.bounds.size.y));
        // 해상도 반올림이나 Game View 리사이즈에서도 가장자리가 비지 않도록 3% 여유를 둔다.
        scale *= 1.03f;
        obj.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SetSprite(string objectName, Sprite sprite, float targetWidth, int order)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null || sprite == null) return;
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = order;
        float scale = targetWidth / Mathf.Max(.01f, sprite.bounds.size.x);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
