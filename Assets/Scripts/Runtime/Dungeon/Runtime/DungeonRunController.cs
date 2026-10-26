using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>한 판의 상태와 시스템 간 흐름만 조정한다.</summary>
public sealed partial class DungeonRunController : MonoBehaviour
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
    private Vector2Int hidingCell;
    private Vector2 hidingPosition;
    private bool bodySimulatedBeforeHiding;
    private SpriteRenderer[] playerSprites;
    public bool IsPlayerHidden { get; private set; }
    public const float HidingInteractionRange = .9f;
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
        DungeonLayoutFactory.CreateLayout(saved?.ResolvedMap ?? expedition?.Current.map, saved?.layoutIndex ?? dungeonDefinition.layoutIndex);
        entrance = DungeonLayoutFactory.Entrance;
        player.position = entrance;
        if (playerBody != null) playerBody.position = entrance;
        if (saved == null || (expedition != null && !expedition.Current.visited))
        {
            SpawnFloorActors();
            if (saved != null) RestorePlayerAndBag(saved);
        }
        else RestoreWorld(saved);
        CreateStairs();
        CreateHidingMarkers();
        DiscoverAroundPlayer();

        ThreeDWorldView.Create(player, true);
        DungeonHud hud = new GameObject("Dungeon HUD").AddComponent<DungeonHud>();
        hud.Initialize(this);
#if UNITY_EDITOR
        hud.gameObject.AddComponent<MonsterPerceptionDebugView>().Initialize(this);
#endif
        Say(TownProgress.HasActiveQuest ? $"의뢰 목표: {TownProgress.ActiveQuest.Title} — 요구 재료를 회수해 마을 길드에 제출하세요." : "입구입니다. 시체를 해체하고 전리품을 가방에 배치해 회수하세요.", 6f);
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
    // 탐색은 같은 런에서 추가만 된다. 미니맵 캐시는 새 칸이 발견될 때만 다시 만든다.
    public int DiscoveredCellCount => discoveredCells.Count;

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
        playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        playerBody = player.GetComponent<Rigidbody2D>();
        DungeonCameraFollow.Ensure(player);
        DungeonVisionOverlayRenderer.Create(player, dungeonDefinition);

        return true;
    }

    private void SpawnFloorActors()
    {
        DungeonWorldFactory factory = new DungeonWorldFactory();

        List<MonsterPrefab> monsters = new(); List<CorpsePrefab> corpsePrefabs = new();
        if (expedition == null) { monsters = dungeonDefinition.ExpandMonsters(); corpsePrefabs = dungeonDefinition.ExpandCorpses(); }
        else
        {
            foreach (string id in expedition.Current.monsterIds) monsters.Add(DungeonCatalog.Active.FindMonster(id));
            foreach (string id in expedition.Current.corpseIds) corpsePrefabs.Add(DungeonCatalog.Active.FindCorpse(id));
        }
        DungeonPopulationPlan population = DungeonPopulationPlan.Create(corpsePrefabs.Count, monsters);
        for (int i = 0; i < corpsePrefabs.Count; i++)
        {
            var prefab = corpsePrefabs[i];
            var plan = expedition != null ? expedition.Current.FindLootPlan(prefab.prefabId)
                : dungeonDefinition.lootTable != null ? CorpseLootPlan.Capture(prefab, dungeonDefinition.lootTable) : null;
            corpses.Add(prefab.Spawn(population.Corpses[i], lootPlan: plan));
        }
        for (int i = 0; i < monsters.Count; i++) enemies.Add(monsters[i].Spawn(player, population.Enemies[i], population.PatrolTargets[i]));

        specialGate = population.Gate;
        CreatePortals(factory);

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
        RestoreHidingPhysics();
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
