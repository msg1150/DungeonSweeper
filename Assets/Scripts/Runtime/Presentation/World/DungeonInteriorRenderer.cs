using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 원화의 상판·벽돌 면을 논리 충돌 크기의 입체 표면에 입힌다.
/// 16×16칸/텍스처 단위 메시로 합쳐 타일마다 렌더러·재질을 만들지 않는다.
/// </summary>
public sealed class DungeonInteriorRenderer : MonoBehaviour
{
    private const int ChunkSize = 16;
    private const float Cell = 1.45f, Border = .18f;
    private const float StairDepth = .06f;
    private readonly List<Mesh> meshes = new();
    private readonly List<Material> materials = new();
    private readonly Dictionary<Texture2D, Material> terrainMaterials = new(), propMaterials = new();
    private readonly Dictionary<Vector2Int, MeshRenderer> hideouts = new();
    private DungeonInteriorArt art;
    private DungeonFloorMap map;
    private Material openMaterial, closedMaterial;
    public int TerrainBatchCount { get; private set; }

    public void Build(DungeonInteriorArt artwork, DungeonFloorMap floorMap, Vector2? entrance = null, Vector2? exit = null)
    {
        if (art != null) throw new System.InvalidOperationException("Dungeon artwork is already built.");
        if (artwork == null || !artwork.IsValid() || floorMap == null || !floorMap.IsValid(out _))
            throw new System.ArgumentException("Invalid dungeon artwork or floor map.");
        art = artwork; map = floorMap;
        for (int cx = 0; cx < map.width; cx += ChunkSize)
        for (int cy = 0; cy < map.height; cy += ChunkSize)
        {
            var batches = new Dictionary<Texture2D, Geometry>();
            for (int x = cx; x < Mathf.Min(cx+ChunkSize,map.width); x++)
            for (int y = cy; y < Mathf.Min(cy+ChunkSize,map.height); y++)
            {
                var cell = new Vector2Int(x,y); Vector2 center = map.Center(cell);
                if (Solid(x,y))
                {
                    int neighbors = (Solid(x-1,y)?1:0) | (Solid(x+1,y)?2:0) | (Solid(x,y-1)?4:0) | (Solid(x,y+1)?8:0);
                    Box(batches,center,new Vector2(Cell,Cell),WallHeight(x,y),art.WallTop(neighbors));
                }
                if (!Floor(x,y)) continue;
                FloorTile(batches,cell,center);
                // DungeonLayoutFactory의 경계 BoxCollider2D와 같은 중심·폭이다.
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int delta = Directions[d];
                    if (Floor(x+delta.x,y+delta.y)) continue;
                    Vector2 size = delta.x == 0 ? new Vector2(Cell+Border,Border) : new Vector2(Border,Cell+Border);
                    float height = Solid(x+delta.x,y+delta.y) ? WallHeight(x+delta.x,y+delta.y)+.002f : delta.y < 0 ? .38f : .95f;
                    Box(batches,center+(Vector2)delta*Cell*.5f,size,height,delta.x==0?art.horizontalTop:art.verticalTop);
                }
            }
            foreach (var pair in batches)
            {
                Emit($"Interior terrain {cx/ChunkSize}-{cy/ChunkSize} {pair.Key.name}",pair.Value,MaterialFor(pair.Key,false));
                TerrainBatchCount++;
            }
        }
        var glows = new Geometry();
        if (entrance.HasValue) { Standing("입구 원화",entrance.Value,art.portal,art.portalWidth,Color.white); Glow(glows,entrance.Value); }
        if (exit.HasValue) { Standing("특수 탈출 포탈 원화",exit.Value,art.portal,art.portalWidth,new Color(.9f,1f,.95f)); Glow(glows,exit.Value); }
        if (glows.vertices.Count>0) Emit("Portal floor glow",glows,MaterialFor(Texture2D.whiteTexture,true)).sortingOrder=-10;
        if (map.hasStairsDown) Stair(map.stairsDown,false);
        if (map.hasStairsUp) Stair(map.stairsUp,true);
        openMaterial = MaterialFor(art.hideoutOpen.texture,true);
        closedMaterial = MaterialFor(art.hideoutClosed.texture,true);
        if (map.hidingSpots != null) foreach (var cell in map.hidingSpots)
            hideouts.Add(cell,Standing("은신처 원화",map.Center(cell),art.hideoutOpen,art.hideoutWidth,Color.white));
    }

    private static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
    private bool Floor(int x,int y) => map.Tile(x,y)==DungeonTile.Floor;
    private bool Solid(int x,int y) => map.Tile(x,y)==DungeonTile.Wall;
    private float WallHeight(int x,int y) => Floor(x,y+1) ? .38f : .95f;

    public void SetHiding(Vector2Int cell, bool hidden)
    {
        if (!hideouts.TryGetValue(cell,out var renderer)) return;
        renderer.sharedMaterial = hidden ? closedMaterial : openMaterial;
        // 열림/닫힘 그림의 투명 여백이 달라도 발판과 월드 크기는 유지한다.
        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        mesh.uv = Uvs(hidden ? art.hideoutClosed.uv : art.hideoutOpen.uv);
    }

    private Material MaterialFor(Texture2D texture,bool prop)
    {
        var cache = prop ? propMaterials : terrainMaterials;
        if (cache.TryGetValue(texture,out var material)) return material;
        material = new Material(art.surfaceShader) { name="Interior "+texture.name };
        material.SetTexture("_MainTex",texture); material.SetColor("_Color",Color.white);
        material.SetFloat("_ZWrite",prop?0:1); material.renderQueue = prop ? 3000 : 2000;
        cache.Add(texture,material); materials.Add(material); return material;
    }

    private void Stair(Vector2Int cell,bool up)
    {
        // 솟은 액자 전체를 바닥 위에 올리지 않는다. 타일에 낸 정사각 입구 아래에 계단 내부만 표시한다.
        float width = art.stairWidth;
        var shape = new Geometry();
        Ground(shape,map.Center(cell),new Vector2(width,width),-StairDepth,art.stairs.uv,up?new Color(.84f,.94f,1f):Color.white);
        Emit(up?"위층 계단 원화":"아래층 계단 원화",shape,MaterialFor(art.stairs.texture,false));
    }

    private bool IsStair(Vector2Int cell) => (map.hasStairsDown && cell==map.stairsDown) || (map.hasStairsUp && cell==map.stairsUp);

    private void FloorTile(Dictionary<Texture2D,Geometry> batches,Vector2Int cell,Vector2 center)
    {
        bool stair=IsStair(cell);
        // 계단 주위는 주변 일반 바닥과 같은 텍스처·위상으로 이어 붙인다.
        var texture=map.Reserved(cell) && !stair ? art.cleanFloor : art.dustyFloor;
        var floor=Batch(batches,texture);
        float half=Cell*.5f;
        if (!stair) { FloorPatch(floor,cell,center,new Rect(-half,-half,Cell,Cell));return; }
        float opening=art.stairWidth*.5f, rim=half-opening;
        FloorPatch(floor,cell,center,new Rect(-half,-half,rim,Cell));
        FloorPatch(floor,cell,center,new Rect(opening,-half,rim,Cell));
        FloorPatch(floor,cell,center,new Rect(-opening,-half,art.stairWidth,rim));
        FloorPatch(floor,cell,center,new Rect(-opening,opening,art.stairWidth,rim));
        // 바닥 높이 0에서 얕은 입구의 안쪽으로 연결한다. 중앙 바닥은 그리지 않아 계단을 덮지 않는다.
        var face=Batch(batches,art.wallFace.texture);var uv=art.wallFace.uv;
        float x=center.x,z=center.y;
        var tint=new Color(.42f,.47f,.58f);
        face.Quad(new(x-opening,-StairDepth,z-opening),new(x+opening,-StairDepth,z-opening),new(x+opening,0,z-opening),new(x-opening,0,z-opening),uv,tint);
        face.Quad(new(x+opening,-StairDepth,z+opening),new(x-opening,-StairDepth,z+opening),new(x-opening,0,z+opening),new(x+opening,0,z+opening),uv,tint);
        face.Quad(new(x-opening,-StairDepth,z+opening),new(x-opening,-StairDepth,z-opening),new(x-opening,0,z-opening),new(x-opening,0,z+opening),uv,tint);
        face.Quad(new(x+opening,-StairDepth,z-opening),new(x+opening,-StairDepth,z+opening),new(x+opening,0,z+opening),new(x+opening,0,z-opening),uv,tint);
    }

    private void FloorPatch(Geometry mesh,Vector2Int cell,Vector2 center,Rect local)
    {
        float period=art.floorRepeatCells;
        var uv=new Rect((cell.x+(local.xMin+Cell*.5f)/Cell)/period,(cell.y+(local.yMin+Cell*.5f)/Cell)/period,
            local.width/Cell/period,local.height/Cell/period);
        Ground(mesh,center+local.center,local.size,0,uv,Color.white);
    }

    private MeshRenderer Standing(string name,Vector2 point,DungeonArtRegion region,float width,Color color)
    {
        // 위치마다 자동 축소하면 동일한 시설이 다른 크기로 보이므로 설정 폭을 그대로 쓴다.
        float correction = 1f/Mathf.Max(.1f,Mathf.Cos(ThreeDPresentationConfig.Active.spriteCameraElevation*Mathf.Deg2Rad));
        Vector3 origin = ThreeDWorldView.Map(point,.025f);
        Vector3 right = Vector3.right*width*.5f, up=Vector3.up*(width/region.Aspect*correction);
        var shape = new Geometry();
        shape.Quad(origin-right,origin+right,origin+right+up,origin-right+up,region.uv,color);
        var renderer = Emit(name,shape,MaterialFor(region.texture,true));
        renderer.sortingOrder = 1002-Mathf.RoundToInt(point.y*20);
        return renderer;
    }

    private void Glow(Geometry geometry,Vector2 point)
    {
        float radius=art.portalWidth*.8f;
        Vector3 origin=ThreeDWorldView.Map(point,.018f);int start=geometry.vertices.Count;
        geometry.vertices.Add(origin);geometry.uvs.Add(Vector2.one*.5f);geometry.colors.Add(new Color(.15f,1f,.67f,.2f));
        const int segments=32;
        for(int i=0;i<segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;
            geometry.vertices.Add(origin+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius*.7f));
            geometry.uvs.Add(Vector2.zero);geometry.colors.Add(new Color(.15f,1f,.67f,0f));
            geometry.triangles.Add(start);geometry.triangles.Add(start+1+i);geometry.triangles.Add(start+1+(i+1)%segments);
        }
    }

    private void Box(Dictionary<Texture2D,Geometry> batches,Vector2 point,Vector2 size,float height,DungeonArtRegion top)
    {
        Ground(Batch(batches,top.texture),point,size,height,top.uv,Color.white);
        var face = Batch(batches,art.wallFace.texture); Rect uv=art.wallFace.uv;
        float left=point.x-size.x*.5f,right=point.x+size.x*.5f,south=point.y-size.y*.5f,north=point.y+size.y*.5f;
        face.Quad(new(left,0,south),new(right,0,south),new(right,height,south),new(left,height,south),uv,Color.white);
        face.Quad(new(right,0,north),new(left,0,north),new(left,height,north),new(right,height,north),uv,new Color(.8f,.83f,.9f));
        face.Quad(new(left,0,north),new(left,0,south),new(left,height,south),new(left,height,north),uv,new Color(.86f,.89f,.96f));
        face.Quad(new(right,0,south),new(right,0,north),new(right,height,north),new(right,height,south),uv,new Color(.8f,.84f,.92f));
    }

    private static Geometry Batch(Dictionary<Texture2D,Geometry> batches,Texture2D texture)
    {
        if (!batches.TryGetValue(texture,out var geometry)) batches.Add(texture,geometry=new Geometry());
        return geometry;
    }
    private static void Ground(Geometry mesh,Vector2 point,Vector2 size,float height,Rect uv,Color color)
    {
        float x=point.x,z=point.y,hx=size.x*.5f,hz=size.y*.5f;
        mesh.Quad(new(x-hx,height,z-hz),new(x+hx,height,z-hz),new(x+hx,height,z+hz),new(x-hx,height,z+hz),uv,color);
    }
    private MeshRenderer Emit(string name,Geometry geometry,Material material)
    {
        var mesh = new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
        mesh.SetVertices(geometry.vertices); mesh.SetUVs(0,geometry.uvs); mesh.SetColors(geometry.colors);
        mesh.SetTriangles(geometry.triangles,0); mesh.RecalculateBounds(); meshes.Add(mesh);
        var obj = new GameObject(name); obj.transform.SetParent(transform,false); obj.layer=ThreeDWorldView.RenderLayer;
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return renderer;
    }
    private static Vector2[] Uvs(Rect rect) => new[] { new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax) };

    private sealed class Geometry
    {
        public readonly List<Vector3> vertices = new();
        public readonly List<Vector2> uvs = new();
        public readonly List<Color> colors = new();
        public readonly List<int> triangles = new();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect uv,Color tint)
        {
            int start=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uvs.Add(new(uv.xMin,uv.yMin));uvs.Add(new(uv.xMax,uv.yMin));uvs.Add(new(uv.xMax,uv.yMax));uvs.Add(new(uv.xMin,uv.yMax));
            for (int i=0;i<4;i++) colors.Add(tint);
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
        }
    }
    private void OnDestroy()
    {
        foreach (var mesh in meshes) if(mesh!=null)Release(mesh);
        foreach (var material in materials) if(material!=null)Release(material);
    }
    private static void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
}
