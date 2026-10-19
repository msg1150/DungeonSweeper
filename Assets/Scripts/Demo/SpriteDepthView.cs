using UnityEngine;

/// <summary>
/// 원래 스프라이트 애니메이션은 XY 논리 객체에서 계속 실행한다.
/// 표시 사본만 XZ 지면으로 옮겨 카메라를 향하게 하므로 이동·판정·저장은 바뀌지 않는다.
/// </summary>
[DefaultExecutionOrder(700)]
public sealed class SpriteDepthView : MonoBehaviour
{
    private SpriteRenderer[] sources, copies;
    private GameObject display;
    private SpriteRenderer shadow;
    private ThreeDWorldView boundWorld;
    private float footLift, width;
    public GameObject Display => display;
    public SpriteRenderer[] Copies => copies;

    public static SpriteDepthView Attach(GameObject owner)
    {
        var view = owner.GetComponent<SpriteDepthView>();
        if (view == null) view = owner.AddComponent<SpriteDepthView>();
        view.EnsureDisplay();
        view.Sync();
        return view;
    }

    private void EnsureDisplay()
    {
        if (display != null || ThreeDWorldView.Current == null) return;
        sources = GetComponentsInChildren<SpriteRenderer>(true);
        if (sources.Length == 0) return;
        // 논리 배우와 같은 수명: 풀 배우는 풀 아래, 마을 시설은 현재 월드 아래.
        bool pooled = GetComponent<MonsterPrefab>() != null || GetComponent<CorpsePrefab>() != null;
        display = new GameObject(name + " 2.5D");
        display.transform.SetParent(pooled ? transform.parent : ThreeDWorldView.Current.transform, false);
        copies = new SpriteRenderer[sources.Length];
        SpriteRenderer reference = null;
        for (int i = 0; i < sources.Length; i++)
        {
            var child = new GameObject(sources[i].name + " Billboard");
            child.transform.SetParent(display.transform, false);
            copies[i] = child.AddComponent<SpriteRenderer>();
            if (reference == null && sources[i].enabled && sources[i].sprite != null) reference = sources[i];
        }
        if (reference != null)
        {
            // 최초 자세의 실제 불투명 발 위치를 사용한다. 공격 궤적/프레임 여백에 따라 출렁이지 않는다.
            footLift = ThreeDWorldView.Current.FootLift(reference.sprite) * Mathf.Abs(reference.transform.lossyScale.y);
            width = reference.sprite.bounds.size.x * Mathf.Abs(reference.transform.lossyScale.x);
        }
        var shadowObject = new GameObject("Ground contact shadow");
        shadowObject.transform.SetParent(display.transform, false);
        shadow = shadowObject.AddComponent<SpriteRenderer>();
        shadowObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        ThreeDWorldView.SetRenderLayer(display);
        BindWorld();
    }

    private void BindWorld()
    {
        boundWorld = ThreeDWorldView.Current;
        if (boundWorld == null) return;
        foreach (var copy in copies) copy.sharedMaterial = boundWorld.SpriteMaterial;
        shadow.sharedMaterial = boundWorld.SpriteMaterial;
        shadow.sprite = boundWorld.ContactShadow;
    }

    private void OnEnable() { if (display != null) display.SetActive(true); }
    private void OnDisable() { if (display != null) display.SetActive(false); }
    private void OnDestroy() { if (display != null) Destroy(display); }
    private void LateUpdate() { EnsureDisplay(); Sync(); }

    public void Sync()
    {
        var world = ThreeDWorldView.Current;
        if (display == null || world == null || world.WorldCamera == null) return;
        var run = DungeonRunController.Instance;
        bool hidden = run != null && run.Player == transform && run.IsPlayerHidden;
        display.SetActive(!hidden);
        if (hidden) return;
        if (boundWorld != world) BindWorld(); // 재입장 후 새 월드 자원을 연결하며 표시 객체는 재사용한다.
        var camera = world.WorldCamera;
        // 카메라의 기울기까지 따라 눕히면 그림의 상체가 지면 깊이 방향으로 뻗어
        // 발의 충돌 판정이 벽에 닿기 전에 머리/몸통부터 벽 메시를 뚫게 된다.
        // 지면에 수직으로 세우고 정사영에서 줄어드는 세로 길이만 보정한다.
        Vector3 facing = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        Quaternion rotation = facing.sqrMagnitude > .0001f ? Quaternion.LookRotation(facing, Vector3.up) : Quaternion.identity;
        float verticalProjection = Mathf.Max(.1f, Mathf.Abs(Vector3.Dot(camera.transform.up, Vector3.up)));
        Vector3 visualUp = Vector3.up / verticalProjection;
        Vector3 ground = ThreeDWorldView.Map(transform.position, world.FloorHeight(transform.position) + .025f);
        display.transform.position = ground;
        display.transform.rotation = Quaternion.identity;
        int order = 1000 - Mathf.RoundToInt(transform.position.y * 20);
        bool visible = false;
        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i]; var copy = copies[i];
            copy.enabled = source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            if (!copy.enabled) continue;
            visible = true;
            copy.sprite = source.sprite; copy.flipX = source.flipX; copy.flipY = source.flipY;
            Color tint = world.IsDungeon ? new Color(.82f, .88f, .92f, 1) : Color.white;
            copy.color = source.color * tint;
            copy.sortingOrder = order + source.sortingOrder;
            copy.spriteSortPoint = SpriteSortPoint.Pivot;
            Vector3 offset = source.transform.position - transform.position;
            copy.transform.position = ground + visualUp * (footLift + offset.y) + camera.transform.right * offset.x;
            copy.transform.rotation = rotation;
            Vector3 scale = source.transform.lossyScale;
            copy.transform.localScale = new Vector3(scale.x, scale.y / verticalProjection, 1);
        }
        shadow.enabled = visible;
        shadow.transform.position = ground;
        shadow.transform.localScale = new Vector3(Mathf.Clamp(width * .65f, .45f, 3f), Mathf.Clamp(width * .28f, .22f, 1f), 1);
        shadow.sortingOrder = -2;
        shadow.color = new Color(.03f, .055f, .06f, world.IsDungeon ? .55f : .28f);
    }
}
