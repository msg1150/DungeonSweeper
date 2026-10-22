#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DungeonVisionEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal)
            || Application.isPlaying) throw new InvalidOperationException("Vision checks require the isolated editor outside Play mode.");
        var catalog = DungeonCatalog.Active;
        var source = catalog.dungeons.First(item => item != null);
        var dungeon = UnityEngine.Object.Instantiate(source);
        var other = ScriptableObject.CreateInstance<DungeonDefinition>();
        var owner = new GameObject("Dungeon vision data check"); owner.SetActive(false);
        var shader = Resources.Load<Shader>("VisionOverlay");
        var material = new Material(shader);
        var window = ScriptableObject.CreateInstance<DungeonBuilderWindow>();
        DungeonDefinition preview = null;
        string folder = "Assets/Editor/vision-checks-" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor", Path.GetFileName(folder));
        var oldDungeons = catalog.dungeons; var oldMonsters = catalog.monsterPrefabs; var oldCorpses = catalog.corpsePrefabs;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            check(other.vision.clearSightRadius == 3 && other.vision.darkSightRadius == 5 && Mathf.Approximately(other.vision.outerDarkness, .9f),
                "new model defaults retain the original 3/5/0.9 presentation");
            var defaults = DungeonVisionSettings.FromDefaults(); var tuning = DungeonTuning.Active;
            check(defaults.clearSightRadius == tuning.clearSightRadius && defaults.darkSightRadius == tuning.darkSightRadius
                && defaults.outerDarkness == tuning.outerDarkness, "new dungeon defaults copy current tuning values");
            float originalDarkness = tuning.outerDarkness;
            defaults.outerDarkness = originalDarkness == 0 ? 1 : 0;
            check(tuning.outerDarkness == originalDarkness, "editing copied defaults does not modify tuning");
            dungeon.vision = new DungeonVisionSettings { clearSightRadius = 2, darkSightRadius = 8, outerDarkness = .35f };
            check(dungeon.IsValid(out _), "a dungeon accepts independent vision values");
            dungeon.vision.clearSightRadius = float.NaN; check(!dungeon.IsValid(out _), "NaN vision radius rejected"); dungeon.vision.clearSightRadius = 2;
            dungeon.vision.darkSightRadius = 2; check(!dungeon.IsValid(out _), "outer radius must exceed the clear radius"); dungeon.vision.darkSightRadius = 8;
            dungeon.vision.outerDarkness = 1.1f; check(!dungeon.IsValid(out _), "darkness above one rejected");
            dungeon.vision.outerDarkness = float.PositiveInfinity; check(!dungeon.IsValid(out _), "nonfinite darkness rejected");
            dungeon.vision.outerDarkness = 0; check(dungeon.IsValid(out _), "zero darkness allows an unmasked dungeon");
            dungeon.vision.outerDarkness = .35f;

            var overlay = owner.AddComponent<DungeonVisionOverlayRenderer>();
            typeof(DungeonVisionOverlayRenderer).GetField("material", flags).SetValue(overlay, material);
            var definitionField = typeof(DungeonVisionOverlayRenderer).GetField("dungeon", flags); definitionField.SetValue(overlay, dungeon);
            var apply = typeof(DungeonVisionOverlayRenderer).GetMethod("ApplyVision", flags);
            apply.Invoke(overlay, new object[] { 1f });
            check(Matches(material, 2, 8, .35f), "2.5D overlay submits the selected dungeon values to its shader");
            apply.Invoke(overlay, new object[] { .1f });
            check(Matches(material, .2f, .8f, .35f), "2D overlay normalizes radii while preserving the same darkness strength");
            dungeon.vision.outerDarkness = .6f; apply.Invoke(overlay, new object[] { 1f });
            check(Matches(material, 2, 8, .6f) && other.vision.outerDarkness == .9f,
                "live asset edits affect only the connected dungeon");
            definitionField.SetValue(overlay, null); apply.Invoke(overlay, new object[] { 1f });
            check(Matches(material, tuning.clearSightRadius, tuning.darkSightRadius, tuning.outerDarkness),
                "legacy runs without a dungeon definition preserve global fallback settings");
            var customVision = dungeon.vision; dungeon.vision = null; definitionField.SetValue(overlay, dungeon);
            apply.Invoke(overlay, new object[] { 1f });
            check(Matches(material, tuning.clearSightRadius, tuning.darkSightRadius, tuning.outerDarkness),
                "missing vision data also retains the legacy fallback");
            dungeon.vision = customVision;
            var restored = JsonUtility.FromJson<DungeonVisionSettings>(JsonUtility.ToJson(dungeon.vision));
            check(restored.IsValid() && restored.clearSightRadius == 2 && restored.darkSightRadius == 8 && restored.outerDarkness == .6f,
                "vision data round trip preserves custom settings");

            var manual = DungeonAuthoringService.Create(folder + "/manual.asset", "시야 검사");
            check(manual.vision.clearSightRadius == tuning.clearSightRadius && manual.vision.darkSightRadius == tuning.darkSightRadius
                && manual.vision.outerDarkness == tuning.outerDarkness, "manual authoring starts with copied tuning defaults");
            preview = DungeonGenerationPreview.Create(new DungeonGenerationSettings { floorCount = 2 }, "시야 검사");
            check(preview.vision.clearSightRadius == tuning.clearSightRadius && preview.vision.outerDarkness == tuning.outerDarkness,
                "generated candidates start with copied tuning defaults");
            var generated = new DungeonVisionSettings { clearSightRadius = 1.5f, darkSightRadius = 9, outerDarkness = .25f };
            typeof(DungeonBuilderWindow).GetField("generatedVision", flags).SetValue(window, generated);
            typeof(DungeonBuilderWindow).GetMethod("ApplyGeneratedPresentation", flags).Invoke(window, new object[] { preview });
            check(preview.vision != generated && preview.vision.clearSightRadius == 1.5f && preview.vision.outerDarkness == .25f,
                "automatic tool copies presentation values without sharing its mutable settings object");
            var saved = DungeonGenerationPreview.Adopt(preview, folder + "/adopted.asset", out string error);
            check(saved != null && saved.vision.darkSightRadius == 9 && saved.vision.outerDarkness == .25f,
                "adoption persists dungeon-wide vision settings: " + error);
            preview.vision.outerDarkness = .8f;
            check(saved.vision.outerDarkness == .25f, "adopted settings remain independent of a discarded candidate");
        }
        finally
        {
            catalog.dungeons = oldDungeons; catalog.monsterPrefabs = oldMonsters; catalog.corpsePrefabs = oldCorpses;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            DungeonGenerationPreview.Discard(preview); AssetDatabase.DeleteAsset(folder);
            UnityEngine.Object.DestroyImmediate(window);
            UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(dungeon); UnityEngine.Object.DestroyImmediate(other);
        }
    }

    private static bool Matches(Material material, float clear, float dark, float strength)
        => Mathf.Approximately(material.GetFloat("_ClearRadius"), clear) && Mathf.Approximately(material.GetFloat("_DarkRadius"), dark)
            && Mathf.Approximately(material.GetFloat("_OuterDarkness"), strength);
}
#endif
