using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>격리 프로젝트에서 직접 만든 임시 에셋만 삭제해 공유 층·카탈로그 처리를 검증한다.</summary>
public static class DungeonDeletionEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Path.GetFullPath(Application.dataPath).Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets"))
            throw new InvalidOperationException("Dungeon deletion checks require the isolated project.");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/deletion-validation");
        string folderGuid = AssetDatabase.CreateFolder("Assets/Editor", Path.GetFileName(folder));
        if (string.IsNullOrEmpty(folderGuid)) throw new InvalidOperationException("Could not create deletion test fixtures.");
        try
        {
            var owner = ScriptableObject.CreateInstance<DungeonDefinition>();
            var other = ScriptableObject.CreateInstance<DungeonDefinition>();
            var exclusive = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
            var shared = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
            var first = ScriptableObject.CreateInstance<DungeonCatalog>();
            var second = ScriptableObject.CreateInstance<DungeonCatalog>();
            string ownerPath = folder + "/owner.asset", exclusivePath = folder + "/exclusive.asset", sharedPath = folder + "/shared.asset";
            AssetDatabase.CreateAsset(owner, ownerPath);
            AssetDatabase.CreateAsset(other, folder + "/other.asset");
            AssetDatabase.CreateAsset(exclusive, exclusivePath);
            AssetDatabase.CreateAsset(shared, sharedPath);
            owner.floors = new[] { exclusive, shared }; other.floors = new[] { shared };
            first.dungeons = new[] { owner, other }; second.dungeons = new[] { owner, owner, other };
            AssetDatabase.CreateAsset(first, folder + "/catalog-one.asset");
            AssetDatabase.CreateAsset(second, folder + "/catalog-two.asset");
            EditorUtility.SetDirty(owner); EditorUtility.SetDirty(other);
            AssetDatabase.SaveAssetIfDirty(owner); AssetDatabase.SaveAssetIfDirty(other);
            check(DungeonDeletionService.TryPlan(owner, out var plan, out var error)
                && plan.FloorCount == 1 && plan.RetainedFloorCount == 1, "deletion plan separates exclusive and shared floors: " + error);
            check(AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ownerPath) == owner && first.dungeons.Length == 2,
                "reviewing a deletion plan leaves source assets and catalog unchanged");
            other.floors = new[] { shared, exclusive }; EditorUtility.SetDirty(other);
            check(!DungeonDeletionService.Delete(plan, out _) && AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ownerPath) == owner,
                "changed floor references invalidate the reviewed deletion plan before any deletion");
            other.floors = new[] { shared }; EditorUtility.SetDirty(other); AssetDatabase.SaveAssetIfDirty(other);
            check(DungeonDeletionService.TryPlan(owner, out plan, out error) && DungeonDeletionService.Delete(plan, out error),
                "confirmed test dungeon and its exclusive floor move to trash: " + error);
            check(AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ownerPath) == null
                && AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(exclusivePath) == null,
                "deleted dungeon and exclusive floor no longer exist as project assets");
            check(AssetDatabase.LoadAssetAtPath<DungeonFloorDefinition>(sharedPath) == shared && other.floors[0] == shared,
                "shared floor and its other dungeon survive deletion");
            check(first.dungeons.Length == 1 && second.dungeons.Length == 1 && first.dungeons[0] == other && second.dungeons[0] == other,
                "all catalogs remove the deleted dungeon including duplicate references");
            check(!DungeonDeletionService.TryPlan(null, out _, out _), "missing dungeon cannot produce a deletion plan");
        }
        finally
        {
            // 이 호출에서 새로 만든 폴더만 정리한다. 실제 프로젝트의 에셋은 대상이 아니다.
            if (AssetDatabase.AssetPathToGUID(folder) == folderGuid) AssetDatabase.DeleteAsset(folder);
        }
    }

    public static void RunStandalone()
    {
        int count = 0;
        Run((passed, name) => { if (!passed) throw new InvalidOperationException(name); count++; });
        string result = $"PASS: {count} editor deletion checks. Play mode was not entered.";
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../deletion-validation-result.txt")), result);
        Debug.Log(result);
    }
}
