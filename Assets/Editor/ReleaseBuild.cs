#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Rejects builds that cannot run the configured game and protected save flow.</summary>
public sealed class ReleaseBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows && report.summary.platform != BuildTarget.StandaloneWindows64)
            throw new BuildFailedException("Protected saves support Windows only. Add a platform save protector before building another target.");
        List<string> issues = CollectIssues();
        if (issues.Count > 0) throw new BuildFailedException("Dungeon Sweeper build validation failed:\n" + string.Join("\n", issues));
    }

    public static List<string> CollectIssues()
    {
        List<string> issues = new();
        GameFlowConfig flow = Resources.Load<GameFlowConfig>("GameFlowConfig");
        if (flow == null) { issues.Add("Missing Resources/GameFlowConfig."); return issues; }
        string[] scenes = EnabledScenes();
        string[] names = scenes.Select(Path.GetFileNameWithoutExtension).ToArray();
        if (names.Length == 0 || names[0] != flow.mainMenuSceneName) issues.Add("The first enabled build scene must be the configured main menu.");
        foreach (string required in new[] { flow.mainMenuSceneName, flow.townSceneName, flow.dungeonSceneName })
            if (string.IsNullOrWhiteSpace(required) || names.Count(name => name == required) != 1)
                issues.Add("Required scene must exist exactly once: " + required);
        if (new[] { flow.mainMenuSceneName, flow.townSceneName, flow.dungeonSceneName }.Distinct().Count() != 3)
            issues.Add("Main menu, town and dungeon must have different names.");
        if (flow.manualSaveSlotCount < 1) issues.Add("Manual save slot count must be positive.");
        foreach (float range in new[] { flow.corpseRange, flow.exitRange, flow.townGateRange, flow.townGuildRange, flow.townShopRange })
            if (!Positive(range)) issues.Add("Interaction ranges must be finite and positive.");
        DungeonTuning tuning = Resources.Load<DungeonTuning>("DungeonTuning");
        if (tuning == null) issues.Add("Missing Resources/DungeonTuning.");
        else
        {
            foreach (float value in new[] { tuning.lootValueMultiplier, tuning.clearSightRadius, tuning.darkSightRadius,
                tuning.playerMoveSpeed, tuning.playerDashSpeed, tuning.playerDashDuration, tuning.patrolSpeed,
                tuning.chaseSpeed, tuning.detectionRange, tuning.hearingRange, tuning.investigationSeconds, tuning.patrolTravelDistance })
                if (!Positive(value)) issues.Add("Movement, economy and vision tuning must be finite and positive.");
            if (tuning.playerMaxHealth < 1 || !Nonnegative(tuning.playerDashCooldown) || !Nonnegative(tuning.playerHitInvulnerability)
                || !Nonnegative(tuning.patrolArrivalPause)
                || !Nonnegative(tuning.outerDarkness) || tuning.outerDarkness > 1f || tuning.darkSightRadius <= tuning.clearSightRadius)
                issues.Add("Invalid health, cooldown or vision tuning.");
        }
        if (Resources.Load<Shader>("VisionOverlay") == null) issues.Add("Missing Resources/VisionOverlay shader.");
        if (Resources.Load<MainMenuPresentationConfig>("MainMenuPresentation") == null) issues.Add("Missing menu presentation config.");
        PlayerVisualProfile profile = Resources.Load<PlayerVisualProfile>("PlayerVisualProfile");
        if (profile == null) issues.Add("Missing player visual profile.");
        else if (profile.columns < 1 || profile.rows < 1 || !Positive(profile.walkFramesPerSecond)
            || !Positive(profile.dashFramesPerSecond) || !Positive(profile.visualScale)) issues.Add("Invalid player animation profile.");
        HashSet<string> lootKinds = new();
        DungeonCatalog catalog = Resources.Load<DungeonCatalog>("DungeonCatalog");
        if (catalog == null) issues.Add("Missing Resources/DungeonCatalog.");
        else
        {
            issues.AddRange(catalog.CollectIssues());
            if (catalog.monsterPrefabs != null) foreach (var prefab in catalog.monsterPrefabs)
                if (prefab != null) ValidatePrefab(prefab.gameObject, prefab.IsValid(), issues);
            if (catalog.corpsePrefabs != null) foreach (var prefab in catalog.corpsePrefabs)
                if (prefab != null) ValidatePrefab(prefab.gameObject, prefab.IsValid(), issues);
            if (catalog.dungeons != null) foreach (var dungeon in catalog.dungeons)
            {
                if (dungeon == null) continue;
                if (dungeon.corpses != null) foreach (var row in dungeon.corpses)
                {
                    if (row?.prefab == null) continue;
                    if (row.count > 0 && row.prefab.loot != null) foreach (var loot in row.prefab.loot)
                        if (loot != null && loot.dropChance > 0f) lootKinds.Add(loot.kindId ?? "");
                }
            }
        }
        TownEconomyConfig economy = Resources.Load<TownEconomyConfig>("TownEconomyConfig");
        if (economy?.bagUpgrades == null) issues.Add("Missing town economy config.");
        else
        {
            int width = GridInventory.Width, height = GridInventory.Height;
            foreach (var upgrade in economy.bagUpgrades)
            {
                if (upgrade == null || !upgrade.IsValid()) { issues.Add("Invalid bag upgrade recipe."); continue; }
                if (upgrade.width < width || upgrade.height < height || (upgrade.width == width && upgrade.height == height)) issues.Add("Bag upgrades must increase capacity without shrinking dimensions.");
                foreach (var material in upgrade.materials) if (!lootKinds.Contains(material.kindId)) issues.Add("Bag material has no loot source: " + material.kindId);
                width = upgrade.width; height = upgrade.height;
            }
        }
        foreach (string path in scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { issues.Add("Missing build scene: " + path); continue; }
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                List<GameObject> objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(item => item.gameObject).ToList();
                if (objects.Any(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item) > 0)) issues.Add("Missing script in scene: " + path);
                if (scene.name == flow.townSceneName || scene.name == flow.dungeonSceneName)
                {
                    if (objects.Count(item => item.activeInHierarchy && item.GetComponent<PlayerMovement>() != null) != 1)
                        issues.Add("Gameplay scene requires exactly one active player: " + path);
                    if (!objects.Any(item => item.activeInHierarchy && item.CompareTag("MainCamera") && item.GetComponent<Camera>() != null))
                        issues.Add("Gameplay scene requires a MainCamera: " + path);
                    if (scene.name == flow.townSceneName && !objects.Any(item => item.GetComponent<TownHubController>() != null))
                        issues.Add("Town scene requires a hub controller.");
                    if (scene.name == flow.dungeonSceneName && !objects.Any(item => item.GetComponent<DungeonRunController>() != null))
                        issues.Add("Dungeon scene requires a run controller.");
                    foreach (var run in objects.Select(item => item.GetComponent<DungeonRunController>()).Where(item => item != null))
                        if (run.dungeonDefinition != null && (catalog == null || catalog.FindDungeon(run.dungeonDefinition.dungeonId) != run.dungeonDefinition))
                            issues.Add("Direct-play dungeon override must be registered in DungeonCatalog: " + path);
                }
            }
            finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        return issues;
    }

    public static string[] EnabledScenes() => EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
    private static void ValidatePrefab(GameObject prefab, bool valid, List<string> issues)
    {
        if (!PrefabUtility.IsPartOfPrefabAsset(prefab) || !valid || !prefab.activeSelf
            || prefab.GetComponentsInChildren<Transform>(true).Any(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0))
            issues.Add("Invalid monster/corpse prefab asset: " + prefab.name);
    }
    private static bool Positive(float value) => Nonnegative(value) && value > 0f;
    private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

public static class ReleaseBuild
{
    [MenuItem("Dungeon Sweeper/Build Windows Release")]
    public static void Windows()
    {
        string output = Path.GetFullPath("Builds/Windows/DungeonSweeper.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = ReleaseBuildValidator.EnabledScenes(), locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Windows release build failed.");
        Debug.Log("Windows release build completed: " + output);
    }
}
#endif
