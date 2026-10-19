using UnityEngine;
using UnityEngine.UI;

/// <summary>UI 셰이더 한 장으로 플레이어 중심의 원형 시야를 안정적으로 합성한다.</summary>
// 카메라(500)와 2.5D 표시(700)가 이동한 뒤 같은 프레임의 시야를 계산한다.
[DefaultExecutionOrder(900)]
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
        if (worldCamera == null) return;
        DungeonTuning tuning = DungeonTuning.Active;
        if (ThreeDWorldView.Current != null)
        {
            Vector3 origin = worldCamera.transform.position;
            material.SetFloat("_UseWorld3D", 1f);
            material.SetVector("_WorldCamera", origin);
            material.SetVector("_WorldPlayer", ThreeDWorldView.Map(player.position));
            material.SetFloat("_Orthographic", worldCamera.orthographic ? 1f : 0f);
            if (worldCamera.orthographic)
            {
                // 정사영 카메라는 화면마다 광선 시작점이 다르다. 원근용 광선 보간을 재사용하지 않는다.
                material.SetVector("_Ground00", Ground(0, 0));
                material.SetVector("_Ground10", Ground(1, 0));
                material.SetVector("_Ground01", Ground(0, 1));
                material.SetVector("_Ground11", Ground(1, 1));
            }
            material.SetVector("_Ray00", worldCamera.ViewportToWorldPoint(new Vector3(0,0,1)) - origin);
            material.SetVector("_Ray10", worldCamera.ViewportToWorldPoint(new Vector3(1,0,1)) - origin);
            material.SetVector("_Ray01", worldCamera.ViewportToWorldPoint(new Vector3(0,1,1)) - origin);
            material.SetVector("_Ray11", worldCamera.ViewportToWorldPoint(new Vector3(1,1,1)) - origin);
            material.SetFloat("_ClearRadius", tuning.clearSightRadius);
            material.SetFloat("_DarkRadius", Mathf.Max(tuning.darkSightRadius,tuning.clearSightRadius+.01f));
            material.SetFloat("_OuterDarkness", tuning.outerDarkness);
            return;
        }
        material.SetFloat("_UseWorld3D", 0f);
        if (!worldCamera.orthographic) return;
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

    private Vector3 Ground(float x, float y)
    {
        Ray ray = worldCamera.ViewportPointToRay(new Vector3(x, y, 0));
        return ray.origin + ray.direction * (-ray.origin.y / Mathf.Min(-.0001f, ray.direction.y));
    }
}
