using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>마을에서 던전 입구로 진입하는 최소 허브 흐름.</summary>
public class TownHubController : MonoBehaviour
{
    private Transform player;
    private Transform dungeonEntrance;
    private Transform guild;
    private Transform supplyShop;
    private Transform warehouse;
    private TownCommercePanel commerce;
    private string toast;
    private float toastTimer;
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
        if (player != null && GameSession.TakeTownPosition(out Vector2 restoredPosition))
        {
            player.position = restoredPosition;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = restoredPosition;
        }
    }

    private void Update()
    {
        if (toastTimer > 0f) toastTimer -= Time.unscaledDeltaTime;
        if (GameShell.IsGameplayInputBlocked || player == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        GameFlowConfig flow = GameFlowConfig.Active;
        if (IsNear(warehouse, flow.townShopRange)) { commerce.Open(TownFacility.Warehouse); return; }
        if (IsNear(dungeonEntrance, flow.townGateRange))
        {
            if (!GameSession.EnterDungeon(out string error)) Say(error);
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
        bool hasDetail = TownProgress.HasAcceptedContract || TownProgress.LastRecoveredCount > 0;
        DrawPanel(new Rect(18, 16, 390, hasDetail ? 78 : 54), new Color(1f, .98f, .91f, .64f));
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 16;
        GUI.color = Color.white;
        GUI.Label(new Rect(30, 22, 368, 24), $"금고 {TownProgress.Gold}G   |   보급 도구 {TownProgress.SupplyKits}");
        GUI.Label(new Rect(30, 45, 368, 24), $"창고 {TownProgress.WarehouseCount}개   |   가방 {TownProgress.BagSize.x} × {TownProgress.BagSize.y}");
        if (TownProgress.HasAcceptedContract)
            GUI.Label(new Rect(30, 68, 368, 24), $"의뢰 {TownProgress.ContractTargetName} 1개  |  +{TownProgress.ActiveContractBonus}G");
        else if (TownProgress.LastRecoveredCount > 0)
            GUI.Label(new Rect(30, 68, 368, 24), $"최근 회수 {TownProgress.LastRecoveredCount}개 · 창고 보관 완료");
        GameFlowConfig flow = GameFlowConfig.Active;
        DrawFacilityLabel(warehouse, "창고", flow.townShopRange);
        DrawFacilityLabel(supplyShop, "거래소", flow.townShopRange);
        DrawFacilityLabel(guild, "길드", flow.townGuildRange);
        DrawFacilityLabel(dungeonEntrance, "던전", flow.townGateRange);
        if (toastTimer > 0f && !string.IsNullOrEmpty(toast))
        {
            Rect toastRect = new Rect(GameGuiScope.Width * .5f - 340f, GameGuiScope.Height * .72f, 680f, 68f);
            DrawPanel(toastRect);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = 23;
            GUI.color = Color.white;
            GUI.Label(toastRect, toast);
        }
        if (player == null) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 18;
        GUI.color = Color.white;
        string prompt = null;
        if (IsNear(warehouse, flow.townShopRange)) prompt = "[E] 창고 · 가방 확장";
        else if (IsNear(dungeonEntrance, flow.townGateRange)) prompt = "[E] 던전으로 출발하기";
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
        Vector3 screen = Camera.main.WorldToScreenPoint(target.position + Vector3.down * 1.4f);
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

    private void Say(string message)
    {
        toast = message;
        toastTimer = 4.5f;
    }

    private static void DrawPanel(Rect rect, Color? color = null)
    {
        Color previous = GUI.color;
        GUI.color = color ?? GameUiTheme.Paper;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
