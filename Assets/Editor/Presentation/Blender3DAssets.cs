#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>FBX는 원본 파일로 남기고 Unity 재질·클립 연결·프리팹을 생성한다.</summary>
[InitializeOnLoad]
public sealed class Blender3DAssets : AssetPostprocessor
{
    static Blender3DAssets() => EditorApplication.delayCall += Ensure;
    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Models/Blender/", StringComparison.Ordinal)) return;
        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.importCameras = importer.importLights = false;
        importer.isReadable = false;
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = !assetPath.EndsWith("Town.fbx") && !assetPath.EndsWith("Portal.fbx");
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    [MenuItem("Dungeon Sweeper/Create Missing Blender 3D Assets")]
    public static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Assets/Models/Blender/CatJanitor.fbx")) return;
        Directory.CreateDirectory("Assets/Models/Materials");
        Directory.CreateDirectory("Assets/Prefabs/Characters");
        Directory.CreateDirectory("Assets/Prefabs/Monsters/3D");
        Directory.CreateDirectory("Assets/Prefabs/Environment");
        const string configPath = "Assets/Resources/ThreeDPresentation.asset";
        var config = AssetDatabase.LoadAssetAtPath<ThreeDPresentationConfig>(configPath);
        if (config == null) { config = ScriptableObject.CreateInstance<ThreeDPresentationConfig>(); AssetDatabase.CreateAsset(config, configPath); }
        if (config.cat == null) config.cat = Prefab("CatJanitor", "Assets/Prefabs/Characters/CatJanitor3D.prefab");
        if (config.goblin == null) config.goblin = Prefab("Goblin", "Assets/Prefabs/Monsters/3D/Goblin3D.prefab");
        if (config.slime == null) config.slime = Prefab("Slime", "Assets/Prefabs/Monsters/3D/Slime3D.prefab");
        if (config.orc == null) config.orc = Prefab("Orc", "Assets/Prefabs/Monsters/3D/Orc3D.prefab");
        if (config.town == null) config.town = Prefab("Town", "Assets/Prefabs/Environment/Town3D.prefab");
        if (config.portal == null) config.portal = Prefab("Portal", "Assets/Prefabs/Environment/Portal3D.prefab");
        string[] species = { "goblin", "slime", "orc" };
        foreach (string kind in species)
        {
            foreach (string folder in new[] { "Monsters", "Corpses" })
            {
                string path = "Assets/Prefabs/" + folder + "/" + kind + ".prefab";
                var actor = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (actor == null) continue;
                UnityEngine.Object component = folder == "Monsters" ? (UnityEngine.Object)actor.GetComponent<MonsterPrefab>() : actor.GetComponent<CorpsePrefab>();
                var settings = new SerializedObject(component);
                if (settings.FindProperty("model3D").objectReferenceValue == null)
                {
                    settings.FindProperty("model3D").objectReferenceValue = config.Model(kind);
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
        const string rendererPath = "Assets/Settings/Renderer3D.asset";
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (data == null) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, rendererPath); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < renderers.arraySize; i++) if (renderers.GetArrayElementAtIndex(i).objectReferenceValue == data) index = i;
        if (index < 0)
        {
            index = renderers.arraySize; renderers.InsertArrayElementAtIndex(index); renderers.GetArrayElementAtIndex(index).objectReferenceValue = data;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_MSAA").intValue = 4;
        }
        config.forwardRendererIndex = index;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        Debug.Log(config.useSpriteActors ? "2.5D presentation is ready. Blender experiment assets are retained." : "Blender 3D models, animation clips and forward renderer are ready.");
    }

    private static GameObject Prefab(string name, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing; // Do not erase subsequent authoring changes.
        string sourcePath = "Assets/Models/Blender/" + name + ".fbx";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("Missing Blender export: " + sourcePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = name + "3D";
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            if (clips.Length > 0)
            {
                Animation animation = instance.GetComponent<Animation>();
                if (animation == null) animation = instance.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (string state in new[] { "Idle", "Walk", "Dash", "Attack", "Loot", "Defeated" })
                {
                    var clip = clips.FirstOrDefault(item => item.name.EndsWith("_" + state, StringComparison.Ordinal) || item.name.EndsWith("|" + state, StringComparison.Ordinal) || item.name == state);
                    if (clip == null) throw new InvalidOperationException(name + " lacks " + state + ": " + string.Join(",", clips.Select(item => item.name)));
                    animation.AddClip(clip, state);
                    animation[state].wrapMode = state == "Idle" || state == "Walk" || state == "Loot" ? WrapMode.Loop : WrapMode.ClampForever;
                }
                animation.clip = clips.FirstOrDefault(item => item.name.EndsWith("_Idle", StringComparison.Ordinal));
            }
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static Material MaterialFor(Material imported)
    {
        string name = imported == null ? "Fallback" : imported.name.Split('.')[0];
        string path = "Assets/Models/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        Color color = imported != null && imported.HasProperty("_Color") ? imported.GetColor("_Color") : imported != null && imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : Color.gray;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", name == "Steel" || name == "Slime" ? .35f : .12f);
        material.SetFloat("_Metallic", name == "Steel" || name == "Gold" ? .35f : 0f);
        if (name == "Portal")
        {
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .8f);
            material.SetFloat("_Cull", 0f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
