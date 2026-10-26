using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>마을 시설과 던전 선택창으로 진입하는 허브 흐름.</summary>
public class TownHubController : MonoBehaviour
{
    private Transform player;
    private Transform dungeonEntrance;
    private Transform guild;
    private Transform supplyShop;
    private Transform warehouse;
    private TownCommercePanel commerce;
    private int viewportWidth, viewportHeight;

    private void Awake()
    {
        TownCasualVisuals.Apply();
        commerce = gameObject.AddComponent<TownCommercePanel>();
        player = FindAnyObjectByType<PlayerMovement>()?.transform;
        dungeonEntrance = GameObject.Find("Dungeon Entrance")?.transform;
        guild = GameObject.Find("Salvager Guild")?.transform;
        supplyShop = GameObject.Find("Supply Shop")?.transform;
        warehouse = GameObject.Find("Town Warehouse")?.transform;
        if (player != null) PlayerVisualAnimator.Ensure(player.gameObject);
        if (player != null)
        {
            bool restored = GameSession.TakeTownPosition(out Vector2 restoredPosition);
            var art = TownPresentationConfig.Active;
            bool authoredTown = art != null && art.IsValid() && (!ThreeDPresentationConfig.Enabled || ThreeDPresentationConfig.SpritesEnabled);
            bool depth = ThreeDPresentationConfig.SpritesEnabled;
            Vector2 position = restored ? restoredPosition : authoredTown ? art.ToWorld(art.playerSpawn, depth) : (Vector2)player.position;
            if (authoredTown) position = TownCasualVisuals.ResolvePlayerPosition(position, player.GetComponent<BoxCollider2D>(), depth);
            player.position = position;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = position;
        }
        if (player != null) ThreeDWorldView.Create(player, false);
    }

    private void Update()
    {
        if (GameShell.IsGameplayInputBlocked || player == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        GameFlowConfig flow = GameFlowConfig.Active;
        if (IsNear(warehouse, flow.townShopRange)) { commerce.Open(TownFacility.Warehouse); return; }
        if (IsNear(dungeonEntrance, flow.townGateRange))
        {
            commerce.Open(TownFacility.Dungeons);
            return;
        }
        if (IsNear(guild, flow.townGuildRange))
        {
            commerce.Open(TownFacility.Guild);
            return;
        }
        if (IsNear(supplyShop, flow.townShopRange))
            commerce.Open(TownFacility.Market);
    }

    private void OnGUI()
    {
        if (GameShell.IsGameplayInputBlocked) return;
        using var gui = new GameGuiScope(true);
        bool hasDetail = TownProgress.HasActiveQuest || TownProgress.LastRecoveredCount > 0;
        DrawPanel(new Rect(18, 16, 390, hasDetail ? 78 : 54), new Color(1f, .98f, .91f, .64f));
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 16;
        GUI.color = Color.white;
        GUI.Label(new Rect(30, 22, 368, 24), $"금고 {TownProgress.Gold}G   |   보급 도구 {TownProgress.SupplyKits}");
        GUI.Label(new Rect(30, 45, 368, 24), $"창고 {TownProgress.WarehouseCount}개   |   가방 {TownProgress.BagSize.x} × {TownProgress.BagSize.y}");
        if (TownProgress.HasActiveQuest)
            GUI.Label(new Rect(30, 68, 368, 24), $"의뢰 {TownProgress.QuestProgressText} · 창고 기준");
        else if (TownProgress.LastRecoveredCount > 0)
            GUI.Label(new Rect(30, 68, 368, 24), $"최근 회수 {TownProgress.LastRecoveredCount}개 · 창고 보관 완료");
        GameFlowConfig flow = GameFlowConfig.Active;
        DrawFacilityLabel(warehouse, "창고", flow.townShopRange);
        DrawFacilityLabel(supplyShop, "거래소", flow.townShopRange);
        DrawFacilityLabel(guild, "길드", flow.townGuildRange);
        DrawFacilityLabel(dungeonEntrance, "던전", flow.townGateRange);
        if (player == null) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 18;
        GUI.color = Color.white;
        string prompt = null;
        if (IsNear(warehouse, flow.townShopRange)) prompt = "[E] 창고 · 가방 확장";
        else if (IsNear(dungeonEntrance, flow.townGateRange)) prompt = "[E] 던전 선택 · 출발하기";
        else if (IsNear(guild, flow.townGuildRange)) prompt = "[E] 회수 의뢰 수락 · 제출";
        else if (IsNear(supplyShop, flow.townShopRange)) prompt = "[E] 거래소 · 전리품 판매 · 보급 도구 구매";
        if (prompt != null)
        {
            Rect promptRect = new Rect(GameGuiScope.Width * .5f - 245f, GameGuiScope.Height - 94f, 490f, 40f);
            DrawPanel(promptRect, new Color(1f, .94f, .74f, .74f));
            GUI.color = Color.white;
            GUI.Label(promptRect, prompt);
        }
    }

    private bool IsNear(Transform target, float range) => player != null && target != null && Vector2.Distance(player.position, target.position) < range;

    private void DrawFacilityLabel(Transform target, string label, float interactionRange)
    {
        // 시설에 접근할 때만 작은 이름표를 띄운다. 멀리 있는 건물에는 상시 패널을 덮지 않는다.
        if (target == null || Camera.main == null || !IsNear(target, interactionRange + .65f)) return;
        // 세워진 건물의 발판에 표시한다. 평면 화면의 아래쪽 오프셋을 쓰면 고양이 위를 덮는다.
        Vector3 anchor = ThreeDPresentationConfig.SpritesEnabled
            ? ThreeDWorldView.Project(target.position, .2f)
            : ThreeDWorldView.Project((Vector2)target.position + Vector2.down * 1.4f);
        Vector3 screen = Camera.main.WorldToScreenPoint(anchor);
        Vector3 point = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0));
        Rect rect = new Rect(point.x - 38, point.y, 76, 26);
        DrawPanel(rect, new Color(1f, .98f, .91f, .68f));
        GUI.skin.label.alignment = TextAnchor.MiddleCenter; GUI.skin.label.fontSize = 15; GUI.Label(rect, label);
    }

    private void LateUpdate()
    {
        if (viewportWidth == Screen.width && viewportHeight == Screen.height) return;
        viewportWidth = Screen.width; viewportHeight = Screen.height;
        TownCasualVisuals.RefreshBackground();
    }

    private static void DrawPanel(Rect rect, Color? color = null)
    {
        Color previous = GUI.color;
        GUI.color = color ?? GameUiTheme.Paper;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
