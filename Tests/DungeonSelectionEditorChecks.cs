#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>플레이 없이 난이도 데이터와 선택창의 카탈로그 구성을 확인한다.</summary>
public static class DungeonSelectionEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        var originalCatalog = DungeonCatalog.Active;
        var source = originalCatalog.dungeons.First(item => item != null);
        var copy = UnityEngine.Object.Instantiate(source);
        var catalog = UnityEngine.Object.Instantiate(originalCatalog);
        var missingMap = ScriptableObject.CreateInstance<DungeonDefinition>();
        var emptyFloor = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        emptyFloor.map = null;
        missingMap.dungeonId = "dungeon.selection-missing-map"; missingMap.floors = new[] { emptyFloor };
        var owner = new GameObject("Selection data validation");
        var active = typeof(DungeonCatalog).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            string[] expected = { "쉬움", "보통", "어려움", "매우 어려움" };
            for (int i = 0; i < expected.Length; i++)
            {
                copy.difficulty = (DungeonDifficulty)i;
                copy.selectionImage = null;
                check(copy.DifficultyLabel == expected[i] && copy.IsValid(out _), "difficulty label and image-free entry: " + expected[i]);
            }
            copy.dungeonId = "dungeon.selection-invalid"; copy.difficulty = (DungeonDifficulty)999;
            check(!copy.IsValid(out _), "unsupported difficulty rejected by entry validation");
            catalog.dungeons = new[] { null, source, source, copy, missingMap };
            active.SetValue(null, catalog);
            var panel = owner.AddComponent<TownCommercePanel>();
            typeof(TownCommercePanel).GetMethod("RefreshDungeons", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
            var choices = (IList)typeof(TownCommercePanel).GetField("dungeonChoices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            check(choices.Count == 3, "selection skips null and duplicate entries");
            var entryType = choices[0].GetType();
            check((DungeonDefinition)entryType.GetField("Definition").GetValue(choices[0]) == source
                && entryType.GetField("Error").GetValue(choices[0]) == null, "selection retains the specific registered dungeon");
            check(entryType.GetField("Error").GetValue(choices[1]) is string, "invalid entry retains its reason for disabled entrance");
            check(entryType.GetField("Error").GetValue(choices[2]) is string, "missing map cannot enable entry when validation omits its error message");
            check(originalCatalog.dungeons != catalog.dungeons, "selection checks leave the actual catalog unchanged");
        }
        finally
        {
            active.SetValue(null, originalCatalog);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(copy);
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(missingMap);
            UnityEngine.Object.DestroyImmediate(emptyFloor);
        }
    }

    public static void RunStandalone()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!root.EndsWith("/.utmp/UnityValidation", StringComparison.Ordinal) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Selection checks require the isolated project outside Play mode.");
        int count = 0;
        Action<bool, string> check = (passed, message) => { if (!passed) throw new InvalidOperationException(message); count++; };
        Run(check);
        DungeonGenerationEditorChecks.Run(check);
        File.WriteAllText(Path.Combine(root, "dungeon-selection-result.txt"), $"PASS: {count} editor data checks; Play mode was not entered.");
    }
}
#endif
