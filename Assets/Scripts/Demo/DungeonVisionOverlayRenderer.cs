using UnityEngine;
using UnityEngine.UI;

/// <summary>UI 셰이더 한 장으로 플레이어 중심의 원형 시야를 안정적으로 합성한다.</summary>
public sealed class DungeonVisionOverlayRenderer : MonoBehaviour
{
    private Transform player;
    private Camera worldCamera;
    private Material material;

    public static void Create(Transform target)
    {
        GameObject root = new GameObject("Dungeon Vision Overlay");
        DungeonVisionOverlayRenderer overlay = root.AddComponent<DungeonVisionOverlayRenderer>();
        overlay.player = target;
        overlay.worldCamera = Camera.main;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject imageObject = new GameObject("Vision Gradient");
        imageObject.transform.SetParent(root.transform, false);
        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Shader shader = Resources.Load<Shader>("VisionOverlay");
        if (shader == null) { Debug.LogError("VisionOverlay shader was not found."); root.SetActive(false); return; }
        overlay.material = new Material(shader);
        image.material = overlay.material;
        image.texture = Texture2D.whiteTexture;
    }

    private void LateUpdate()
    {
        if (player == null || material == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null || !worldCamera.orthographic) return;
        DungeonTuning tuning = DungeonTuning.Active;
        Vector3 viewport = worldCamera.WorldToViewportPoint(player.position);
        float screenWorldHeight = worldCamera.orthographicSize * 2f;
        material.SetVector("_VisionCenter", new Vector4(viewport.x, viewport.y, 0f, 0f));
        material.SetFloat("_ClearRadius", tuning.clearSightRadius / screenWorldHeight);
        material.SetFloat("_DarkRadius", Mathf.Max(tuning.darkSightRadius, tuning.clearSightRadius + .01f) / screenWorldHeight);
        material.SetFloat("_OuterDarkness", tuning.outerDarkness);
        material.SetFloat("_Aspect", Screen.width / Mathf.Max(1f, Screen.height));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
