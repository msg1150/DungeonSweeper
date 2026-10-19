using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>한 판의 상태와 시스템 간 흐름만 조정한다.</summary>
public class DungeonRunController : MonoBehaviour
{
    public static DungeonRunController Instance { get; private set; }
    [Tooltip("Direct scene play override. Town entries use the selected catalog dungeon; saves restore their recorded dungeon.")]
    public DungeonDefinition dungeonDefinition;
    private string activeDungeonId;
    private DungeonExpeditionData expedition;
    public int FloorIndex => expedition?.currentFloor ?? 0;
    public int FloorCount => expedition?.floors.Count ?? 1;
    public bool HasEntrancePortal => FloorIndex == 0;
    public bool HasSpecialPortal => expedition == null || FloorIndex == expedition.exitFloor;

    private readonly List<CorpseRunData> corpses = new();
    private readonly List<EnemyAgent> enemies = new();
    private GridInventory inventory;
    private readonly List<LootDefinition> pendingLoot = new();
    private readonly HashSet<Vector2Int> discoveredCells = new();
    private PlayerMovement movement;
    private Rigidbody2D playerBody;
    private Transform player;
    private PlayerHealth playerHealth;
    private Vector2 entrance;
    private Vector2 specialGate;
    private CorpseRunData activeCorpse;
    private DismantleSession dismantleSession;
    private string toast;
    private float toastTimer;
    private bool hasEscaped;
    private bool isDead;
    private bool lootPlacementOpen;
    private bool actorsReleased;
    private Vector2Int lastDiscoveryCell = new(int.MinValue, int.MinValue);

    public GridInventory Inventory => inventory;
    public DismantleSession ActiveSession => dismantleSession;
    public string ActiveCorpseName => activeCorpse?.Name;
    public string Toast => toastTimer > 0f ? toast : null;
    public bool HasEscaped => hasEscaped;
    public bool IsRunActive => enabled && !actorsReleased && !hasEscaped && !isDead;
    public Transform Player => player;
    public IReadOnlyList<EnemyAgent> Enemies => enemies;
    public IReadOnlyList<LootDefinition> PendingLootItems => pendingLoot;
    public LootDefinition PendingLoot => pendingLoot.Count > 0 ? pendingLoot[0] : null;
    public bool IsLootPlacementOpen => lootPlacementOpen;
    public PlayerHealth PlayerHealth => playerHealth;
    public string DungeonName => (dungeonDefinition != null ? dungeonDefinition.displayName : DungeonLayoutFactory.LayoutName)
        + (expedition != null ? $" · {FloorIndex + 1}/{FloorCount}층 ({expedition.Current.displayName})" : "");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DungeonSaveData saved = GameSession.TakeDungeonSave();
        DungeonDefinition pending = GameSession.TakeDungeonDefinition();
        if (saved == null)
        {
            string error = null;
            dungeonDefinition = pending != null ? pending : dungeonDefinition != null ? dungeonDefinition : DungeonCatalog.Active?.Pick(out error);
            if (DungeonCatalog.Active == null || !DungeonCatalog.Active.ValidateEntry(dungeonDefinition, out error))
            {
                Debug.LogError(error ?? "Register the dungeon in Resources/DungeonCatalog.");
                enabled = false;
                return;
            }
            activeDungeonId = dungeonDefinition.dungeonId;
        }
        else { activeDungeonId = saved.dungeonId; dungeonDefinition = DungeonCatalog.Active?.FindDungeon(activeDungeonId); }
        expedition = saved != null ? saved.ResolvedExpedition : DungeonExpeditionData.Create(dungeonDefinition);
        Vector2Int bagSize = saved == null ? TownProgress.BagSize : new(saved.ResolvedWidth, saved.ResolvedHeight);
        inventory = new GridInventory(bagSize.x, bagSize.y);
        if (!FindExistingPlayer()) return;
        ClearLegacyDungeonGeometry();
        DungeonLayoutFactory.CreateLayout(saved?.ResolvedMap ?? expedition?.Current.map, saved?.layoutIndex ?? dungeonDefinition.layoutIndex);
        entrance = DungeonLayoutFactory.Entrance;
        player.position = entrance;
        if (playerBody != null) playerBody.position = entrance;
        if (saved == null || (expedition != null && !expedition.Current.visited))
        {
            BuildDemoWorld();
            if (saved != null) RestorePlayerAndBag(saved);
        }
        else RestoreWorld(saved);
        CreateStairs();
        DiscoverAroundPlayer();

        ThreeDWorldView.Create(player, true);
        DungeonDemoHud hud = new GameObject("Dungeon Demo HUD").AddComponent<DungeonDemoHud>();
        hud.Initialize(this);
#if UNITY_EDITOR
        hud.gameObject.AddComponent<MonsterPerceptionDebugView>().Initialize(this);
#endif
        Say(TownProgress.HasAcceptedContract ? $"의뢰 목표: {TownProgress.ContractTargetName}을 회수해 마을 길드에 제출하세요." : "입구입니다. 시체를 해체하고 전리품을 가방에 배치해 회수하세요.", 6f);
    }

    private void Update()
    {
        if (toastTimer > 0f) toastTimer -= Time.unscaledDeltaTime;
        if (isDead || GameShell.IsGameplayInputBlocked || Keyboard.current == null) return;
        if (hasEscaped)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (!GameSession.ReturnToTown(out string error)) Say(error, 999f);
            }
            return;
        }

        if (lootPlacementOpen) return;

        if (dismantleSession != null)
        {
            UpdateDismantling();
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
            TryInteract();
    }

    private void LateUpdate() { if (IsRunActive) DiscoverAroundPlayer(); }

    public bool IsCellDiscovered(int x, int y) => discoveredCells.Contains(new Vector2Int(x, y));

    private void DiscoverAroundPlayer()
    {
        if (player == null) return;
        Vector2Int center = DungeonLayoutFactory.WorldToCell(player.position);
        if (center == lastDiscoveryCell) return;
        lastDiscoveryCell = center;
        for (int x = Mathf.Max(0, center.x - 3); x <= Mathf.Min(DungeonLayoutFactory.Width - 1, center.x + 3); x++)
        for (int y = Mathf.Max(0, center.y - 3); y <= Mathf.Min(DungeonLayoutFactory.Height - 1, center.y + 3); y++)
            if (DungeonLayoutFactory.IsWalkableCell(x, y) && Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) <= 3)
                discoveredCells.Add(new Vector2Int(x, y));
    }

    private bool FindExistingPlayer()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("The dungeon scene requires a PlayerMovement component.");
            enabled = false;
            return false;
        }

        player = movement.transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = player.gameObject.AddComponent<PlayerHealth>();
        playerHealth.Initialize();
        PlayerVisualAnimator.Ensure(player.gameObject);
        playerBody = player.GetComponent<Rigidbody2D>();
        DungeonCameraFollow.Ensure(player);
        DungeonVisionOverlayRenderer.Create(player);

        PlayerInteractor oldInteractor = player.GetComponent<PlayerInteractor>();
        if (oldInteractor != null) oldInteractor.enabled = false;
        DismantleController oldDismantle = FindAnyObjectByType<DismantleController>();
        if (oldDismantle != null) oldDismantle.enabled = false;
        return true;
    }

    // Dungeon_Test에 남아 있던 테스트용 벽/배경은 새 런타임 레이아웃과 겹치므로 사용하지 않는다.
    private void ClearLegacyDungeonGeometry()
    {
        foreach (BoxCollider2D collider in FindObjectsByType<BoxCollider2D>())
        {
            if (collider.GetComponentInParent<PlayerMovement>() == null)
                collider.enabled = false;
        }

        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>())
        {
            if (renderer.GetComponentInParent<PlayerMovement>() == null)
                renderer.enabled = false;
        }
    }

    private void BuildDemoWorld()
    {
        DungeonWorldFactory factory = new DungeonWorldFactory();

        CorpseInteractable existing = FindAnyObjectByType<CorpseInteractable>();
        if (existing != null) existing.gameObject.SetActive(false);
        List<MonsterPrefab> monsters = new(); List<CorpsePrefab> corpsePrefabs = new();
        if (expedition == null) { monsters = dungeonDefinition.ExpandMonsters(); corpsePrefabs = dungeonDefinition.ExpandCorpses(); }
        else
        {
            foreach (string id in expedition.Current.monsterIds) monsters.Add(DungeonCatalog.Active.FindMonster(id));
            foreach (string id in expedition.Current.corpseIds) corpsePrefabs.Add(DungeonCatalog.Active.FindCorpse(id));
        }
        DungeonPopulationPlan population = DungeonPopulationPlan.Create(corpsePrefabs.Count, monsters);
        for (int i = 0; i < corpsePrefabs.Count; i++) corpses.Add(corpsePrefabs[i].Spawn(population.Corpses[i]));
        for (int i = 0; i < monsters.Count; i++) enemies.Add(monsters[i].Spawn(player, population.Enemies[i], population.PatrolTargets[i]));

        specialGate = population.Gate;
        CreatePortals(factory);

    }

    private void TryInteract()
    {
        int stair = NearbyStair();
        if (stair != 0) { if (!TryTravelFloor(stair, out string error)) Say(error); return; }
        CorpseRunData corpse = FindNearbyCorpse();
        if (corpse != null)
        {
            activeCorpse = corpse;
            // Cancelling pauses this corpse's work; it does not erase damage or progress.
            dismantleSession = corpse.Session;
            movement.SetMovementEnabled(false);
            Say($"{corpse.Name} 해체 시작. 초록색 영역에서 [E]를 누르세요.");
            return;
        }

        if (HasEntrancePortal && Vector2.Distance(player.position, entrance) < GameFlowConfig.Active.exitRange) FinishRun("입구로 돌아와 탈출했습니다.");
        else if (HasSpecialPortal && Vector2.Distance(player.position, specialGate) < GameFlowConfig.Active.exitRange) FinishRun("특수 게이트를 찾아 탈출했습니다.");
        else Say("시체 또는 탈출 지점 가까이에서 [E]를 누르세요.");
    }

    private CorpseRunData FindNearbyCorpse()
    {
        foreach (CorpseRunData corpse in corpses)
            if (!corpse.IsProcessed && Vector2.Distance(player.position, corpse.Visual.transform.position) < GameFlowConfig.Active.corpseRange)
                return corpse;
        return null;
    }

    private void UpdateDismantling()
    {
        dismantleSession.Tick(Time.deltaTime);
        if (Keyboard.current.rKey.wasPressedThisFrame && TownProgress.TryUseSupplyKit())
        {
            dismantleSession.AddSupplySuccess();
            Say("보급 도구 사용: 해체 성공 1회를 확보했습니다.");
            if (dismantleSession.IsComplete)
            {
                CompleteDismantling();
            }
            return;
        }
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GameShell.ConsumeGameplayEscape();
            EndDismantling("작업을 중단했습니다.");
            return;
        }
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        bool success = dismantleSession.IsInSuccessWindow;
        dismantleSession.RegisterAttempt();
        if (!success)
        {
            AlertNearbyMonsters(activeCorpse.Visual.transform.position);
            Say("해체 소음 발생! 근처 몬스터가 소리를 조사합니다.");
        }
        if (dismantleSession.IsComplete)
        {
            CompleteDismantling();
        }
        else if (dismantleSession.IsDestroyed)
        {
            GameSession.RequestAutosave();
            activeCorpse.MarkProcessed();
            EndDismantling("시체가 완전히 훼손되어 전리품을 잃었습니다.");
        }
        else
        {
            Say(success ? $"정확한 해체! 성공 {dismantleSession.Successes}/{dismantleSession.Difficulty.RequiredSuccesses}" : $"훼손 발생! 실패 {dismantleSession.Failures}/{dismantleSession.Difficulty.MaxFailures}");
        }
    }

    private void CompleteDismantling()
    {
        foreach (LootDefinition loot in activeCorpse.Loot) pendingLoot.Add(loot);
        activeCorpse.MarkProcessed();
        dismantleSession = null;
        activeCorpse = null;
        FinishLootRoll();
    }

    private void EndDismantling(string message)
    {
        dismantleSession = null;
        activeCorpse = null;
        movement.SetMovementEnabled(true);
        Say(message);
    }

    private void AlertNearbyMonsters(Vector2 noisePosition)
    {
        foreach (EnemyAgent enemy in enemies) if (enemy != null) enemy.HearNoise(noisePosition);
    }

    public void HandlePlayerDeath()
    {
        if (!IsRunActive) return;
        isDead = true;
        movement.SetMovementEnabled(false);
        Time.timeScale = 1f;
        pendingLoot.Clear();
        inventory.Clear();
        TownProgress.FailRun();
        if (!GameSession.ReturnToTown(out string error)) Say(error, 999f);
    }

    public string GetContextPrompt()
    {
        if (hasEscaped || dismantleSession != null) return null;
        int stair = NearbyStair();
        if (stair != 0) return stair > 0 ? "[E] 아래층으로 내려가기" : "[E] 위층으로 올라가기";
        CorpseRunData corpse = FindNearbyCorpse();
        if (corpse != null) return $"[E] {corpse.Name} 해체";
        if (HasEntrancePortal && Vector2.Distance(player.position, entrance) < GameFlowConfig.Active.exitRange) return "[E] 입구로 탈출 (현재 전리품 확정)";
        if (HasSpecialPortal && Vector2.Distance(player.position, specialGate) < GameFlowConfig.Active.exitRange) return "[E] 특수 게이트로 탈출";
        return null;
    }

    public bool TryPlacePendingLoot(int lootIndex, int gridX, int gridY, bool rotated)
    {
        if (!IsRunActive || !lootPlacementOpen || lootIndex < 0 || lootIndex >= pendingLoot.Count) return false;
        LootDefinition loot = pendingLoot[lootIndex];
        LootDefinition placement = rotated ? loot.RotatedClockwise() : loot;
        if (!inventory.TryPlace(placement, gridX, gridY))
        {
            Say("그 위치에는 전리품이 들어가지 않습니다.");
            return false;
        }
        pendingLoot.RemoveAt(lootIndex);
        if (pendingLoot.Count == 0) CloseLootPlacement($"{placement.Name}을(를) 가방에 넣었습니다.");
        return true;
    }

    public void DiscardPendingLoot(int lootIndex)
    {
        if (!IsRunActive || !lootPlacementOpen || lootIndex < 0 || lootIndex >= pendingLoot.Count) return;
        LootDefinition loot = pendingLoot[lootIndex];
        pendingLoot.RemoveAt(lootIndex);
        if (pendingLoot.Count == 0) CloseLootPlacement($"{loot.Name}을(를) 포기했습니다.");
    }

    public bool TryMoveStoredLoot(int itemId, int gridX, int gridY)
    {
        if (!IsRunActive) return false;
        if (!inventory.TryMove(itemId, gridX, gridY))
        {
            Say("그 위치에는 전리품을 옮길 수 없습니다.");
            return false;
        }
        return true;
    }

    private void FinishRun(string message)
    {
        if (!IsRunActive) return;
        if (!TownProgress.TryReceiveRun(inventory, out string error)) { Say(error, 999f); return; }
        hasEscaped = true;
        movement.SetMovementEnabled(false);
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        Say(message, 999f);
    }

    private void OpenLootPlacement()
    {
        lootPlacementOpen = true;
        movement.SetMovementEnabled(false);
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        Time.timeScale = 0f;
        Say("회수한 전리품을 가방에 직접 배치하세요.", 999f);
    }

    private void FinishLootRoll()
    {
        GameSession.RequestAutosave();
        if (pendingLoot.Count > 0) OpenLootPlacement();
        else
        {
            movement.SetMovementEnabled(true);
            Say("이번 해체에서는 회수 가능한 전리품이 나오지 않았습니다.");
        }
    }

    private void CloseLootPlacement(string message)
    {
        lootPlacementOpen = false;
        Time.timeScale = 1f;
        movement.SetMovementEnabled(true);
        Say(message);
        GameSession.RequestAutosave();
    }

    public DungeonSaveData CaptureSave()
    {
        DungeonSaveData data = new()
        {
            dungeonId = activeDungeonId, layoutIndex = DungeonLayoutFactory.LayoutIndex, health = playerHealth.Current,
            bagWidth = inventory.Columns, bagHeight = inventory.Rows,
            playerPosition = player.position, specialGate = specialGate,
            activeCorpseIndex = activeCorpse == null ? -1 : corpses.IndexOf(activeCorpse),
            lootPlacementOpen = lootPlacementOpen, inventory = inventory.Capture(), totalValue = inventory.TotalValue,
            discoveredCells = new List<Vector2Int>(discoveredCells), motion = movement.Capture(),
            invulnerabilitySeconds = playerHealth.RemainingInvulnerability
        };
        foreach (CorpseRunData corpse in corpses)
        {
            CorpseSaveData state = new()
            {
                prefabId = corpse.PrefabId, name = corpse.Name, monsterIndex = corpse.MonsterIndex, position = corpse.Visual.transform.position,
                requiredSuccesses = corpse.Difficulty.RequiredSuccesses, maxFailures = corpse.Difficulty.MaxFailures,
                pointerSpeed = corpse.Difficulty.PointerSpeed, windowSize = corpse.Difficulty.SuccessWindowSize,
                processed = corpse.IsProcessed, session = corpse.CaptureSession()
            };
            foreach (LootDefinition loot in corpse.Loot) state.loot.Add(LootSaveData.Capture(loot));
            data.corpses.Add(state);
        }
        foreach (EnemyAgent enemy in enemies) if (enemy != null) data.enemies.Add(enemy.Capture());
        foreach (LootDefinition loot in pendingLoot) data.pendingLoot.Add(LootSaveData.Capture(loot));
        if (expedition != null)
        {
            // 먼저 독립 사본을 만든다. 반환할 현재 층 목록을 실제 탐험에 연결하면
            // 저장 호출자의 편집이 내부 층 상태까지 바꾸게 된다.
            data.expedition = expedition.Copy();
            var current = data.expedition.Current;
            current.visited = true; current.corpses = data.corpses; current.enemies = data.enemies;
            current.discoveredCells = data.discoveredCells; current.specialGate = specialGate;
            data.mapSnapshot = current.map;
            data.hasMapSnapshot = true; data.expeditionVersion = 1;
        }
        return data;
    }

    private void RestoreWorld(DungeonSaveData data)
    {
        DungeonWorldFactory factory = new();
        foreach (CorpseSaveData state in data.corpses)
        {
            CorpseRunData corpse = !string.IsNullOrEmpty(state.prefabId)
                ? DungeonCatalog.Active.FindCorpse(state.prefabId).Spawn(state.position, state)
                : factory.CreateCorpse(state.name, state.position,
                new DismantleDifficulty(state.requiredSuccesses, state.maxFailures, state.pointerSpeed, state.windowSize),
                state.monsterIndex, RestoreLegacyLoot(state));
            corpse.RestoreSession(state.session);
            if (state.processed) corpse.MarkProcessed();
            corpses.Add(corpse);
        }
        foreach (EnemySaveData state in data.enemies)
        {
            EnemyAgent enemy = !string.IsNullOrEmpty(state.prefabId)
                ? DungeonCatalog.Active.FindMonster(state.prefabId).Spawn(player, state.position, state.roamTarget, state.definition)
                : factory.CreateEnemy(state.definition, player, state.position, state.roamTarget);
            enemy.Restore(state);
            enemies.Add(enemy);
        }
        specialGate = data.specialGate;
        CreatePortals(factory);
        RestorePlayerAndBag(data);
    }

    private void RestorePlayerAndBag(DungeonSaveData data)
    {
        inventory.Restore(data.inventory, data.totalValue);
        foreach (LootSaveData loot in data.pendingLoot) pendingLoot.Add(loot.Restore());
        player.position = data.playerPosition;
        if (playerBody != null) { playerBody.position = data.playerPosition; playerBody.linearVelocity = Vector2.zero; }
        playerHealth.Restore(data.health, data.invulnerabilitySeconds);
        movement.Restore(data.motion);
        if (data.discoveredCells != null) foreach (Vector2Int cell in data.discoveredCells) discoveredCells.Add(cell);
        if (data.activeCorpseIndex >= 0)
        {
            activeCorpse = corpses[data.activeCorpseIndex];
            dismantleSession = activeCorpse.Session;
            movement.SetMovementEnabled(false);
        }
        if (data.lootPlacementOpen) OpenLootPlacement();
    }

    private void CreatePortals(DungeonWorldFactory factory)
    {
        if (HasEntrancePortal) factory.CreateExitMarker("입구", entrance, new Color(.25f, .55f, 1f), false);
        if (HasSpecialPortal) factory.CreateExitMarker("특수 탈출 게이트", specialGate, new Color(.75f, .3f, 1f), true);
    }

    private void CreateStairs()
    {
        if (expedition == null) return;
        var map = expedition.Current.map;
        if (map.hasStairsUp) CreateStair(-1, map.Center(map.stairsUp));
        if (map.hasStairsDown) CreateStair(1, map.Center(map.stairsDown));
    }
    private void CreateStair(int direction, Vector2 position)
    {
        var marker = new GameObject(direction > 0 ? "아래층 계단" : "위층 계단");
        marker.transform.position = position; marker.AddComponent<DungeonStairMarker>().direction = direction;
        var sprite = marker.AddComponent<SpriteRenderer>(); sprite.sprite = CasualArtLibrary.WhiteSprite;
        sprite.color = direction > 0 ? new Color(.28f,.75f,.62f) : new Color(.55f,.70f,.95f);
        sprite.sortingOrder = 2; marker.transform.localScale = new Vector3(.8f,.8f,1);
    }
    private int NearbyStair()
    {
        if (expedition == null) return 0;
        var map = expedition.Current.map;
        if (map.hasStairsUp && Vector2.Distance(player.position, map.Center(map.stairsUp)) < .8f) return -1;
        if (map.hasStairsDown && Vector2.Distance(player.position, map.Center(map.stairsDown)) < .8f) return 1;
        return 0;
    }
    public bool TryTravelFloor(int direction, out string error)
    {
        error = null;
        if (!IsRunActive || GameSession.IsLoading || expedition == null || (direction != -1 && direction != 1)
            || dismantleSession != null || lootPlacementOpen || NearbyStair() != direction)
        { error = "계단 가까이에서 작업을 끝낸 뒤 이동하세요."; return false; }
        int targetIndex = FloorIndex + direction;
        if (targetIndex < 0 || targetIndex >= FloorCount) { error = "이동할 층이 없습니다."; return false; }
        var state = CaptureSave(); var journey = state.expedition;
        journey.currentFloor = targetIndex; var target = journey.Current;
        state.mapSnapshot = target.map; state.corpses = target.corpses; state.enemies = target.enemies;
        state.discoveredCells = target.discoveredCells; state.specialGate = target.specialGate;
        state.playerPosition = target.map.Center(direction > 0 ? target.map.stairsUp : target.map.stairsDown);
        state.motion.isDashing = false; state.motion.dashSeconds = 0;
        return GameSession.ChangeDungeonFloor(state, out error);
    }

    private static LootDefinition[] RestoreLegacyLoot(CorpseSaveData state)
    {
        var loot = new LootDefinition[state.loot.Count];
        for (int i = 0; i < loot.Length; i++) loot[i] = state.loot[i].Restore();
        return loot;
    }

    /// <summary>
    /// 저장·귀환 정산을 끝낸 후에만 호출한다. 처리된 시체도 이때까지 위치와 저장 상태를 보존한다.
    /// 씬의 삭제 순서에 의존하지 않도록 정상 이동에서 먼저 반환하고 OnDestroy는 안전망으로 둔다.
    /// </summary>
    public void ReleaseActors()
    {
        if (actorsReleased) return;
        actorsReleased = true;
        foreach (var enemy in enemies) if (enemy != null && !DungeonActorPool.Return(enemy.gameObject)) Destroy(enemy.gameObject);
        foreach (var corpse in corpses) if (corpse.Visual != null && !DungeonActorPool.Return(corpse.Visual)) Destroy(corpse.Visual);
        enemies.Clear(); corpses.Clear(); pendingLoot.Clear();
        activeCorpse = null; dismantleSession = null;
    }

    private void OnDestroy()
    {
        ReleaseActors();
        if (Instance == this)
        {
            if (lootPlacementOpen) Time.timeScale = 1f;
            Instance = null;
        }
    }

    private void Say(string message, float duration = 4.5f)
    {
        toast = message;
        toastTimer = duration;
    }
}
