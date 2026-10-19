using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum TownFacility { Warehouse, Market, Upgrades, Guild, Dungeons }

/// <summary>마을 시설과 던전 선택창의 일시정지·입력 차단·닫기를 공유한다.</summary>
public sealed class TownCommercePanel : MonoBehaviour
{
    private static TownCommercePanel instance;
    private bool open;
    private TownFacility facility;
    private float previousScale;
    private string selectedKind, filter = "", notice;
    private int sort;
    private Vector2 listScroll, detailScroll;
    private static readonly string[] sortLabels = { "이름순", "수량순", "가격순" };
    private List<WarehouseGroup> cachedGroups;
    private List<WarehouseStackData> cachedStacks;
    private uint groupRevision, stackRevision;
    private string cachedFilter, cachedStackKind;
    private int cachedSort;
    private readonly List<DungeonChoice> dungeonChoices = new();
    private int selectedDungeon = -1;
    private GUIStyle dungeonDescriptionStyle;
    private sealed class DungeonChoice
    {
        public DungeonDefinition Definition;
        public string Error;
    }
    public static bool IsOpen => instance != null && instance.open;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetRuntimeState() => instance = null;
    private void Awake() => instance = this;

    public void Open(TownFacility target)
    {
        if (GameSession.IsLoading || GameShell.IsMenuOpen) return;
        dungeonChoices.Clear(); selectedDungeon = -1;
        if (target == TownFacility.Dungeons) RefreshDungeons();
        if (!open) previousScale = Time.timeScale;
        facility = target; open = true; Time.timeScale = 0f;
        listScroll = detailScroll = Vector2.zero; notice = null;
    }
    public void Close()
    {
        if (!open) return;
        open = false; dungeonChoices.Clear(); selectedDungeon = -1;
        if (!GameSession.IsLoading && gameObject.scene == SceneManager.GetActiveScene()) Time.timeScale = previousScale;
        GameShell.ConsumeGameplayEscape();
    }
    private void OnDisable() => Close();
    private void OnDestroy() { if (instance == this) instance = null; }
    private void Update()
    {
        if (open && !GameShell.IsMenuOpen && !GameShell.IsFocusInputBlocked && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
    private static void Panel(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
    private static void Text(Rect rect, string value, int size = 17)
    {
        GUI.skin.label.fontSize = size; GUI.skin.label.alignment = TextAnchor.UpperLeft; GUI.Label(rect, value);
    }
    private void OnGUI()
    {
        if (!open || GameShell.IsMenuOpen) return;
        using var gui = new GameGuiScope(true);
        GUI.enabled &= !GameShell.IsFocusInputBlocked;
        GUI.skin.button.fontSize = 16;
        Panel(new Rect(0, 0, 1280, 720), GameUiTheme.Scrim);
        Panel(new Rect(150, 64, 980, 592), GameUiTheme.Paper);
        string title = facility switch { TownFacility.Dungeons => "던전 선택", TownFacility.Market => "거래소 · 보급 상점", TownFacility.Guild => "회수 길드", TownFacility.Upgrades => "가방 확장", _ => "마을 창고" };
        Text(new Rect(176, 80, 800, 38), title, 27);
        if (GUI.Button(new Rect(1002, 82, 102, 34), "닫기 [Esc]")) { Close(); return; }
        if (facility == TownFacility.Dungeons)
        {
            DrawDungeonSelection();
            return;
        }
        Vector2Int size = TownProgress.BagSize;
        Text(new Rect(176, 124, 900, 28), $"보유 {TownProgress.Gold}G    |    창고 {TownProgress.WarehouseCount}개 · 용량 제한 없음    |    가방 {size.x} × {size.y}", 16);
        if (facility == TownFacility.Warehouse || facility == TownFacility.Upgrades)
        {
            if (GUI.Button(new Rect(176, 158, 160, 32), "보관 전리품")) { facility = TownFacility.Warehouse; notice = null; }
            if (GUI.Button(new Rect(346, 158, 160, 32), "가방 확장")) { facility = TownFacility.Upgrades; notice = null; }
        }
        if (facility == TownFacility.Upgrades) DrawUpgrade();
        else if (facility == TownFacility.Guild) DrawGuild();
        else DrawWarehouse(facility == TownFacility.Market);
        if (!string.IsNullOrEmpty(notice)) Text(new Rect(176, 604, 924, 40), notice, 15);
    }

    private void RefreshDungeons()
    {
        var catalog = DungeonCatalog.Active;
        if (catalog?.dungeons == null) return;
        var seen = new HashSet<DungeonDefinition>();
        foreach (var dungeon in catalog.dungeons)
        {
            if (dungeon == null || !seen.Add(dungeon)) continue;
            // 도면 연결 검증은 창을 열 때 한 번 수행한다. OnGUI마다 BFS를 반복하지 않는다.
            bool valid = catalog.ValidateEntry(dungeon, out string error);
            dungeonChoices.Add(new DungeonChoice { Definition = dungeon, Error = valid ? null : error ?? "던전 설정을 확인하세요." });
        }
    }

    private void DrawDungeonSelection()
    {
        Text(new Rect(176, 121, 924, 26), "탐험할 던전을 고른 뒤 입장하세요.", 16);
        if (dungeonChoices.Count == 0)
        {
            Text(new Rect(176, 188, 924, 60), "현재 입장할 수 있는 던전이 없습니다.", 21);
            return;
        }
        const int columns = 3;
        const float strideX = 304f, strideY = 200f, cardWidth = 292f, cardHeight = 188f;
        int rows = (dungeonChoices.Count + columns - 1) / columns;
        Rect viewport = new Rect(176, 155, 928, 378);
        listScroll = GUI.BeginScrollView(viewport, listScroll, new Rect(0, 0, 908, rows * strideY));
        try
        {
            for (int i = 0; i < dungeonChoices.Count; i++)
            {
                float y = (i / columns) * strideY;
                // 모든 카드는 고정 크기이며 화면 밖 카드는 그리지 않는다.
                if (y + cardHeight < listScroll.y || y > listScroll.y + viewport.height) continue;
                var choice = dungeonChoices[i]; var dungeon = choice.Definition;
                if (dungeon == null) continue;
                Rect card = new Rect((i % columns) * strideX, y, cardWidth, cardHeight);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) { selectedDungeon = i; notice = null; }
                Panel(card, selectedDungeon == i ? GameUiTheme.Mint : GameUiTheme.EmptyCell);
                Rect image = new Rect(card.x + 8, card.y + 8, card.width - 16, 108);
                Panel(image, GameUiTheme.Paper);
                if (dungeon.selectionImage != null)
                    GUI.DrawTexture(image, dungeon.selectionImage, ScaleMode.ScaleToFit, true);
                else CenteredText(image, "탐험 지역", 20);
                CenteredText(new Rect(card.x + 8, card.y + 120, card.width - 16, 28), dungeon.displayName, 20);
                CenteredText(new Rect(card.x + 8, card.y + 151, card.width - 16, 28),
                    $"난이도 · {dungeon.DifficultyLabel}" + (choice.Error != null ? "  ·  입장 불가" : ""), 16);
            }
        }
        finally { GUI.EndScrollView(); }

        var selected = selectedDungeon >= 0 && selectedDungeon < dungeonChoices.Count ? dungeonChoices[selectedDungeon] : null;
        var definition = selected?.Definition;
        if (definition != null)
        {
            int floors = definition.floors != null && definition.floors.Length > 0 ? definition.floors.Length : 1;
            Text(new Rect(176, 544, 720, 28), $"{definition.displayName} · {floors}층 · 난이도 {definition.DifficultyLabel}", 17);
            dungeonDescriptionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, alignment = TextAnchor.UpperLeft };
            GUI.Label(new Rect(176, 575, 720, 60), notice ?? selected.Error ?? definition.selectionDescription, dungeonDescriptionStyle);
        }
        else Text(new Rect(176, 548, 720, 38), "던전을 선택하세요.", 18);
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && definition != null && selected.Error == null;
        bool enter = GUI.Button(new Rect(930, 550, 174, 42), "선택 던전 입장");
        GUI.enabled = enabled;
        // 스크롤 영역을 닫은 뒤 씬을 바꾼다. 입장 시 최신 카탈로그·도면을 다시 검증한다.
        if (enter)
        {
            if (GameSession.EnterDungeon(out string error, definition)) Close();
            else notice = error ?? "던전 입장에 실패했습니다. 던전 설정을 확인하세요.";
        }
    }

    private static void CenteredText(Rect rect, string value, int size)
    {
        GUI.skin.label.fontSize = size;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.Label(rect, value);
    }

    private void DrawWarehouse(bool selling)
    {
        if (selling && GUI.Button(new Rect(618, 548, 472, 35), $"해체 보급 도구 구매 · 25G  (보유 {TownProgress.SupplyKits})"))
            notice = TownProgress.TryBuySupplyKit() ? "해체 보급 도구를 구매했습니다." : "골드가 부족하거나 더 구매할 수 없습니다.";
        Text(new Rect(176, 200, 50, 28), "검색", 15);
        filter = GUI.TextField(new Rect(224, 200, 205, 28), filter, 80);
        if (GUI.Button(new Rect(439, 200, 143, 28), sortLabels[sort])) sort = (sort + 1) % 3;
        var groups = WarehouseGroups();
        if (!groups.Exists(group => group.KindId == selectedKind)) selectedKind = groups.Count > 0 ? groups[0].KindId : null;
        listScroll = GUI.BeginScrollView(new Rect(176, 242, 416, 345), listScroll, new Rect(0, 0, 394, groups.Count * 76));
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (GUI.Button(new Rect(0, i * 76, 394, 68), GUIContent.none)) { selectedKind = group.KindId; detailScroll = Vector2.zero; }
            GUI.color = Color.white;
            if (group.KindId == selectedKind)
                GameUiTheme.Panel(new Rect(0, i * 76 + 6, 4, 53), GameUiTheme.MintPressed);
            Text(new Rect(12, i * 76 + 6, 370, 27), $"{group.Name} × {group.Quantity}" + (group.SaleLocked ? "  [판매 잠금]" : ""));
            GUI.color = Color.white;
            string price = group.MinimumPrice == group.MaximumPrice ? group.MinimumPrice + "G" : $"{group.MinimumPrice}~{group.MaximumPrice}G";
            Text(new Rect(12, i * 76 + 36, 370, 25), "개당 " + price + (IsMaterial(group.KindId) ? " · 가방 확장 재료" : ""), 14);
        }
        GUI.EndScrollView();
        if (groups.Count == 0) { Text(new Rect(618, 250, 472, 95), "보관된 전리품이 없습니다.\n던전에서 회수해 귀환하면 창고에 보관됩니다."); return; }
        var selected = groups.Find(group => group.KindId == selectedKind);
        Text(new Rect(618, 200, 472, 33), selected.Name, 22);
        bool locked = TownProgress.IsSaleLocked(selected.KindId);
        bool nextLocked = GUI.Toggle(new Rect(618, 241, 250, 28), locked, "판매 잠금", GUI.skin.button);
        if (locked != nextLocked) TownProgress.SetSaleLock(selected.KindId, nextLocked);
        Text(new Rect(618, 278, 472, 50), selling ? "가격별로 보관됩니다. 판매할 전리품을 선택하세요." : "거래소에서 판매하거나 가방 확장 재료로 사용하세요.", 14);
        var stacks = WarehouseStacks(selected.KindId);
        detailScroll = GUI.BeginScrollView(new Rect(618, 328, 486, selling ? 205 : 255), detailScroll, new Rect(0, 0, 462, stacks.Count * 61));
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            Text(new Rect(8, i * 61 + 8, 270, 31), $"개당 {stack.unitPrice}G · {stack.quantity}개");
            if (selling)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !TownProgress.IsSaleLocked(selected.KindId);
                bool sale = GUI.Button(new Rect(298, i * 61 + 4, 148, 35), "1개 판매"); GUI.enabled = previousEnabled;
                if (sale) { notice = TownProgress.TrySellOne(stack.id, out string error) ? $"{selected.Name} 1개 판매 · +{stack.unitPrice}G" : error; break; }
            }
        }
        GUI.EndScrollView();
    }
    private List<WarehouseGroup> WarehouseGroups()
    {
        if (cachedGroups != null && groupRevision == TownProgress.WarehouseRevision && cachedFilter == filter && cachedSort == sort) return cachedGroups;
        cachedGroups = TownProgress.GetWarehouseGroups();
        cachedGroups.RemoveAll(group => !string.IsNullOrEmpty(filter) && !(group.Name ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase));
        cachedGroups.Sort((a, b) => sort switch { 1 => b.Quantity.CompareTo(a.Quantity), 2 => b.MaximumPrice.CompareTo(a.MaximumPrice),
            _ => string.Compare(a.Name, b.Name, StringComparison.Ordinal) });
        groupRevision = TownProgress.WarehouseRevision; cachedFilter = filter; cachedSort = sort;
        return cachedGroups;
    }
    private List<WarehouseStackData> WarehouseStacks(string kind)
    {
        if (cachedStacks != null && stackRevision == TownProgress.WarehouseRevision && cachedStackKind == kind) return cachedStacks;
        cachedStacks = TownProgress.GetStacks(kind);
        stackRevision = TownProgress.WarehouseRevision; cachedStackKind = kind;
        return cachedStacks;
    }

    private static bool IsMaterial(string kind)
    {
        foreach (var upgrade in TownEconomyConfig.Active.bagUpgrades)
            foreach (var material in upgrade.materials) if (material.kindId == kind) return true;
        return false;
    }
    private void DrawUpgrade()
    {
        var next = TownProgress.NextBagUpgrade;
        if (next == null) { Text(new Rect(176, 215, 900, 65), "가방 확장을 모두 완료했습니다.", 23); return; }
        Text(new Rect(176, 212, 900, 35), $"{next.displayName} · {next.width} × {next.height}", 24);
        Text(new Rect(176, 258, 900, 30), $"필요 골드 {next.goldCost}G / 보유 {TownProgress.Gold}G");
        var requirements = next.materials;
        detailScroll = GUI.BeginScrollView(new Rect(176, 303, 924, 170), detailScroll, new Rect(0, 0, 900, requirements.Count * 57));
        for (int i = 0; i < requirements.Count; i++)
        {
            var material = requirements[i]; long owned = TownProgress.MaterialCount(material.kindId);
            Text(new Rect(8, i * 57, 870, 29), $"{material.displayName} · 필요 {material.quantity}개 / 보유 {owned}개");
            Text(new Rect(8, i * 57 + 28, 870, 25), $"사용 재료의 판매 가치 합계: {TownProgress.MaterialCostPreview(material.kindId, material.quantity)}G", 14);
        }
        GUI.EndScrollView();
        Text(new Rect(176, 483, 924, 50), "가격이 낮은 재료부터 사용합니다. 판매 잠금 상태의 재료도 업그레이드에는 사용됩니다.", 14);
        bool ready = TownProgress.CanUpgradeBag(out string error);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && ready;
        bool buy = GUI.Button(new Rect(176, 548, 430, 40), $"재료와 {next.goldCost}G를 사용해 가방 확장"); GUI.enabled = previousEnabled;
        if (!ready) Text(new Rect(626, 552, 464, 38), error, 16);
        if (buy) notice = TownProgress.TryUpgradeBag(out error) ? "가방 확장을 완료했습니다. 다음 던전부터 적용됩니다." : error;
    }
    private void DrawGuild()
    {
        if (!TownProgress.HasAcceptedContract)
        {
            Text(new Rect(176, 210, 900, 85), "전리품 회수 의뢰\n요구 전리품 1개를 창고에서 제출하면 보상 120G를 받습니다.", 20);
            if (GUI.Button(new Rect(176, 334, 430, 40), "새 회수 의뢰 수락")) notice = TownProgress.AcceptContract() ? "회수 의뢰를 수락했습니다." : "의뢰를 수락할 수 없습니다.";
            return;
        }
        Text(new Rect(176, 210, 900, 70), $"의뢰 제출품: {TownProgress.ContractTargetName} 1개\n창고 보유: {TownProgress.MaterialCount(TownProgress.ContractKindId)}개 · 보상 120G", 22);
        Text(new Rect(176, 300, 900, 65), "제출한 전리품은 소비됩니다. 가격이 낮은 것부터 사용하며 판매 잠금은 제출을 막지 않습니다.", 16);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && TownProgress.MaterialCount(TownProgress.ContractKindId) >= 1;
        bool submit = GUI.Button(new Rect(176, 394, 430, 40), "창고 전리품 1개 제출 · 보상 받기"); GUI.enabled = previousEnabled;
        if (submit) notice = TownProgress.TrySubmitContract(out string error) ? "의뢰를 완료했습니다. +120G" : error;
    }
}
