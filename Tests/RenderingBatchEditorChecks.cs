#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>플레이 없이 메시 한도·지도 좌표·미탐색 정보와 캐시 무효화 규칙을 검사한다.</summary>
public static class RenderingBatchEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal)
            || Application.isPlaying) throw new InvalidOperationException("Rendering checks require the isolated editor outside Play mode.");
        var root = new GameObject("Batched lines check", typeof(RectTransform)); root.SetActive(false);
        try
        {
            var lines = root.AddComponent<MonsterPerceptionLines>();
            var populate = typeof(MonsterPerceptionLines).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            using var vertices = new VertexHelper();
            for (int i = 0; i < 640; i++) lines.AddSegment(Vector2.zero, Vector2.right * 10, 2, Color.red);
            populate.Invoke(lines, new object[] { vertices });
            check(lines.SegmentCount == 640 && vertices.currentVertCount == 2560 && vertices.currentIndexCount == 3840,
                "four monsters' base/hearing/hiding lines fit in one UI geometry batch");
            var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, 0);
            check(vertex.color.Equals((Color32)Color.red) && vertex.position == new Vector3(0, -1, 0),
                "line mesh preserves vertex color, alpha and screen-space thickness");
            lines.AddSegment(Vector2.zero, Vector2.zero, 2, Color.white);
            lines.AddSegment(Vector2.zero, Vector2.one, 0, Color.white);
            check(lines.SegmentCount == 640, "degenerate and zero-width lines produce no geometry");
            for (int i = 640; i <= MonsterPerceptionLines.MaximumSegments; i++) lines.AddSegment(Vector2.zero, Vector2.one, 1, Color.white);
            populate.Invoke(lines, new object[] { vertices });
            check(lines.SegmentCount == MonsterPerceptionLines.MaximumSegments && vertices.currentVertCount == 64000,
                "each line batch stays below the uGUI vertex limit");
            lines.ClearSegments(); populate.Invoke(lines, new object[] { vertices });
            check(vertices.currentVertCount == 0 && vertices.currentIndexCount == 0, "next frame clears stale line geometry");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        CheckCanvasSubmission(check);

        var map = new DungeonFloorMap { width = 4, height = 4, tiles = new int[16], hasStairsUp = true, hasStairsDown = true,
            stairsUp = new Vector2Int(0, 0), stairsDown = new Vector2Int(1, 2), hidingSpots = new List<Vector2Int> { new(3, 3) } };
        var discovered = new HashSet<Vector2Int> { new(0, 0), new(1, 2), new(2, 1) };
        int queries = 0;
        Func<int, int, bool> visible = (x, y) => { queries++; return discovered.Contains(new Vector2Int(x, y)); };
        using var cache = new DungeonMinimapTexture();
        check(cache.Refresh(map, 4, 4, discovered.Count, 20, visible), "first minimap draw uploads one cached texture");
        var texture = cache.Texture;
        check(Near(texture.GetPixel(10, 10), new Color(.4f, .65f, 1f))
            && Near(texture.GetPixel(30, 50), new Color(1f, .73f, .3f)), "minimap stairs keep colors and bottom/top orientation");
        check(Near(texture.GetPixel(50, 30), new Color(.57f, .68f, .75f))
            && texture.GetPixel(70, 70).a == 0 && texture.GetPixel(30, 30).a == 0,
                "normal discovered cells are visible while unexplored cells and hiding places remain hidden");
        queries = 0;
        check(!cache.Refresh(map, 4, 4, discovered.Count, 20, visible) && queries == 0 && cache.Texture == texture,
            "unchanged minimap performs no discovery queries or texture rebuild");
        discovered.Add(new Vector2Int(3, 3));
        check(cache.Refresh(map, 4, 4, discovered.Count, 20, visible) && cache.Texture == texture
            && Near(texture.GetPixel(70, 70), new Color(.15f, .85f, .75f)), "discovery refresh reuses the texture and reveals hiding places");
        check(cache.Refresh(map, 4, 4, discovered.Count, 30, visible) && cache.Texture.width == 120,
            "zoom refresh resizes map content independently of the HUD viewport");
        var nextFloor = map.Copy(); nextFloor.hasStairsDown = false;
        check(cache.Refresh(nextFloor, 4, 4, discovered.Count, 30, visible)
            && Near(cache.Texture.GetPixel(45, 75), new Color(.57f, .68f, .75f)),
                "floor change with the same discovery count replaces old stair markers");
        check(cache.Refresh(null, 4, 4, discovered.Count, 30, visible)
            && Near(cache.Texture.GetPixel(15, 15), new Color(.57f, .68f, .75f)), "legacy maps without a floor asset retain normal discovered cells");
        cache.Dispose(); check(cache.Texture == null, "HUD teardown releases the owned minimap texture");
        check(cache.Refresh(map, 4, 4, discovered.Count, 20, visible), "cache can be initialized again after a floor transition");
    }

    private static void CheckCanvasSubmission(Action<bool, string> check)
    {
        var owner = new GameObject("Perception HUD ownership check");
        owner.SetActive(false);
        owner.transform.position = new Vector3(100, 200, 0);
        owner.transform.localScale = new Vector3(2, 3, 1);
        Canvas canvas = null;
        try
        {
            var view = owner.AddComponent<MonsterPerceptionDebugView>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(MonsterPerceptionDebugView).GetMethod("EnsureCanvas", flags).Invoke(view, null);
            canvas = (Canvas)typeof(MonsterPerceptionDebugView).GetField("lineCanvas", flags).GetValue(view);
            check(canvas.transform.parent == null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay,
                "perception overlay is a top-level canvas independent of HUD transforms");
            var origin = new Vector2(Screen.width * .5f, Screen.height * .5f);
            typeof(MonsterPerceptionDebugView).GetMethod("AddSegment", flags).Invoke(view,
                new object[] { origin, origin + new Vector2(30, 10), 2f, Color.yellow });
            var lines = canvas.GetComponentInChildren<MonsterPerceptionLines>();
            Canvas.ForceUpdateCanvases();
            lines.SetAllDirty(); lines.Rebuild(CanvasUpdate.PreRender);
            var mesh = lines.canvasRenderer.GetMesh();
            check(mesh != null && mesh.vertexCount == 4 && mesh.triangles.Length == 6 && lines.canvasRenderer.materialCount == 1
                && lines.canvasRenderer.GetMaterial(0) != null && !lines.raycastTarget,
                "normal Graphic rebuild submits the debug line mesh and material to CanvasRenderer without blocking input");
            // 일반 MonoBehaviour의 Play 수명 콜백을 Edit 모드에서 자동 실행했다고 가정하지 않는다.
            typeof(MonsterPerceptionDebugView).GetMethod("OnDestroy", flags).Invoke(view, null);
            check(canvas == null, "HUD teardown handler releases its separate top-level debug canvas");
        }
        finally
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static bool Near(Color actual, Color expected) => Mathf.Abs(actual.r - expected.r) < .006f
        && Mathf.Abs(actual.g - expected.g) < .006f && Mathf.Abs(actual.b - expected.b) < .006f && Mathf.Abs(actual.a - expected.a) < .006f;
}
#endif
