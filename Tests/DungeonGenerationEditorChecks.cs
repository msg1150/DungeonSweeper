using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DungeonGenerationEditorChecks
{
    public static void Run(Action<bool,string> check)
    {
        if (!Path.GetFullPath(Application.dataPath).Replace('\\','/').EndsWith("/.utmp/UnityValidation/Assets")) throw new InvalidOperationException("Editor generation tests require the isolated project.");
        var catalog = DungeonCatalog.Active;
        var previous = catalog.dungeons; var monsters = catalog.monsterPrefabs; var corpses = catalog.corpsePrefabs;
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/generated-validation.asset");
        var settings = new DungeonGenerationSettings { floorCount = 3,width = 32,height = 20,seed = 71081 };
        int assetCount = AssetDatabase.GetAllAssetPaths().Length;
        DungeonDefinition preview = null, saved = null;
        var sourceCatalog = ScriptableObject.CreateInstance<DungeonCatalog>();
        var sourceDungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
        var sourceFloor = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        string folder = null;
        try
        {
            sourceCatalog.dungeons = new[] { sourceDungeon };
            sourceDungeon.corpses = new[] { new CorpseSpawnEntry { prefab = catalog.FindCorpse("corpse.goblin"), count = 1 } };
            sourceDungeon.floors = new[] { sourceFloor };
            sourceFloor.corpses = new[] { new CorpseSpawnEntry { prefab = catalog.FindCorpse("corpse.slime"), count = 1 } };
            var sources = ReleaseBuildValidator.CollectLootSources(sourceCatalog);
            check(sources.Contains("slime.core") && !sources.Contains(LootKinds.GoblinHide),
                "release loot source validation uses authored floors instead of inactive legacy spawn rows");
            sourceFloor.corpses[0].count = 0;
            check(ReleaseBuildValidator.CollectLootSources(sourceCatalog).Count == 0,
                "zero-count floor corpses cannot falsely satisfy upgrade material sources");
            check(!DungeonAuthoringService.TryNewAssetPath("Assets/../../escape.asset", out _, out _)
                && !DungeonAuthoringService.TryNewAssetPath("Assets/Resources/DungeonCatalog.asset", out _, out _)
                && !DungeonAuthoringService.TryNewAssetPath("Assets/Editor/invalid.txt", out _, out _),
                "manual and generated authoring reject path traversal, overwrite and non-asset destinations");
            check(DungeonAuthoringService.TryNewAssetPath("Assets/Editor/../Editor/path-review.asset", out var normalized, out _)
                && normalized == "Assets/Editor/path-review.asset", "authoring normalizes relative segments within Assets without creating a file");
            preview = DungeonGenerationPreview.Create(settings,"미리보기 검사");
            check(preview.IsValid(out _) && !EditorUtility.IsPersistent(preview) && preview.floors.All(floor => !EditorUtility.IsPersistent(floor)),"generated candidate and all floors remain memory-only before adoption");
            check(catalog.dungeons.Length == previous.Length && AssetDatabase.GetAllAssetPaths().Length == assetCount,"preview creates no assets and registers no playable dungeon");
            settings.seed++;
            var regenerated = DungeonGenerationPreview.Create(settings,"다시 생성");
            check(regenerated.floors.Any(floor => !floor.map.tiles.SequenceEqual(preview.floors[Array.IndexOf(regenerated.floors,floor)].map.tiles)),"retry creates a fresh preview without modifying the first candidate");
            DungeonGenerationPreview.Discard(regenerated);
            check(catalog.dungeons.SequenceEqual(previous) && AssetDatabase.GetAllAssetPaths().Length == assetCount,"discarding a preview leaves catalog and project assets unchanged");
            check(DungeonGenerationPreview.Adopt(preview,"",out _) == null && AssetDatabase.GetAllAssetPaths().Length == assetCount,"cancelled adoption creates no files");
            check(DungeonGenerationPreview.Adopt(preview,"Assets/Resources/DungeonCatalog.asset",out _) == null,"adoption cannot overwrite an existing asset");
            preview.extraExitFloor = 99;
            check(DungeonGenerationPreview.Adopt(preview,path,out _) == null && !File.Exists(path),"invalid preview cannot be adopted or partially persisted");
            preview.extraExitFloor = -1;
            saved = DungeonGenerationPreview.Adopt(preview,path,out string error);
            check(saved != null,"adoption saves the reviewed candidate: " + error);
            folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(saved.floors[0])).Replace('\\','/');
            check(EditorUtility.IsPersistent(saved) && saved.floors.All(EditorUtility.IsPersistent) && catalog.FindDungeon(saved.dungeonId) == saved,
                "approved candidate is persisted and automatically registered for gameplay");
            check(saved.generationSeed == preview.generationSeed && saved.generationVersion == DungeonMapGenerator.Version
                && saved.floors.Select((floor,index) => floor.map.tiles.SequenceEqual(preview.floors[index].map.tiles)).All(value => value),
                "adoption saves exact reviewed geometry and seed without generating another layout");
            preview.floors[0].map.tiles[0] = 99;
            check(saved.IsValid(out _) && saved.floors[0].map.tiles[0] != 99,"persistent maps are independent copies of the temporary preview");
            string text = File.ReadAllText(AssetDatabase.GetAssetPath(saved.floors[0]));
            check(text.Contains("DungeonFloorDefinition") && File.Exists(path + ".meta"),"approved floor data and Unity GUID metadata exist on disk");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceFloor);
            UnityEngine.Object.DestroyImmediate(sourceDungeon);
            UnityEngine.Object.DestroyImmediate(sourceCatalog);
            catalog.dungeons = previous; catalog.monsterPrefabs = monsters; catalog.corpsePrefabs = corpses;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            // 이 테스트에서 만든 Assets/Editor 경로만 제거한다. 사용자 프로젝트에서는 실행하지 않는다.
            AssetDatabase.DeleteAsset(path);
            if (folder != null && folder.StartsWith("Assets/Editor/",StringComparison.Ordinal)) AssetDatabase.DeleteAsset(folder);
            DungeonGenerationPreview.Discard(preview);
        }
    }
}
