#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates editable prefab assets once; never overwrites authored content.</summary>
public static class DungeonContentAuthoring
{
    [MenuItem("Dungeon Sweeper/Create Missing Default Dungeon Content")]
    public static void EnsureDefaults()
    {
        const string root = "Assets/Prefabs";
        Directory.CreateDirectory(root + "/Monsters");
        Directory.CreateDirectory(root + "/Corpses");
        Directory.CreateDirectory(root + "/Previews");
        Directory.CreateDirectory("Assets/Resources/Dungeons");
        AssetDatabase.Refresh();
        var monsters = new List<MonsterPrefab>();
        var corpses = new List<CorpsePrefab>();
        var definitions = MonsterDatabase.Active.GetSpawnDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            int corpseIndex = System.Array.IndexOf(new[] { "goblin", "slime", "orc" }, definition.id);
            if (corpseIndex < 0) continue; // The bundled corpse sheet supports these three default species.
            string monsterPath = root + "/Monsters/" + definition.id + ".prefab";
            var monster = AssetDatabase.LoadAssetAtPath<GameObject>(monsterPath)?.GetComponent<MonsterPrefab>();
            if (monster == null)
            {
                if (File.Exists(monsterPath)) throw new System.InvalidOperationException("Existing prefab has no MonsterPrefab component: " + monsterPath);
                var obj = new GameObject(definition.displayName);
                try
                {
                    obj.transform.localScale = new Vector3(.55f, .55f, 1f);
                    var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 3;
                    renderer.sprite = Preview(root + "/Previews/" + definition.id + "-monster.asset", definition.spriteSheetResource, 3, 2, 3);
                    obj.AddComponent<BoxCollider2D>().size = Vector2.one; obj.AddComponent<MonsterVisualAnimator>();
                    var settings = obj.AddComponent<MonsterPrefab>(); settings.prefabId = "monster." + definition.id;
                    settings.stats = JsonUtility.FromJson<MonsterDefinition>(JsonUtility.ToJson(definition));
                    settings.stats.hasMovementStats = true;
                    var tuning = DungeonTuning.Active;
                    settings.stats.patrolSpeed = tuning.patrolSpeed; settings.stats.chaseSpeed = tuning.chaseSpeed;
                    settings.stats.detectionRange = tuning.detectionRange; settings.stats.hearingRange = tuning.hearingRange;
                    settings.stats.investigationSeconds = tuning.investigationSeconds;
                    settings.stats.patrolTravelDistance = tuning.patrolTravelDistance; settings.stats.patrolArrivalPause = tuning.patrolArrivalPause;
                    settings.stats.loot = new(); // New corpse prefabs own their loot tables.
                    monster = PrefabUtility.SaveAsPrefabAsset(obj, monsterPath).GetComponent<MonsterPrefab>();
                }
                finally { Object.DestroyImmediate(obj); }
            }
            monsters.Add(monster);
            string corpsePath = root + "/Corpses/" + definition.id + ".prefab";
            var corpse = AssetDatabase.LoadAssetAtPath<GameObject>(corpsePath)?.GetComponent<CorpsePrefab>();
            if (corpse == null)
            {
                if (File.Exists(corpsePath)) throw new System.InvalidOperationException("Existing prefab has no CorpsePrefab component: " + corpsePath);
                var obj = new GameObject(definition.displayName + " 시체");
                try
                {
                    obj.transform.localScale = new Vector3(.4f, .4f, 1f);
                    var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 2;
                    renderer.sprite = Preview(root + "/Previews/" + definition.id + "-corpse.asset", "Sprites/Monsters/corpse-sheet", 3, 1, corpseIndex);
                    var settings = obj.AddComponent<CorpsePrefab>(); settings.prefabId = "corpse." + definition.id;
                    settings.displayName = obj.name; settings.sheetIndex = corpseIndex;
                    settings.loot = JsonUtility.FromJson<MonsterDefinition>(JsonUtility.ToJson(definition)).loot;
                    if (definition.attackStyle == MonsterAttackStyle.ClubSwing)
                    { settings.requiredSuccesses = 4; settings.pointerSpeed = 1.12f; settings.windowSize = .16f; }
                    corpse = PrefabUtility.SaveAsPrefabAsset(obj, corpsePath).GetComponent<CorpsePrefab>();
                }
                finally { Object.DestroyImmediate(obj); }
            }
            corpses.Add(corpse);
        }
        string[] ids = { "fork-vault", "ring-corridor", "stepped-crypt" };
        string[] names = { "갈림길 저장고", "고리형 회랑", "계단식 묘실" };
        var dungeons = new DungeonDefinition[3];
        for (int i = 0; i < 3; i++)
        {
            string path = "Assets/Resources/Dungeons/" + ids[i] + ".asset";
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
            if (dungeon == null)
            {
                if (File.Exists(path)) throw new System.InvalidOperationException("Existing asset is not a DungeonDefinition: " + path);
                dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
                dungeon.dungeonId = "dungeon." + ids[i]; dungeon.displayName = names[i]; dungeon.layoutIndex = i;
                dungeon.monsters = monsters.ConvertAll(prefab => new MonsterSpawnEntry { prefab = prefab, count = 1 }).ToArray();
                dungeon.corpses = corpses.ConvertAll(prefab => new CorpseSpawnEntry { prefab = prefab, count = 1 }).ToArray();
                AssetDatabase.CreateAsset(dungeon, path);
            }
            dungeons[i] = dungeon;
        }
        const string catalogPath = "Assets/Resources/DungeonCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<DungeonCatalog>(catalogPath) == null)
        {
            if (File.Exists(catalogPath)) throw new System.InvalidOperationException("Existing asset is not a DungeonCatalog: " + catalogPath);
            var catalog = ScriptableObject.CreateInstance<DungeonCatalog>(); catalog.dungeons = dungeons;
            catalog.monsterPrefabs = monsters.ToArray(); catalog.corpsePrefabs = corpses.ToArray();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Editable dungeon configurations and monster/corpse prefabs are ready.");
    }

    private static Sprite Preview(string path, string resource, int columns, int rows, int index)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (existing != null) return existing;
        if (File.Exists(path)) throw new System.InvalidOperationException("Existing preview is not a Sprite: " + path);
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null) throw new System.InvalidOperationException("Missing sprite sheet: " + resource);
        float width = texture.width / (float)columns, height = texture.height / (float)rows;
        var sprite = Sprite.Create(texture, new Rect(index % columns * width, index / columns * height, width, height), new Vector2(.5f, .35f), 180f);
        AssetDatabase.CreateAsset(sprite, path);
        return sprite;
    }
}

[CustomEditor(typeof(DungeonCatalog))]
public sealed class DungeonCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("새 몬스터·시체는 Monster Prefabs / Corpse Prefabs에 먼저 등록하고 던전 스폰 배열에 연결하세요. 스폰에서 제외해도 기존 저장에 사용한 등록 항목은 유지하세요.", MessageType.Info);
        DrawDefaultInspector();
        foreach (string issue in ((DungeonCatalog)target).CollectIssues()) EditorGUILayout.HelpBox(issue, MessageType.Error);
    }
}

[CustomEditor(typeof(DungeonDefinition))]
public sealed class DungeonDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("배열의 각 행에 프리팹과 Count를 넣으세요. 몬스터와 시체 수는 독립적이며 0개도 가능합니다. 같은 프리팹을 쓰는 던전은 능력치를 공유합니다.", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("dungeonId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        var layout = serializedObject.FindProperty("layoutIndex");
        layout.intValue = EditorGUILayout.Popup("Layout", layout.intValue, new[] { "갈림길 저장고", "고리형 회랑", "계단식 묘실" });
        EditorGUILayout.PropertyField(serializedObject.FindProperty("monsters"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("corpses"), true);
        serializedObject.ApplyModifiedProperties();
        var dungeon = (DungeonDefinition)target;
        if (!dungeon.IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        else EditorGUILayout.LabelField("배치 가능한 최대 개체 수", DungeonLayoutFactory.PopulationCapacity(dungeon.layoutIndex).ToString());
    }
}
#endif
