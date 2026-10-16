using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// XY 게임 로직과 XZ 입체 표시를 분리한다. 저장·경로·2D 충돌은 기존 좌표를 유지한다.
/// 모델에는 3D 충돌체를 붙이지 않아 논리 충돌이나 시선 Raycast에 영향을 주지 않는다.
/// </summary>
[DefaultExecutionOrder(500)]
public sealed class ThreeDWorldView : MonoBehaviour
{
    public const int RenderLayer = 30;
    public static ThreeDWorldView Current { get; private set; }
    private Camera camera;
    private Transform player;
    private bool dungeon, overview, previewCamera;
    private Light lantern;
    private readonly List<Material> ownedMaterials = new();
    private readonly List<Mesh> ownedMeshes = new();
    private readonly Dictionary<Sprite, float> footLifts = new();
    private Sprite contactShadow;
    private Texture2D shadowTexture;
    private SpriteRenderer townGround;
    private int viewportWidth, viewportHeight;
    public Material SpriteMaterial { get; private set; }
    public Sprite ContactShadow => contactShadow;
    public bool IsDungeon => dungeon;
    public Camera WorldCamera => camera;
    public static Vector3 Map(Vector2 point, float height = 0) => new(point.x, height, point.y);
    public float FloorHeight(Vector2 point)
    {
        var map = dungeon ? DungeonLayoutFactory.ActiveMap : null;
        if (map == null) return 0;
        if (map.hasStairsUp && OnSteps(point, map.Center(map.stairsUp), out float up)) return up;
        if (map.hasStairsDown && OnSteps(point, map.Center(map.stairsDown), out float down)) return down;
        return 0;
    }
    private static bool OnSteps(Vector2 point, Vector2 center, out float height)
    {
        Vector2 offset = point - center;
        height = .06f + Mathf.Clamp(Mathf.FloorToInt((offset.y + .45f) / .15f), 0, 5) * .065f;
        return Mathf.Abs(offset.x) < .4f && Mathf.Abs(offset.y) < .45f;
    }
    public static Vector3 Project(Vector2 point, float height = 0) => Current != null ? Map(point, height) : new Vector3(point.x, point.y, 0);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => Current = null;

    public static void Create(Transform target, bool isDungeon)
    {
        if (!ThreeDPresentationConfig.Enabled || Current != null) return;
        var view = new GameObject(ThreeDPresentationConfig.SpritesEnabled ? "2.5D World Presentation" : "3D World Presentation").AddComponent<ThreeDWorldView>();
        Current = view; view.player = target; view.dungeon = isDungeon; view.Build();
    }
    public static void SetRenderLayer(GameObject obj)
    {
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = RenderLayer;
    }
    private void Build()
    {
        camera = Camera.main;
        if (camera == null) return;
        camera.GetUniversalAdditionalCameraData().SetRenderer(ThreeDPresentationConfig.Active.forwardRendererIndex);
        camera.cullingMask = 1 << RenderLayer;
        camera.orthographic = ThreeDPresentationConfig.SpritesEnabled;
        camera.orthographicSize = dungeon ? ThreeDPresentationConfig.Active.dungeonViewSize : ThreeDPresentationConfig.Active.townViewSize;
        camera.fieldOfView = ThreeDPresentationConfig.Active.cameraFieldOfView;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = dungeon ? new Color(.035f,.055f,.075f) : new Color(.57f,.75f,.78f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = dungeon ? new Color(.27f,.32f,.39f) : new Color(.67f,.70f,.64f);
        var sun = new GameObject("3D Sun"); sun.transform.SetParent(transform);
        sun.transform.rotation = Quaternion.Euler(55,-30,0);
        var light = sun.AddComponent<Light>(); light.type = LightType.Directional;
        light.color = dungeon ? new Color(.65f,.77f,.91f) : new Color(1f,.92f,.77f);
        light.intensity = dungeon ? .48f : 1.15f; light.shadows = LightShadows.Soft;
        light.shadowBias = .035f; light.shadowNormalBias = .25f;
        if (ThreeDPresentationConfig.SpritesEnabled) BuildSpriteResources();
        if (dungeon) BuildDungeon(); else if (ThreeDPresentationConfig.SpritesEnabled) BuildIllustratedTown(); else BuildTown();
        ThreeDActorView.Attach(player.gameObject, "cat");
        if (dungeon)
        {
            foreach (EnemyAgent actor in FindObjectsByType<EnemyAgent>())
                ThreeDActorView.Attach(actor.gameObject, Kind(actor.PrefabId), false, actor.GetComponent<MonsterPrefab>()?.model3D);
            foreach (CorpsePrefab actor in FindObjectsByType<CorpsePrefab>())
                ThreeDActorView.Attach(actor.gameObject, Kind(actor.prefabId), true, actor.model3D);
            var lamp = new GameObject("Player lantern"); lamp.transform.SetParent(transform);
            lantern = lamp.AddComponent<Light>(); lantern.type = LightType.Point;
            lantern.color = new Color(.83f,.88f,.78f); lantern.range = 7; lantern.intensity = 1.9f;
            lantern.shadows = LightShadows.None;
        }
        UpdateCamera(true);
        FitTownGround();
    }
    private void BuildSpriteResources()
    {
        // Resources 재질 참조로 일반 빌드에서도 셰이더가 누락되지 않게 한다.
        SpriteMaterial = Resources.Load<Material>("Sprite25D");
        shadowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Soft contact shadow", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float radius = new Vector2((x + .5f) / 16f - 1, (y + .5f) / 16f - 1).magnitude;
            pixels[y * 32 + x] = new Color(1, 1, 1, 1 - Mathf.SmoothStep(.15f, 1, radius));
        }
        shadowTexture.SetPixels(pixels); shadowTexture.Apply(false, true);
        contactShadow = Sprite.Create(shadowTexture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
    }
    public float FootLift(Sprite sprite)
    {
        if (sprite == null) return 0;
        if (footLifts.TryGetValue(sprite, out float value)) return value;
        // 초기 자세마다 한 번만 읽는다. 런타임 시트의 CPU 버퍼를 다시 유지하지 않는다.
        Texture2D readable = CasualArtLibrary.CreateReadableCopy(sprite.texture);
        try
        {
            Rect rect = sprite.rect;
            var pixels = readable.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            int width = (int)rect.width, bottom = 0;
            bool found = false;
            for (int y = 0; y < (int)rect.height && !found; y++)
                for (int x = width / 5; x < width * 4 / 5; x++)
                    if (pixels[y * width + x].a > .2f) { bottom = y; found = true; break; }
            value = (sprite.pivot.y - bottom) / sprite.pixelsPerUnit;
            footLifts[sprite] = value; return value;
        }
        finally { Destroy(readable); }
    }
    private void BuildIllustratedTown()
    {
        var groundObject = new GameObject("Illustrated town ground"); groundObject.transform.SetParent(transform, false);
        groundObject.layer = RenderLayer;
        townGround = groundObject.AddComponent<SpriteRenderer>();
        townGround.sprite = CasualArtLibrary.LoadFull("Sprites/Environment/town-casual", 100f);
        townGround.sharedMaterial = SpriteMaterial; townGround.sortingOrder = -10;
        groundObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        groundObject.transform.position = new Vector3(0, -.03f, 0);
        foreach (string name in new[] { "Salvager Guild", "Supply Shop", "Town Warehouse", "Dungeon Entrance" })
        {
            var facility = GameObject.Find(name);
            if (facility != null) SpriteDepthView.Attach(facility);
        }
        // 시설 앞에서 상호작용하며, 그림의 몸통을 통과하지 않는다.
        var obstacles = new GameObject("Town 2.5D Logic Footprints"); obstacles.transform.SetParent(transform);
        Obstacle(obstacles.transform, new Vector2(-4.3f, 2.1f), new Vector2(2.5f, 1.2f));
        Obstacle(obstacles.transform, new Vector2(.2f, 2.9f), new Vector2(2.3f, 1.0f));
        Obstacle(obstacles.transform, new Vector2(-.4f, -2.0f), new Vector2(2.4f, 1.0f));
        Obstacle(obstacles.transform, new Vector2(-11.5f,0), new Vector2(.3f,17));
        Obstacle(obstacles.transform, new Vector2(11.5f,0), new Vector2(.3f,17));
        Obstacle(obstacles.transform, new Vector2(0,-8.5f), new Vector2(23,.3f));
        Obstacle(obstacles.transform, new Vector2(0,8.5f), new Vector2(23,.3f));
    }
    private void FitTownGround()
    {
        if (townGround == null || townGround.sprite == null || camera == null) return;
        float height = camera.orthographicSize * 2;
        Vector3 size = townGround.sprite.bounds.size;
        float scale = Mathf.Max(height * camera.aspect / size.x, height / size.y) * 1.03f;
        // 배경 그림의 화면 비율을 보존하되 지면으로 눕혀 배우와 앞뒤 공간을 공유한다.
        float tilt = Mathf.Sin(ThreeDPresentationConfig.Active.spriteCameraElevation * Mathf.Deg2Rad);
        townGround.transform.localScale = new Vector3(scale, scale / tilt, 1);
        viewportWidth = Screen.width; viewportHeight = Screen.height;
    }
    private static string Kind(string id) => id != null && id.EndsWith("slime") ? "slime" : id != null && id.EndsWith("orc") ? "orc" : "goblin";
    private void BuildTown()
    {
        var town = Instantiate(ThreeDPresentationConfig.Active.town, transform);
        town.name = "Blender Town"; SetRenderLayer(town);
        // FBX 루트의 축 변환을 보존한다. 이를 덮어쓰면 Z-up 메시가 눕는다.
        town.transform.rotation = Quaternion.Euler(0,180,0) * town.transform.rotation;
        var obstacles = new GameObject("Town 3D Logic Footprints"); obstacles.transform.SetParent(transform);
        Obstacle(obstacles.transform, new Vector2(-4.3f,1.5f), new Vector2(2.5f,1.8f));
        Obstacle(obstacles.transform, new Vector2(.2f,2.3f), new Vector2(2.3f,1.15f));
        Obstacle(obstacles.transform, new Vector2(-.4f,-2.6f), new Vector2(2.4f,1.5f));
        // Preserve a gap for gate interaction and stop walking off the island.
        Obstacle(obstacles.transform, new Vector2(-11.5f,0), new Vector2(.3f,17));
        Obstacle(obstacles.transform, new Vector2(11.5f,0), new Vector2(.3f,17));
        Obstacle(obstacles.transform, new Vector2(0,-8.5f), new Vector2(23,.3f));
        Obstacle(obstacles.transform, new Vector2(0,8.5f), new Vector2(23,.3f));
    }
    private static void Obstacle(Transform parent, Vector2 point, Vector2 size)
    {
        var obj = new GameObject("Footprint"); obj.transform.SetParent(parent);
        obj.transform.position = point; obj.AddComponent<BoxCollider2D>().size = size;
    }
    private Material Surface(string name, Color color, string texture = null)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .08f);
        if (texture != null) material.SetTexture("_BaseMap", Resources.Load<Texture2D>(texture));
        ownedMaterials.Add(material); return material;
    }
    private void BuildDungeon()
    {
        var floor = Surface("Dungeon Slate Floor", ThreeDPresentationConfig.SpritesEnabled ? new Color(.72f,.78f,.79f) : new Color(.25f,.31f,.34f),
            ThreeDPresentationConfig.SpritesEnabled ? "Sprites/Environment/dungeon-floor-casual" : null);
        var stone = Surface("Dungeon Wall Stone", ThreeDPresentationConfig.SpritesEnabled ? new Color(.63f,.70f,.72f) : new Color(.30f,.36f,.39f),
            ThreeDPresentationConfig.SpritesEnabled ? "Sprites/Environment/dungeon-wall-casual" : null);
        var trim = Surface("3D Wall Cap", new Color(.39f,.44f,.44f));
        var floors = new List<CombineInstance>(); var walls = new List<CombineInstance>(); var caps = new List<CombineInstance>();
        const float cell = 1.45f;
        Mesh cube = CubeMesh();
        for (int x = 0; x < DungeonLayoutFactory.Width; x++)
        for (int y = 0; y < DungeonLayoutFactory.Height; y++)
        {
            if (DungeonLayoutFactory.IsSolidWallCell(x, y))
            {
                float blockHeight = DungeonLayoutFactory.IsWalkableCell(x, y + 1) ? .45f : 1.25f;
                Vector2 center = DungeonLayoutFactory.CellCenter(x, y);
                AddBox(walls, cube, Map(center, blockHeight * .5f), new Vector3(cell, blockHeight, cell));
                AddBox(caps, cube, Map(center, blockHeight + .045f), new Vector3(cell, .09f, cell));
            }
            if (!DungeonLayoutFactory.IsWalkableCell(x,y)) continue;
            Vector2 p = DungeonLayoutFactory.CellCenter(x,y);
            AddBox(floors,cube,Map(p,-.12f),new Vector3(cell-.025f,.24f,cell-.025f));
            foreach (Vector2Int direction in new[] { Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down })
            {
                if (DungeonLayoutFactory.IsWalkableCell(x+direction.x,y+direction.y)) continue;
                Vector2 wall = p+(Vector2)direction*cell*.5f;
                // Low south walls keep the player and corpses visible; north walls retain height.
                float height = direction.y < 0 ? .45f : 1.25f;
                Vector3 size = direction.x == 0 ? new Vector3(cell+.15f,height,.22f) : new Vector3(.22f,height,cell+.15f);
                AddBox(walls,cube,Map(wall,height*.5f),size);
                size.y=.09f; AddBox(caps,cube,Map(wall,height+.045f),size);
            }
        }
        Combine("Dungeon 3D Floor",floors,floor); Combine("Dungeon 3D Walls",walls,stone); Combine("Dungeon 3D Caps",caps,trim);
        var steps = new List<CombineInstance>();
        foreach (var stair in FindObjectsByType<DungeonStairMarker>())
        {
            Vector2 point = stair.transform.position;
            for (int i = 0; i < 6; i++)
            {
                float height = .06f + i * .065f;
                AddBox(steps, cube, Map(point + new Vector2(0, (i - 2.5f) * .15f), height * .5f), new Vector3(.8f, height, .15f));
            }
            var indicator = new GameObject(stair.direction > 0 ? "Down stair glow" : "Up stair glow"); indicator.transform.SetParent(transform);
            indicator.transform.position = Map(point, .65f);
            var lamp = indicator.AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 2.4f; lamp.intensity = 1.1f;
            lamp.color = stair.direction > 0 ? new Color(.28f,.9f,.65f) : new Color(.45f,.65f,1);
        }
        Combine("Dungeon Stairs", steps, trim);
        foreach (string name in new[] { "입구", "특수 탈출 게이트" })
        {
            var marker = GameObject.Find(name); if (marker == null) continue;
            GameObject gate;
            if (ThreeDPresentationConfig.SpritesEnabled)
                gate = SpriteDepthView.Attach(marker).Display;
            else gate = Instantiate(ThreeDPresentationConfig.Active.portal,transform);
            gate.name = name + (ThreeDPresentationConfig.SpritesEnabled ? " 2.5D" : " 3D"); gate.transform.position = Map(marker.transform.position);
            if (!ThreeDPresentationConfig.SpritesEnabled) gate.transform.rotation = Quaternion.Euler(0,180,0) * gate.transform.rotation;
            SetRenderLayer(gate);
            var glow = new GameObject("Gate glow"); glow.transform.SetParent(gate.transform);
            glow.transform.position = Map(marker.transform.position,1.25f);
            var lamp = glow.AddComponent<Light>(); lamp.type=LightType.Point; lamp.range=4;lamp.intensity=2;
            lamp.color=new Color(.28f,.9f,.65f);lamp.shadows=LightShadows.None;
        }
    }
    private Mesh CubeMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh; Destroy(temp); return mesh;
    }
    private static void AddBox(List<CombineInstance> pieces,Mesh mesh,Vector3 center,Vector3 size)
        => pieces.Add(new CombineInstance { mesh=mesh,transform=Matrix4x4.TRS(center,Quaternion.identity,size) });
    private void Combine(string name,List<CombineInstance> parts,Material material)
    {
        if (parts.Count==0)return;
        var mesh = new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
        mesh.CombineMeshes(parts.ToArray(),true,true); mesh.UploadMeshData(true); ownedMeshes.Add(mesh);
        var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.layer=RenderLayer;
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;
    }
    public void SetOverview(bool value) { overview=value; UpdateCamera(true); }
    public void PreviewCamera(bool value) { previewCamera=value; if(!value) UpdateCamera(true); }
    private void LateUpdate()
    {
        if (!previewCamera) UpdateCamera(false);
        if (townGround != null && (viewportWidth != Screen.width || viewportHeight != Screen.height)) FitTownGround();
        if (lantern != null && player != null) lantern.transform.position=Map(player.position,1.8f);
    }
    private void UpdateCamera(bool immediate)
    {
        if (camera==null || player==null)return;
        if (ThreeDPresentationConfig.SpritesEnabled)
        {
            Vector3 center = dungeon && !overview ? Map(player.position, .35f) : new Vector3(0, .35f, 0);
            Quaternion rotation = Quaternion.Euler(ThreeDPresentationConfig.Active.spriteCameraElevation, 0, 0);
            Vector3 desiredPosition = center - rotation * Vector3.forward * 35;
            camera.transform.position = immediate ? desiredPosition : Vector3.Lerp(camera.transform.position, desiredPosition, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
            camera.transform.rotation = rotation;
            float overviewSize = Mathf.Max(11, DungeonLayoutFactory.Width * 1.45f / Mathf.Max(.1f,camera.aspect) * .5f + 1,
                DungeonLayoutFactory.Height * 1.45f * Mathf.Sin(ThreeDPresentationConfig.Active.spriteCameraElevation * Mathf.Deg2Rad) * .5f + 1);
            camera.orthographicSize = overview ? overviewSize : dungeon ? ThreeDPresentationConfig.Active.dungeonViewSize : ThreeDPresentationConfig.Active.townViewSize;
            return;
        }
        Vector3 focus=dungeon && !overview ? Map(player.position,.4f) : new Vector3(0,.3f,0);
        Vector3 offset= dungeon && !overview ? new Vector3(0,9,-7.5f) : new Vector3(0,14,-11);
        if (overview) offset=new Vector3(0,28,-19);
        Vector3 desired=focus+offset;
        camera.transform.position=immediate ? desired : Vector3.Lerp(camera.transform.position,desired,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
        camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position);
    }
    private void OnDestroy()
    {
        if (Current==this) Current=null;
        foreach (var item in ownedMaterials) if(item!=null)Destroy(item);
        foreach (var item in ownedMeshes) if(item!=null)Destroy(item);
        if (contactShadow != null) Destroy(contactShadow);
        if (shadowTexture != null) Destroy(shadowTexture);
    }
}
