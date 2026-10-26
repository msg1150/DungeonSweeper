#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Play 없이 임포트된 원화·발판·카메라 투영과 시설 충돌 위치를 검사한다.</summary>
public static class TownPresentationEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        var art = Resources.Load<TownPresentationConfig>("TownPresentation");
        check(art != null && art.IsValid(), "new town art config and four facility sprites load correctly");
        var expectedNames = new[] { "Salvager Guild", "Supply Shop", "Town Warehouse", "Dungeon Entrance" };
        var expectedAnchors = new[] { new Vector2(.273f,.448f), new Vector2(.531f,.29f), new Vector2(.381f,.652f), new Vector2(.746f,.493f) };
        var mask = new Color32[16*16];
        for (int y = 2; y < 16; y++)
        for (int x = 0; x < 16; x++)
            if (y >= 8 || (x >= (y == 2 ? 4 : y < 5 ? 3 : 2) && x < (y == 2 ? 12 : y < 5 ? 13 : 14))) mask[y*16+x] = new Color32(255,255,255,255);
        mask[0] = new Color32(0,0,0,20);
        Vector2[] baked = TownFootprintBaker.Extract(mask,16,16,new Vector2(8,2),.4f,.5f);
        Vector2 min = Vector2.one * float.PositiveInfinity, max = -min;
        foreach (var point in baked) { var pixel = point*16+new Vector2(8,2); min=Vector2.Min(min,pixel); max=Vector2.Max(max,pixel); }
        check(min == new Vector2(2,2) && max == new Vector2(14,8), "alpha bake excludes transparent margins, faint shadows and the roof overhang");
        check(baked.Length >= 4 && baked.Length <= 256, "alpha silhouette simplification keeps a bounded polygon");
        bool emptyRejected = false;
        try { TownFootprintBaker.Extract(new Color32[16],4,4,Vector2.zero,.25f,.5f); }
        catch (InvalidOperationException) { emptyRejected = true; }
        check(emptyRejected, "empty alpha masks cannot replace a usable footprint");
        var cameraObject = new GameObject("Town projection check");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = art.ViewHeight * .5f; camera.aspect = art.referenceAspect;
        Quaternion rotation = Quaternion.Euler(ThreeDPresentationConfig.Active.spriteCameraElevation, 0f, 0f);
        camera.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * 35f, rotation);
        try
        {
            for (int i = 0; i < 4; i++)
            {
                var row = art.facilities[i];
                check(row.objectName == expectedNames[i] && Vector2.Distance(row.feetAnchor, expectedAnchors[i]) < .0001f,
                    "facility uses the supplied composition role and anchor: " + row.objectName);
                Vector2 position = art.ToWorld(row.feetAnchor, true);
                Vector3 projected = camera.WorldToViewportPoint(ThreeDWorldView.Map(position));
                check(Mathf.Abs(projected.x - row.feetAnchor.x) < .0001f && Mathf.Abs(projected.y - (1f-row.feetAnchor.y)) < .0001f,
                    "2.5D ground anchor projects to the reference art point: " + row.objectName);
                check(row.sprite.pivot.y / row.sprite.rect.height < .1f && row.sprite.pivot.x / row.sprite.rect.width > .45f,
                    "facility sprite imports with a foot pivot instead of canvas-center pivot: " + row.objectName);
                check(row.HasCurrentFootprint, "facility uses a baked contour for the current Sprite and threshold: " + row.objectName);
                float scale = art.ViewWidth*row.visibleWidth/(row.sprite.bounds.size.x*row.imageWidthFraction);
                float top = row.feetAnchor.y-row.sprite.bounds.max.y*scale/art.ViewHeight;
                check(top > -.001f, "relocated facility roof remains inside the frame: " + row.objectName);
            }
            foreach (float aspect in new[] { 4f/3f, 16f/9f, 1746f/901f, 21f/9f })
            {
                Rect viewport = TownPresentationConfig.FitViewport(aspect, art.referenceAspect);
                check(viewport.x >= 0f && viewport.y >= 0f && viewport.xMax <= 1.00001f && viewport.yMax <= 1.00001f
                    && Mathf.Abs(aspect*viewport.width/viewport.height-art.referenceAspect) < .0001f,
                    "town viewport preserves all artwork proportions at aspect " + aspect);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
        Scene original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Town.unity"); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            TownCasualVisuals.ApplyArt(art, true);
            Physics2D.SyncTransforms();
            foreach (var row in art.facilities)
            {
                var facility = GameObject.Find(row.objectName);
                var collider = facility.GetComponent<PolygonCollider2D>();
                Rect expected = art.Footprint(row, true);
                check(facility.GetComponent<SpriteRenderer>().sprite == row.sprite
                    && Vector2.Distance(facility.transform.position, art.ToWorld(row.feetAnchor,true)) < .0001f,
                    "display and interaction root share the new anchor: " + row.objectName);
                check(Vector2.Distance(collider.bounds.center, expected.center) < .001f
                    && Vector2.Distance(collider.bounds.size, expected.size) < .001f,
                    "scaled sprite collision matches its authored footprint: " + row.objectName);
                check(facility.GetComponent<TownFacilityView>() != null, "town artwork bypasses runtime alpha readback: " + row.objectName);
                check(collider.pathCount == 1 && collider.GetPath(0).Length == row.footprintOutline.Length
                    && (facility.GetComponent<BoxCollider2D>() == null || !facility.GetComponent<BoxCollider2D>().enabled),
                    "alpha polygon is the only active facility collision: " + row.objectName);
                bool samePoints = true;
                foreach (var point in collider.GetPath(0))
                {
                    Vector2 world = facility.transform.TransformPoint(point);
                    if (!art.OverlapsFootprint(row,new Rect(world-Vector2.one*.01f,Vector2.one*.02f),true)) samePoints=false;
                }
                check(samePoints, "save restoration and physical contact use the same contour: " + row.objectName);
                float oldThreshold = row.footprintAlphaThreshold;
                try { row.footprintAlphaThreshold = .55f; check(!row.HasCurrentFootprint, "changing alpha threshold requires an explicit rebake: " + row.objectName); }
                finally { row.footprintAlphaThreshold = oldThreshold; }
            }
            var player = GameObject.Find("Player"); var body = player.GetComponent<BoxCollider2D>();
            var spawn = art.ToWorld(art.playerSpawn, true);
            check(TownCasualVisuals.ResolvePlayerPosition(spawn,body,true) == spawn, "clear plaza spawn remains unchanged");
            var blocked = art.Footprint(art.facilities[2], true).center;
            var restored = TownCasualVisuals.ResolvePlayerPosition(blocked,body,true);
            check(restored != blocked && art.Contains(restored,true), "old save inside a relocated building restores to safe ground");
            check(TownCasualVisuals.ResolvePlayerPosition(new Vector2(float.NaN,0f),body,true) == spawn, "invalid old town position cannot poison movement");
            var edge = GameObject.Find("Town Walkable Boundary").GetComponent<EdgeCollider2D>();
            check(edge.pointCount == art.walkableOutline.Length+1 && edge.points[0] == edge.points[edge.pointCount-1], "town shoreline barrier is a closed loop");
            check(!art.Contains(art.ToWorld(new Vector2(.05f,.9f),true),true)
                && art.Contains(art.ToWorld(new Vector2(.51f,.92f),true),true), "riverbank is blocked while the lower bridge remains walkable");
            int count = UnityEngine.Object.FindObjectsByType<TownFacilityView>().Length;
            TownCasualVisuals.ApplyArt(art,true);
            check(UnityEngine.Object.FindObjectsByType<TownFacilityView>().Length == count, "reapplying town art does not duplicate facilities");
            foreach (var row in art.facilities)
                check(GameObject.Find(row.objectName).GetComponents<PolygonCollider2D>().Length == 1, "reapplying art does not duplicate polygon colliders: " + row.objectName);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene,true);
        }
    }
}
#endif
