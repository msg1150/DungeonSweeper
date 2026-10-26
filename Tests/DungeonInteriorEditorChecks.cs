#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>게임을 실행하지 않고 실제 임포트·메시·리소스 수명과 정적 원화 구성을 확인한다.</summary>
public static class DungeonInteriorEditorChecks
{
    public static void Run(Action<bool,string> check)
    {
        Guard();
        var art = DungeonInteriorArt.Active;
        check(art != null && art.IsValid(),"dungeon interior textures, UV regions and shader load from the shipped asset");
        var regions = new[] { art.horizontalTop,art.verticalTop,art.cornerTop,art.junctionTop,art.endTop,art.wallFace,
            art.portal,art.hideoutOpen,art.hideoutClosed,art.stairs };
        check(regions.All(region=>region.IsValid && !region.texture.isReadable),"interior PNGs keep no runtime CPU pixel copies");
        check(art.hideoutOpen.uv == art.hideoutClosed.uv && art.hideoutOpen.Aspect == art.hideoutClosed.Aspect,
            "hiding state changes use the same canvas framing and foot anchor");
        for(int mask=0;mask<16;mask++)
        {
            var expected = mask==3?art.horizontalTop:mask==12?art.verticalTop:
                new[]{0,1,2,4,8}.Contains(mask)?art.endTop:new[]{7,11,13,14,15}.Contains(mask)?art.junctionTop:art.cornerTop;
            check(art.WallTop(mask)==expected,"all wall connection masks select a usable region: "+mask);
        }
        // 격리 프로세스의 앞선 검사에서 남긴 미저장 빈 장면을 교체한다.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            var maps = new[] { PreviewMap(), DungeonMapGenerator.Generate(new DungeonGenerationSettings {floorCount=1,width=64,height=64,seed=19427})[0] };
            foreach(var map in maps)
            {
                var savedMap = JsonUtility.ToJson(map); string random = JsonUtility.ToJson(UnityEngine.Random.state);
                var root = new GameObject("Interior native check");
                var renderer = root.AddComponent<DungeonInteriorRenderer>();
                renderer.Build(art,map,map.Center(map.entrance),map.Center(new Vector2Int(15,10)));
                var shapes = root.GetComponentsInChildren<MeshFilter>();
                var ownedMeshes = shapes.Select(shape=>shape.sharedMesh).ToArray();
                var ownedMaterials = root.GetComponentsInChildren<MeshRenderer>().Select(item=>item.sharedMaterial).Distinct().ToArray();
                check(JsonUtility.ToJson(map)==savedMap && JsonUtility.ToJson(UnityEngine.Random.state)==random,
                    "art composition cannot modify saved maps, reserved cells or gameplay RNG: "+map.width);
                check(root.GetComponentsInChildren<Collider>().Length==0 && root.GetComponentsInChildren<Collider2D>().Length==0,
                    "art adds no duplicate terrain, stair or hideout collision: "+map.width);
                check(ownedMeshes.All(mesh=>mesh.vertexCount>0 && mesh.GetTriangles(0).All(index=>index>=0 && index<mesh.vertexCount)),
                    "terrain and prop meshes contain valid indices at map width "+map.width);
                check(renderer.TerrainBatchCount<=((map.width+15)/16)*((map.height+15)/16)*7 && ownedMaterials.Length<=12,
                    "terrain batches and materials are bounded by chunks and original textures: "+map.width);
                check(shapes.All(shape=>shape.gameObject.layer==ThreeDWorldView.RenderLayer),"new artwork uses the presentation camera layer");
                foreach(var cell in map.hidingSpots.Take(1))
                {
                    var hideout = root.GetComponentsInChildren<MeshRenderer>().First(item=>item.name=="은신처 원화");
                    var mesh = hideout.GetComponent<MeshFilter>().sharedMesh; var before = mesh.vertices;
                    renderer.SetHiding(cell,true);
                    check(hideout.sharedMaterial.mainTexture==art.hideoutClosed.texture && mesh.vertices.SequenceEqual(before),
                        "hiding closes the curtain without moving the foot anchor");
                    renderer.SetHiding(cell,false);
                    check(hideout.sharedMaterial.mainTexture==art.hideoutOpen.texture,"exiting hiding opens the curtain again");
                }
                // 일반 MonoBehaviour의 Play 수명 콜백은 Edit 모드에서 자동 호출된다고 가정하지 않는다.
                Release(renderer);
                UnityEngine.Object.DestroyImmediate(root);
                check(ownedMeshes.All(mesh=>mesh==null) && ownedMaterials.All(material=>material==null),"floor teardown releases every owned art mesh and material");
            }
            CheckFixedSizes(art,check);
            CheckFloorIntegration(art,check);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }

    private static void CheckFixedSizes(DungeonInteriorArt art,Action<bool,string> check)
    {
        var map=PreviewMap();
        map.hidingSpots=new(){new(1,3),new(3,3)};
        map.hasStairsUp=true;map.stairsUp=new(1,4);map.stairsDown=new(4,4);
        var root=new GameObject("Fixed prop size check");
        var renderer=root.AddComponent<DungeonInteriorRenderer>();
        try
        {
            renderer.Build(art,map,map.Center(new Vector2Int(1,7)),map.Center(new Vector2Int(15,10)));
            var shapes=root.GetComponentsInChildren<MeshFilter>();
            var hideouts=shapes.Where(shape=>shape.name=="은신처 원화").ToArray();
            var portals=shapes.Where(shape=>shape.name=="입구 원화" || shape.name=="특수 탈출 포탈 원화").ToArray();
            var stairs=shapes.Where(shape=>shape.name=="위층 계단 원화" || shape.name=="아래층 계단 원화").ToArray();
            bool SameSize(MeshFilter[] items) => items.Length==2 && (items[0].sharedMesh.bounds.size-items[1].sharedMesh.bounds.size).sqrMagnitude<.000001f;
            check(SameSize(hideouts),"hideouts next to a wall and in a room have identical visible width and height");
            check(SameSize(portals),"entrance and exit portal size is independent of side walls");
            check(SameSize(stairs),"up/down stairs keep identical dimensions in narrow and open floor cells");
            check(Mathf.Abs(hideouts[0].sharedMesh.bounds.size.x-art.hideoutWidth)<.0001f
                && Mathf.Abs(portals[0].sharedMesh.bounds.size.x-art.portalWidth)<.0001f
                && Mathf.Abs(stairs[0].sharedMesh.bounds.size.x-art.stairWidth)<.0001f,
                "every prop type uses its configured world width without an implicit location multiplier");
            var size=stairs[0].sharedMesh.bounds.size;
            check(Mathf.Abs(size.x-size.z)<.0001f && stairs[0].sharedMesh.bounds.center.y<0,
                "stairs use a square grid opening below floor height instead of a raised picture frame");
            var before=hideouts[0].sharedMesh.bounds;
            renderer.SetHiding(map.hidingSpots[0],true);renderer.SetHiding(map.hidingSpots[0],false);
            check(hideouts[0].sharedMesh.bounds==before,"curtain state changes preserve the fixed footprint and height");
        }
        finally { Release(renderer);UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckFloorIntegration(DungeonInteriorArt art,Action<bool,string> check)
    {
        var map=PlacementMap();var root=new GameObject("Tile integration check");var renderer=root.AddComponent<DungeonInteriorRenderer>();
        try
        {
            Vector2 portalPoint=map.Center(map.entrance);
            renderer.Build(art,map,portalPoint);
            var shapes=root.GetComponentsInChildren<MeshFilter>();
            const float clearHalf=(1.45f-.18f)*.5f;
            var portal=shapes.First(shape=>shape.name=="입구 원화");
            check(portal.sharedMesh.bounds.min.x>=portalPoint.x-clearHalf && portal.sharedMesh.bounds.max.x<=portalPoint.x+clearHalf,
                "a portal fits between the actual one-cell boundary walls at fixed size");
            var hiding=shapes.Where(shape=>shape.name=="은신처 원화").ToArray();
            for(int i=0;i<hiding.Length;i++)
            {
                Vector2 center=map.Center(map.hidingSpots[i]);var bounds=hiding[i].sharedMesh.bounds;
                check(bounds.min.x>=center.x-clearHalf && bounds.max.x<=center.x+clearHalf,
                    "hideout artwork stays clear of side walls in a corner and alcove: "+i);
            }
            var floor=shapes.Where(shape=>shape.GetComponent<MeshRenderer>().sharedMaterial.mainTexture==art.dustyFloor).ToArray();
            Vector2 opening=map.Center(map.stairsDown);bool coversOpening=false,continuousUv=true;
            foreach(var shape in floor)
            {
                var vertices=shape.sharedMesh.vertices;var uv=shape.sharedMesh.uv;var indices=shape.sharedMesh.triangles;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector2 expected=new((vertices[i].x/1.45f+map.width*.5f)/art.floorRepeatCells,
                        (vertices[i].z/1.45f+map.height*.5f)/art.floorRepeatCells);
                    if(Vector2.Distance(uv[i],expected)>.0001f || Mathf.Abs(vertices[i].y)>.0001f) continuousUv=false;
                }
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector2 Point(int index) => new(vertices[index].x,vertices[index].z);
                    if(InTriangle(opening,Point(indices[i]),Point(indices[i+1]),Point(indices[i+2]))) coversOpening=true;
                }
            }
            check(!coversOpening,"the floor contains a real opening instead of a floor tile under the stair image");
            check(continuousUv,"stair rim and surrounding ground share exactly the same height and texture phase");
            var stair=shapes.First(shape=>shape.name=="아래층 계단 원화");var boundsStair=stair.sharedMesh.bounds;
            check(boundsStair.max.y<0f && boundsStair.min.x>=opening.x-clearHalf && boundsStair.max.x<=opening.x+clearHalf
                && boundsStair.min.z>=opening.y-clearHalf && boundsStair.max.z<=opening.y+clearHalf,
                "the stair interior is recessed below a single floor cell and cannot overlap the neighboring wall");
            float previous=art.hideoutWidth;
            try { art.hideoutWidth=1.7f;check(!art.IsValid(),"oversized prop settings are rejected before they can intersect narrow walls again"); }
            finally { art.hideoutWidth=previous; }
        }
        finally { Release(renderer);UnityEngine.Object.DestroyImmediate(root); }
    }

    private static bool InTriangle(Vector2 point,Vector2 a,Vector2 b,Vector2 c)
    {
        float Cross(Vector2 x,Vector2 y) => x.x*y.y-x.y*y.x;
        float ab=Cross(b-a,point-a),bc=Cross(c-b,point-b),ca=Cross(a-c,point-c);
        return (ab>=-.000001f && bc>=-.000001f && ca>=-.000001f) || (ab<=.000001f && bc<=.000001f && ca<=.000001f);
    }

    private static DungeonFloorMap PlacementMap()
    {
        var map=new DungeonFloorMap {width=8,height=8,tiles=new int[64],entrance=new(1,2),hasStairsDown=true,
            stairsDown=new(6,5),hidingSpots=new(){new(1,5),new(5,1)}};
        for(int x=0;x<8;x++)for(int y=0;y<8;y++)map.tiles[y*8+x]=(int)(x==0||x==7||y==0||y==7?DungeonTile.Wall:DungeonTile.Floor);
        map.tiles[3*8+3]=2;map.tiles[4*8+3]=2;map.tiles[3*8+4]=2;
        return map;
    }

    public static DungeonFloorMap PreviewMap()
    {
        var map = new DungeonFloorMap {width=20,height=14,tiles=new int[280],entrance=new Vector2Int(3,2),
            hasStairsDown=true,stairsDown=new Vector2Int(17,10),hidingSpots=new(){new(3,11),new(2,6),new(17,2)}};
        for(int x=0;x<20;x++) for(int y=0;y<14;y++)
            map.tiles[y*20+x]=(int)(x==0 || x==19 || y==0 || y==13?DungeonTile.Wall:DungeonTile.Floor);
        for(int y=5;y<=8;y++) {map.tiles[y*20+7]=2;map.tiles[y*20+12]=2;}
        for(int x=7;x<=12;x++) map.tiles[5*20+x]=2;
        return map;
    }

    public static void CaptureStatic()
    {
        Guard();
        CaptureMap(PreviewMap(),"dungeon-interior-static.png",10f,new Vector2Int(15,10));
        CaptureMap(PlacementMap(),"dungeon-placement-details.png",4.8f,new Vector2Int(1,2));
    }

    private static void CaptureMap(DungeonFloorMap map,string filename,float viewSize,Vector2Int entrance)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Static interior preview");
        root.AddComponent<DungeonInteriorRenderer>().Build(DungeonInteriorArt.Active,map,map.Center(entrance));
        var cameraObject = new GameObject("Static preview camera"); var camera=cameraObject.AddComponent<Camera>();
        camera.GetUniversalAdditionalCameraData().SetRenderer(ThreeDPresentationConfig.Active.forwardRendererIndex);
        camera.orthographic=true;camera.orthographicSize=viewSize;camera.aspect=1.6f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.027f,.036f);
        camera.cullingMask=1<<ThreeDWorldView.RenderLayer;camera.nearClipPlane=.1f;camera.farClipPlane=100f;
        Quaternion rotation=Quaternion.Euler(ThreeDPresentationConfig.Active.spriteCameraElevation,0,0);
        camera.transform.SetPositionAndRotation(-(rotation*Vector3.forward)*40,rotation);
        var target = new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+filename),pixels.EncodeToPNG());
            Debug.Log("Captured the production dungeon artwork renderer outside Play mode.");
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;target.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(cameraObject);Release(root.GetComponent<DungeonInteriorRenderer>());UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static void Guard()
    {
        if(!Application.dataPath.Replace('\\','/').EndsWith("/.utmp/UnityValidation/Assets",StringComparison.Ordinal)
            || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Interior checks require an isolated editor outside Play mode.");
    }
    private static void Release(DungeonInteriorRenderer renderer) => typeof(DungeonInteriorRenderer)
        .GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(renderer,null);
}
#endif
