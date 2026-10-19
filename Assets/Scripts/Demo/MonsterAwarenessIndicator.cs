using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>AI가 제공하는 발견/목격 지점 추적 상태를 머리 위에 표시한다. 인식 판정은 하지 않는다.</summary>
[DefaultExecutionOrder(800)]
[DisallowMultipleComponent]
public sealed class MonsterAwarenessIndicator : MonoBehaviour, IDungeonPoolResettable
{
    private static Mesh detectedMesh, searchingMesh;
    private static Material material;
    private EnemyAgent owner;
    private SpriteDepthView spriteView;
    private SpriteRenderer source;
    private ThreeDActorView modelView;
    private GameObject boundModel;
    private Transform modelHead;
    private GameObject marker;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MonsterAwareness displayed;
    private float popTime;

    public static void Ensure(EnemyAgent enemy)
    {
        var view = enemy.GetComponent<MonsterAwarenessIndicator>();
        if (view == null) view = enemy.gameObject.AddComponent<MonsterAwarenessIndicator>();
        view.owner = enemy;
        view.source = enemy.GetComponentInChildren<SpriteRenderer>();
        view.ResetForPool();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetResources()
    {
        if (detectedMesh != null) Destroy(detectedMesh);
        if (searchingMesh != null) Destroy(searchingMesh);
        if (material != null) Destroy(material);
        detectedMesh = searchingMesh = null; material = null;
    }

    public void ResetForPool()
    {
        displayed = MonsterAwareness.None; popTime = 0f;
        if (marker != null) marker.SetActive(false);
    }
    private void OnDisable() => ResetForPool();
    private void OnDestroy() { if (marker != null) Destroy(marker); }

    private void LateUpdate()
    {
        var run = DungeonRunController.Instance;
        var state = owner != null && run != null && run.IsRunActive ? owner.Awareness : MonsterAwareness.None;
        if (state == MonsterAwareness.None) { ResetForPool(); return; }
        var world = ThreeDWorldView.Current;
        var camera = world != null ? world.WorldCamera : Camera.main;
        if (camera == null || !EnsureMarker()) return;
        if (displayed != state)
        {
            displayed = state; popTime = 0f;
            if (state == MonsterAwareness.Detected)
            {
                if (detectedMesh == null) detectedMesh = BuildMesh(false);
                meshFilter.sharedMesh = detectedMesh;
            }
            else
            {
                if (searchingMesh == null) searchingMesh = BuildMesh(true);
                meshFilter.sharedMesh = searchingMesh;
            }
        }
        marker.SetActive(true);
        marker.layer = world != null ? ThreeDWorldView.RenderLayer : gameObject.layer;
        marker.transform.position = HeadPosition(world) + camera.transform.up * .38f;
        marker.transform.rotation = camera.transform.rotation;
        popTime = Mathf.Min(.2f, popTime + (GameShell.IsGameplayInputBlocked ? 0f : Time.deltaTime));
        float pop = 1f + .2f * Mathf.Sin(popTime / .2f * Mathf.PI);
        marker.transform.localScale = Vector3.one * (.58f * pop);
        meshRenderer.sortingOrder = 1020 - Mathf.RoundToInt(transform.position.y * 20f);
    }

    private Vector3 HeadPosition(ThreeDWorldView world)
    {
        if (world != null && ThreeDPresentationConfig.SpritesEnabled)
        {
            if (spriteView == null) spriteView = GetComponent<SpriteDepthView>();
            if (spriteView != null && spriteView.Display != null) return spriteView.HeadPosition;
        }
        else if (world != null)
        {
            if (modelView == null) modelView = GetComponent<ThreeDActorView>();
            if (modelView != null && modelView.Model != null && boundModel != modelView.Model)
            {
                boundModel = modelView.Model; modelHead = null;
                foreach (var child in boundModel.GetComponentsInChildren<Transform>())
                    if (child.name == "FaceOrigin") { modelHead = child; break; }
            }
            return modelHead != null ? modelHead.position + Vector3.up * .3f
                : ThreeDWorldView.Map(transform.position, world.FloorHeight(transform.position) + 1.5f);
        }
        return source != null ? new Vector3(source.bounds.center.x, source.bounds.max.y, transform.position.z)
            : transform.position + Vector3.up * 1.3f;
    }

    private bool EnsureMarker()
    {
        if (marker != null) return true;
        if (material == null)
        {
            var shader = Resources.Load<Shader>("MonsterAwareness");
            if (shader == null) return false;
            material = new Material(shader) { name = "Shared monster awareness", hideFlags = HideFlags.HideAndDontSave };
        }
        // 논리 배우와 같은 수명으로 보관한다. 풀 반환 때 숨기고 다음 대여에 재사용한다.
        marker = new GameObject("Monster awareness ! / ?");
        marker.transform.SetParent(transform.parent, false);
        meshFilter = marker.AddComponent<MeshFilter>();
        meshRenderer = marker.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return true;
    }

    private static Mesh BuildMesh(bool question)
    {
        // 작은 벡터 도형 두 개를 공유한다. 폰트·텍스처 로딩이나 개체별 메시 생성을 피한다.
        var vertices = new List<Vector3>(); var colors = new List<Color32>(); var triangles = new List<int>();
        void Disk(Vector2 center, float radius, Color32 color, int segments = 24)
        {
            int start = vertices.Count;
            vertices.Add(center); colors.Add(color);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius); colors.Add(color);
                triangles.Add(start); triangles.Add(start + 1 + i); triangles.Add(start + 1 + (i + 1) % segments);
            }
        }
        Disk(Vector2.zero, .5f, new Color32(255, 245, 210, 255));
        Disk(Vector2.zero, .45f, new Color32(20, 32, 34, 255));
        Color32 ink = question ? new Color32(255, 214, 83, 255) : new Color32(255, 90, 78, 255);
        Vector2[] points = question
            ? new[] { new Vector2(-.16f,.17f), new Vector2(-.13f,.27f), new Vector2(0,.31f), new Vector2(.14f,.25f),
                new Vector2(.16f,.13f), new Vector2(.08f,.03f), new Vector2(0,-.03f), new Vector2(0,-.10f) }
            : new[] { new Vector2(0,.30f), new Vector2(0,-.10f) };
        const float halfWidth = .052f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 a = points[i - 1], b = points[i], direction = (b - a).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            int start = vertices.Count;
            foreach (var point in new[] { a - side, a + side, b + side, b - side }) { vertices.Add(point); colors.Add(ink); }
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        foreach (var point in points) Disk(point, halfWidth, ink, 8);
        Disk(new Vector2(0,-.28f), .065f, ink, 12);
        var mesh = new Mesh { name = question ? "Monster question mark" : "Monster exclamation mark" };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds(); mesh.UploadMeshData(true);
        return mesh;
    }
}
