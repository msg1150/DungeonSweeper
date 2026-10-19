#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Inspector 중복·실효 없는 설정 노출·삭제한 스크립트 참조의 재발을 플레이 없이 확인한다.</summary>
public static class SourceCleanupEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Path.GetFullPath(Application.dataPath).Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("Cleanup checks require the isolated project.");
        var inspectedType = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var editors = new Dictionary<Type, Type>();
        foreach (var type in TypeCache.GetTypesWithAttribute<CustomEditor>())
        {
            if (type.Assembly != typeof(DungeonDefinitionInspector).Assembly) continue;
            foreach (CustomEditor attribute in type.GetCustomAttributes(typeof(CustomEditor), false))
            {
                var targetType = (Type)inspectedType.GetValue(attribute);
                check(!editors.ContainsKey(targetType), "single project Inspector for " + targetType.Name);
                editors[targetType] = type;
            }
        }
        check(editors[typeof(DungeonDefinition)] == typeof(DungeonDefinitionInspector), "floor-aware dungeon Inspector is selected");
        check(new[] { typeof(DungeonDefinition), typeof(DungeonFloorDefinition), typeof(MonsterDatabase) }
            .All(type => !Attribute.IsDefined(type, typeof(CreateAssetMenuAttribute))), "old asset creation menus cannot bypass the authoring tool");
        check(TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type => type.Assembly == typeof(DungeonRunController).Assembly)
            .All(type => type.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) == null),
            "no static Reset method can collide with the editor component callback");
        var dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
        var tuning = ScriptableObject.CreateInstance<DungeonTuning>();
        try
        {
            var fields = VisibleFields(dungeon);
            check(!fields.Contains("monsters") && !fields.Contains("corpses") && !fields.Contains("layoutIndex")
                && fields.Contains("floors") && fields.Contains("difficulty"), "dungeon default Inspector cannot expose inactive single-floor settings");
            fields = VisibleFields(tuning);
            check(!fields.Contains("patrolSpeed") && !fields.Contains("chaseSpeed") && !fields.Contains("detectionRange")
                && fields.Contains("playerMoveSpeed") && fields.Contains("showMonsterPerceptionRanges"), "tuning keeps active player/debug settings without old monster controls");
        }
        finally { UnityEngine.Object.DestroyImmediate(dungeon); UnityEngine.Object.DestroyImmediate(tuning); }
        foreach (string retired in new[] { "PlayerInteractor", "DismantleController", "CorpseInteractable", "IInteractable", "DungeonTestBootstrap" })
            check(Type.GetType(retired + ", Assembly-CSharp") == null, "retired type removed: " + retired);

        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Town.unity", "Assets/Scenes/Dungeon.unity", "Assets/Scenes/Dungeon_Test.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(item => item.gameObject).ToArray();
                check(objects.All(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item) == 0), "no missing scripts in " + path);
                if (path.EndsWith("Dungeon.unity") || path.EndsWith("Dungeon_Test.unity"))
                {
                    check(objects.Count(item => item.GetComponent<DungeonRunController>() != null) == 1
                        && objects.Count(item => item.GetComponent<PlayerMovement>() != null) == 1, "current run controller/player in " + path);
                    check(objects.All(item => item.GetComponent<Collider2D>() == null || item.GetComponent<PlayerMovement>() != null),
                        "no unused static collision geometry in " + path);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        CheckFloorCreation(check);
    }

    private static HashSet<string> VisibleFields(UnityEngine.Object target)
    {
        var result = new HashSet<string>();
        using var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter)) { enter = false; result.Add(property.name); }
        return result;
    }

    private static void CheckFloorCreation(Action<bool, string> check)
    {
        string folder = "Assets/Editor/cleanup-checks-" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor", Path.GetFileName(folder));
        var legacy = ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            var prefab = DungeonCatalog.Active.monsterPrefabs.First(item => item != null);
            legacy.dungeonId = "dungeon.cleanup-validation";
            legacy.monsters = new[] { new MonsterSpawnEntry { prefab = prefab, count = 2 } };
            legacy.corpses = Array.Empty<CorpseSpawnEntry>();
            AssetDatabase.CreateAsset(legacy, folder + "/legacy.asset");
            DungeonAuthoringService.ConvertLegacy(legacy);
            check(legacy.floors.Length == 1 && legacy.floors[0].monsters[0].count == 2 && legacy.floors[0].corpses.Length == 0,
                "legacy conversion preserves explicit monster counts and empty corpse arrays");
            check(legacy.monsters.Length == 0 && legacy.corpses.Length == 0, "conversion clears inactive global spawn arrays");
            legacy.monsters = new[] { new MonsterSpawnEntry { prefab = prefab, count = 99 } };
            var added = DungeonAuthoringService.AddFloor(legacy);
            check(added.monsters.All(row => row.count == 1) && legacy.floors[0].monsters[0].count == 2,
                "new floors cannot silently reuse inactive global counts");
        }
        finally
        {
            // Only files created under this unique folder in the isolated project are removed.
            AssetDatabase.DeleteAsset(folder);
            if (legacy != null && !EditorUtility.IsPersistent(legacy)) UnityEngine.Object.DestroyImmediate(legacy);
        }
    }

    public static void RunStandalone()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!root.EndsWith("/.utmp/UnityValidation", StringComparison.Ordinal) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cleanup checks require the isolated project outside Play mode.");
        int count = 0;
        Action<bool, string> check = (passed, message) => { if (!passed) throw new InvalidOperationException(message); count++; };
        Run(check);
        DungeonGenerationEditorChecks.Run(check);
        DungeonSelectionEditorChecks.Run(check);
        File.WriteAllText(Path.Combine(root, "source-cleanup-result.txt"), $"PASS: {count} editor checks; Play mode was not entered.");
    }
}
#endif
