using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>마을에서 던전 입구로 진입하는 최소 허브 흐름.</summary>
public class TownHubController : MonoBehaviour
{
    private Transform player;
    private readonly Vector2 dungeonEntrance = new Vector2(4.8f, 0f);
    private readonly Vector2 guild = new Vector2(-4.3f, 1.5f);
    private readonly Vector2 supplyShop = new Vector2(.2f, 2.3f);
    private string toast;

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerMovement>()?.transform;
        if (player != null) PlayerVisualAnimator.Ensure(player.gameObject);
    }

    private void Update()
    {
        if (player == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (Vector2.Distance(player.position, dungeonEntrance) < 1.5f)
        {
            SceneManager.LoadScene("Dungeon");
            return;
        }
        if (Vector2.Distance(player.position, guild) < 2.1f)
        {
            toast = TownProgress.AcceptContract()
                ? $"의뢰 수락: {TownProgress.ContractTargetName} 회수 시 +{TownProgress.ActiveContractBonus}G"
                : $"진행 중 의뢰: {TownProgress.ContractTargetName} 회수 시 +{TownProgress.ActiveContractBonus}G";
            return;
        }
        if (Vector2.Distance(player.position, supplyShop) < 1.7f)
            toast = TownProgress.TryBuySupplyKit() ? "해체 보급 도구를 구매했습니다." : "보급 도구는 25G입니다. 회수품을 정산하세요.";
    }

    private void OnGUI()
    {
        DrawPanel(new Rect(18, 16, 520, TownProgress.LastRunGold > 0 || TownProgress.HasAcceptedContract ? 108 : 56));
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 20;
        GUI.color = Color.white;
        GUI.Label(new Rect(34, 27, 480, 27), $"마을 금고  {TownProgress.Gold}G    |    해체 보급 도구  {TownProgress.SupplyKits}");
        if (TownProgress.LastRunGold > 0)
            GUI.Label(new Rect(34, 54, 480, 25), TownProgress.LastContractBonus > 0 ? $"최근 정산  +{TownProgress.LastRunGold}G  (의뢰 보너스 +{TownProgress.LastContractBonus}G)" : $"최근 회수 정산  +{TownProgress.LastRunGold}G");
        if (TownProgress.HasAcceptedContract)
            GUI.Label(new Rect(34, TownProgress.LastRunGold > 0 ? 79 : 54, 480, 25), $"진행 의뢰  {TownProgress.ContractTargetName} 회수  |  보너스 +{TownProgress.ActiveContractBonus}G");
        if (!string.IsNullOrEmpty(toast))
        {
            Rect toastRect = new Rect(Screen.width * .5f - 340f, Screen.height * .72f, 680f, 68f);
            DrawPanel(toastRect);
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = 23;
            GUI.color = Color.white;
            GUI.Label(toastRect, toast);
        }
        if (player == null) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 23;
        GUI.color = Color.white;
        string prompt = null;
        if (Vector2.Distance(player.position, dungeonEntrance) < 1.5f) prompt = "[E] 던전으로 출발하기";
        else if (Vector2.Distance(player.position, guild) < 2.1f) prompt = TownProgress.HasAcceptedContract ? "[E] 진행 중 의뢰 확인" : "[E] 회수 의뢰 수락 (목표 보너스 +120G)";
        else if (Vector2.Distance(player.position, supplyShop) < 1.7f) prompt = "[E] 해체 보급 도구 구매 (25G · 던전에서 R로 성공 1회)";
        if (prompt != null)
        {
            Rect promptRect = new Rect(Screen.width * .5f - 260f, Screen.height - 108f, 520f, 58f);
            DrawPanel(promptRect, new Color(.16f, .11f, .04f, .94f));
            GUI.color = new Color(1f, .89f, .38f);
            GUI.Label(promptRect, prompt);
        }
    }

    private static void DrawPanel(Rect rect, Color? color = null)
    {
        Color previous = GUI.color;
        GUI.color = color ?? new Color(.035f, .055f, .09f, .92f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = previous;
    }
}
