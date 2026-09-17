using UnityEngine;

/// <summary>데모의 상태를 그리는 표시 계층. 게임 규칙을 변경하지 않는다.</summary>
public class DungeonDemoHud : MonoBehaviour
{
    private DungeonRunController run;
    private readonly Color mint = new(.2f, .9f, .62f);
    private readonly Color danger = new(.92f, .27f, .3f);

    public void Initialize(DungeonRunController controller) => run = controller;

    private void OnGUI()
    {
        if (run == null) return;
        DrawHeader();
        DrawInventory();
        DrawContextPrompt();
        DrawToast();
        if (run.ActiveSession != null) DrawSkillCheck(run.ActiveSession);
        if (run.HasEscaped) DrawResult();
    }

    private void DrawHeader()
    {
        GUI.skin.label.fontSize = 17;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 14, 740, 25), "DUNGEON SWEEPER  ·  비전투 회수 작업 데모");
        GUI.Label(new Rect(16, 39, 740, 24), $"회수 가치 {run.Inventory.TotalValue}G    |    [WASD] 이동 / [Space] 대시 / [E] 상호작용");
    }

    private void DrawInventory()
    {
        const float cell = 24f;
        float x = Screen.width - 160f;
        float y = 18f;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 145f, 23f), "작업 가방  5 × 4");
        for (int row = 0; row < GridInventory.Height; row++)
        for (int col = 0; col < GridInventory.Width; col++)
        {
            int id = run.Inventory.GetCell(col, row);
            GUI.color = id == 0 ? new Color(.12f, .15f, .2f) : LootColor(id);
            GUI.DrawTexture(new Rect(x + col * cell, y + 25f + row * cell, cell - 2f, cell - 2f), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 12;
        GUI.Label(new Rect(x, y + 126, 150f, 55f), "아이템 크기별로 정리됩니다.\n가방이 가득 차면 전리품을 포기해야 합니다.");
    }

    private void DrawContextPrompt()
    {
        string prompt = run.GetContextPrompt();
        if (string.IsNullOrEmpty(prompt)) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.color = new Color(1f, .86f, .35f);
        GUI.Label(new Rect(Screen.width * .5f - 230f, Screen.height - 78, 460f, 26f), prompt);
    }

    private void DrawToast()
    {
        if (string.IsNullOrEmpty(run.Toast)) return;
        GUI.color = new Color(.85f, .96f, 1f);
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(16, Screen.height - 42, Screen.width - 32, 28), run.Toast);
    }

    private void DrawSkillCheck(DismantleSession session)
    {
        float width = 560f;
        float x = (Screen.width - width) * .5f;
        float y = Screen.height - 220f;
        GUI.color = new Color(.025f, .04f, .07f, .96f);
        GUI.Box(new Rect(x, y, width, 180), string.Empty);
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 19;
        GUI.Label(new Rect(x, y + 14, width, 28), $"{run.ActiveCorpseName} 해체  ·  성공 {session.Successes}/{session.Difficulty.RequiredSuccesses}  ·  훼손 {session.Failures}/{session.Difficulty.MaxFailures}");
        GUI.skin.label.fontSize = 14;
        GUI.Label(new Rect(x, y + 45, width, 24), "포인터가 초록색 영역에 있을 때 [E]  ·  몬스터는 멈추지 않습니다");
        Rect bar = new Rect(x + 42, y + 91, width - 84, 28);
        GUI.color = new Color(.4f, .09f, .11f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = mint; GUI.DrawTexture(new Rect(bar.x + bar.width * session.WindowStart, bar.y, bar.width * session.Difficulty.SuccessWindowSize, bar.height), Texture2D.whiteTexture);
        GUI.color = danger; GUI.DrawTexture(new Rect(bar.x + bar.width * session.PointerPosition - 3f, bar.y - 7f, 6f, bar.height + 14f), Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.skin.label.fontSize = 13;
        GUI.Label(new Rect(x, y + 133, width, 22), "[ESC] 작업 취소");
    }

    private void DrawResult()
    {
        GUI.color = new Color(.02f, .04f, .07f, .94f);
        GUI.Box(new Rect(Screen.width * .5f - 250f, Screen.height * .5f - 95f, 500f, 190f), string.Empty);
        GUI.color = mint;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 29;
        GUI.Label(new Rect(Screen.width * .5f - 230f, Screen.height * .5f - 56f, 460f, 40f), "RUN COMPLETE");
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(Screen.width * .5f - 225f, Screen.height * .5f - 5f, 450f, 54f), $"{run.Inventory.TotalValue}G 상당의 전리품을 회수했습니다.\n더 깊이 탐험할수록 위험·수익·가방 공간의 판단이 커집니다.");
    }

    private static Color LootColor(int id) => Color.HSVToRGB((id * .19f) % 1f, .62f, .9f);
}
