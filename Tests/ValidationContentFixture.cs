#if UNITY_EDITOR
using System;
using UnityEditor;

/// <summary>격리 검사용 콘텐츠. 실제 게임의 제작 도구에서 샘플을 자동 등록하지 않는다.</summary>
public static class ValidationContentFixture
{
    public static void EnsureSampleDungeon()
    {
        const string path = "Assets/Resources/Dungeons/deep-salvage.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
        if (existing != null)
        {
            if (!DungeonAuthoringService.SaveAndRegister(existing, out string existingError)) throw new InvalidOperationException(existingError);
            return;
        }
        var dungeon = DungeonAuthoringService.Create(path, "깊은 회수 유적"); dungeon.dungeonId = "dungeon.deep-salvage";
        DungeonAuthoringService.AddFloor(dungeon); dungeon.extraExitFloor = 1;
        if (!DungeonAuthoringService.SaveAndRegister(dungeon, out string error)) throw new InvalidOperationException(error);
    }
}
#endif
