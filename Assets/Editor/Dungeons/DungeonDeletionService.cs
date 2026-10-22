#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>던전 삭제 범위를 먼저 계산한다. 폴더 전체·공유 에셋·저장 파일은 삭제하지 않는다.</summary>
public static class DungeonDeletionService
{
    public sealed class Plan
    {
        public readonly DungeonDefinition Dungeon;
        public readonly string Name, DungeonPath;
        internal readonly string[] Floors;
        public int FloorCount => Floors.Length;
        public readonly int RetainedFloorCount;

        internal Plan(DungeonDefinition dungeon, string path, string[] floors, int retained)
        { Dungeon = dungeon; Name = dungeon.displayName; DungeonPath = path; Floors = floors; RetainedFloorCount = retained; }
    }

    private static bool CanTrash(string path) => !string.IsNullOrEmpty(path)
        && path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("Assets/MidtermProfiling/", StringComparison.OrdinalIgnoreCase);

    public static bool TryPlan(DungeonDefinition dungeon, out Plan plan, out string error)
    {
        plan = null; error = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { error = "플레이를 종료한 뒤 던전을 삭제하세요."; return false; }
        string path = AssetDatabase.GetAssetPath(dungeon);
        if (dungeon == null || !CanTrash(path) || !AssetDatabase.IsMainAsset(dungeon))
        { error = "Assets 아래에 저장된 던전 에셋을 선택하세요. 프로파일링 폴더는 삭제 대상에서 제외됩니다."; return false; }
        try
        {
            // 한 파일에 공유 가능한 다른 하위 에셋이 들어 있으면 파일 단위 삭제를 하지 않는다.
            if (AssetDatabase.LoadAllAssetsAtPath(path).Any(item => item != null && item != dungeon))
            { error = "다른 하위 에셋을 포함한 던전 파일입니다. 하위 에셋을 별도 파일로 분리한 뒤 삭제하세요."; return false; }
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            int retained = 0;
            foreach (var floor in (dungeon.floors ?? Array.Empty<DungeonFloorDefinition>()).Where(item => item != null).Distinct())
            {
                string floorPath = AssetDatabase.GetAssetPath(floor);
                if (!CanTrash(floorPath) || !AssetDatabase.IsMainAsset(floor)
                    || AssetDatabase.LoadAllAssetsAtPath(floorPath).Any(item => item != null && item != floor))
                { retained++; continue; }
                candidates.Add(floorPath);
            }
            if (candidates.Count > 0)
            {
                // 다른 던전뿐 아니라 씬·다른 데이터 에셋이 직접 참조하는 층도 남긴다.
                // 삭제할 던전과 후보 층 자체는 외부 참조의 출처에서 제외한다.
                string[] sources = AssetDatabase.GetAllAssetPaths().Where(item => item.StartsWith("Assets/", StringComparison.Ordinal)
                    && item != path && !candidates.Contains(item) && !AssetDatabase.IsValidFolder(item)).ToArray();
                var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(sources, false), StringComparer.Ordinal);
                // 다른 던전에서 아직 저장하지 않은 층 참조도 공유로 취급한다.
                foreach (string guid in AssetDatabase.FindAssets("t:DungeonDefinition", new[] { "Assets" }))
                {
                    var other = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (other == null || other == dungeon || other.floors == null) continue;
                    foreach (var floor in other.floors) if (floor != null) dependencies.Add(AssetDatabase.GetAssetPath(floor));
                }
                retained += candidates.Count(item => dependencies.Contains(item));
                candidates.RemoveWhere(item => dependencies.Contains(item));
            }
            plan = new Plan(dungeon, path, candidates.OrderBy(item => item, StringComparer.Ordinal).ToArray(), retained);
            return true;
        }
        catch (Exception exception) { error = "삭제 범위를 확인하지 못했습니다: " + exception.Message; return false; }
    }

    public static bool Delete(Plan approved, out string message)
    {
        message = null;
        if (approved == null || !TryPlan(approved.Dungeon, out var current, out message)) return false;
        if (approved.DungeonPath != current.DungeonPath || !approved.Floors.SequenceEqual(current.Floors))
        { message = "확인 중 던전이나 층 구성이 변경되었습니다. 삭제 버튼을 다시 눌러 확인하세요."; return false; }
        var catalogs = new List<DungeonCatalog>();
        var previous = new List<DungeonDefinition[]>();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:DungeonCatalog", new[] { "Assets" }))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DungeonCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog?.dungeons == null || !catalog.dungeons.Contains(current.Dungeon)) continue;
                catalogs.Add(catalog); previous.Add(catalog.dungeons);
            }
            // 카탈로그 갱신을 먼저 저장한다. 실패 시 아직 파일을 지우지 않았으므로 참조를 복원할 수 있다.
            for (int i = 0; i < catalogs.Count; i++)
            {
                catalogs[i].dungeons = previous[i].Where(item => item != current.Dungeon).ToArray();
                EditorUtility.SetDirty(catalogs[i]); AssetDatabase.SaveAssetIfDirty(catalogs[i]);
            }
            if (!AssetDatabase.MoveAssetToTrash(current.DungeonPath)) throw new InvalidOperationException("던전 파일을 휴지통으로 옮기지 못했습니다.");
        }
        catch (Exception exception)
        {
            var restoreErrors = new List<string>();
            for (int i = 0; i < catalogs.Count; i++)
            {
                catalogs[i].dungeons = previous[i]; EditorUtility.SetDirty(catalogs[i]);
                try { AssetDatabase.SaveAssetIfDirty(catalogs[i]); }
                catch (Exception restoreError) { restoreErrors.Add(restoreError.Message); }
            }
            message = "던전을 삭제하지 못했습니다: " + exception.Message;
            if (restoreErrors.Count > 0) message += "\n카탈로그 복원 저장도 실패했습니다. 파일 권한을 확인하고 다시 저장하세요: " + string.Join("; ", restoreErrors);
            return false;
        }
        // 본체 삭제가 끝난 뒤 전용 층을 정리한다. 일부 층이 실패해도 삭제된 던전을 재등록하지 않는다.
        var remaining = new List<string>();
        foreach (string floorPath in current.Floors)
        {
            try { if (!AssetDatabase.MoveAssetToTrash(floorPath)) remaining.Add(floorPath); }
            catch (Exception) { remaining.Add(floorPath); }
        }
        message = $"‘{approved.Name}’ 던전을 삭제했습니다. 전용 층 {current.FloorCount - remaining.Count}개를 휴지통으로 이동했습니다.";
        if (current.RetainedFloorCount > 0) message += $" 공유되거나 별도 보관이 필요한 층 {current.RetainedFloorCount}개는 유지했습니다.";
        if (remaining.Count > 0) message += "\n휴지통 이동에 실패해 남아 있는 층:\n" + string.Join("\n", remaining);
        return true;
    }
}
#endif
