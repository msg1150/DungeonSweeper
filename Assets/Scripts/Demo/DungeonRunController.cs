using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>한 판의 상태와 시스템 간 흐름만 조정한다.</summary>
public class DungeonRunController : MonoBehaviour
{
    public static DungeonRunController Instance { get; private set; }

    private readonly List<CorpseRunData> corpses = new();
    private readonly GridInventory inventory = new();
    private readonly List<LootDefinition> pendingLoot = new();
    private PlayerMovement movement;
    private Rigidbody2D playerBody;
    private Transform player;
    private Vector2 entrance;
    private Vector2 specialGate;
    private CorpseRunData activeCorpse;
    private DismantleSession dismantleSession;
    private string toast;
    private float toastTimer;
    private bool hasEscaped;
    private bool lootPlacementOpen;

    public GridInventory Inventory => inventory;
    public DismantleSession ActiveSession => dismantleSession;
    public string ActiveCorpseName => activeCorpse?.Name;
    public string Toast => toast;
    public bool HasEscaped => hasEscaped;
    public Transform Player => player;
    public IReadOnlyList<LootDefinition> PendingLootItems => pendingLoot;
    public LootDefinition PendingLoot => pendingLoot.Count > 0 ? pendingLoot[0] : null;
    public bool IsLootPlacementOpen => lootPlacementOpen;

    private void Awake()
    {
        Instance = this;
        FindExistingPlayer();
        BuildDemoWorld();

        DungeonDemoHud hud = new GameObject("Dungeon Demo HUD").AddComponent<DungeonDemoHud>();
        hud.Initialize(this);
        Say("입구입니다. 시체를 해체하고 전리품을 가방에 배치해 회수하세요.", 6f);
    }

    private void Update()
    {
        if (toastTimer > 0f) toastTimer -= Time.deltaTime;
        if (Keyboard.current == null) return;
        if (hasEscaped)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
                SceneManager.LoadScene("Town");
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

    private void FindExistingPlayer()
    {
        movement = FindAnyObjectByType<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("Dungeon_Test requires its existing PlayerMovement component.");
            enabled = false;
            return;
        }

        player = movement.transform;
        PlayerVisualAnimator.Ensure(player.gameObject);
        playerBody = player.GetComponent<Rigidbody2D>();
        entrance = player.position;

        PlayerInteractor oldInteractor = player.GetComponent<PlayerInteractor>();
        if (oldInteractor != null) oldInteractor.enabled = false;
        DismantleController oldDismantle = FindAnyObjectByType<DismantleController>();
        if (oldDismantle != null) oldDismantle.enabled = false;
    }

    private void BuildDemoWorld()
    {
        DungeonWorldFactory factory = new DungeonWorldFactory();
        DismantleDifficulty standard = new DismantleDifficulty(3, 3, .95f, .22f);

        CorpseInteractable existing = FindAnyObjectByType<CorpseInteractable>();
        if (existing != null)
        {
            existing.enabled = false;
            corpses.Add(new CorpseRunData(existing.gameObject, "고블린 시체", standard,
                new LootDefinition("고블린 이빨", 1, 1, 20, LootShape.Tooth),
                new LootDefinition("낡은 단검", 1, 3, 75, LootShape.Dagger)));
        }
        else
        {
            corpses.Add(factory.CreateCorpse("고블린 시체", new Vector2(-1.7f, 1.1f), standard,
                new LootDefinition("고블린 이빨", 1, 1, 20, LootShape.Tooth),
                new LootDefinition("낡은 단검", 1, 3, 75, LootShape.Dagger)));
        }

        corpses.Add(factory.CreateCorpse("슬라임 시체", new Vector2(2.1f, 2.7f), standard,
            new LootDefinition("슬라임 젤", 1, 2, 40, LootShape.Gel),
            new LootDefinition("슬라임 핵", 1, 1, 90, LootShape.Core)));
        corpses.Add(factory.CreateCorpse("오우거 시체", new Vector2(5.5f, -2.5f), new DismantleDifficulty(4, 3, 1.12f, .16f),
            LootDefinition.CreateShaped("두꺼운 가죽", 130, LootShape.Hide,
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1)),
            new LootDefinition("오우거 뿔", 1, 3, 160, LootShape.Horn)));

        factory.CreateExitMarker("입구", entrance + new Vector2(-.7f, 0f), new Color(.25f, .55f, 1f), false);
        specialGate = new Vector2(6.8f, 2.7f);
        factory.CreateExitMarker("특수 탈출 게이트", specialGate, new Color(.75f, .3f, 1f), true);

    }

    private void TryInteract()
    {
        CorpseRunData corpse = FindNearbyCorpse();
        if (corpse != null)
        {
            activeCorpse = corpse;
            dismantleSession = new DismantleSession(corpse.Difficulty);
            movement.SetMovementEnabled(false);
            Say($"{corpse.Name} 해체 시작. 초록색 영역에서 [E]를 누르세요.");
            return;
        }

        if (Vector2.Distance(player.position, entrance) < 1.15f) FinishRun("입구로 돌아와 탈출했습니다.");
        else if (Vector2.Distance(player.position, specialGate) < 1.15f) FinishRun("특수 게이트를 찾아 탈출했습니다.");
        else Say("시체 또는 탈출 지점 가까이에서 [E]를 누르세요.");
    }

    private CorpseRunData FindNearbyCorpse()
    {
        foreach (CorpseRunData corpse in corpses)
            if (!corpse.IsProcessed && Vector2.Distance(player.position, corpse.Visual.transform.position) < 1.25f)
                return corpse;
        return null;
    }

    private void UpdateDismantling()
    {
        dismantleSession.Tick(Time.deltaTime);
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            EndDismantling("작업을 중단했습니다.");
            return;
        }
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;

        bool success = dismantleSession.IsInSuccessWindow;
        dismantleSession.RegisterAttempt();
        if (dismantleSession.IsComplete)
        {
            foreach (LootDefinition loot in activeCorpse.Loot)
                pendingLoot.Add(loot);
            activeCorpse.MarkProcessed();
            dismantleSession = null;
            activeCorpse = null;
            OpenLootPlacement();
        }
        else if (dismantleSession.IsDestroyed)
        {
            activeCorpse.MarkProcessed();
            EndDismantling("시체가 완전히 훼손되어 전리품을 잃었습니다.");
        }
        else
        {
            Say(success ? $"정확한 해체! 성공 {dismantleSession.Successes}/{dismantleSession.Difficulty.RequiredSuccesses}" : $"훼손 발생! 실패 {dismantleSession.Failures}/{dismantleSession.Difficulty.MaxFailures}");
        }
    }

    private void EndDismantling(string message)
    {
        dismantleSession = null;
        activeCorpse = null;
        movement.SetMovementEnabled(true);
        Say(message);
    }

    public void NotifyPlayerCaught()
    {
        if (hasEscaped) return;
        if (dismantleSession != null) EndDismantling("몬스터가 접근해 작업을 취소했습니다!");
        player.position = entrance + new Vector2(.7f, 0f);
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        Say("발각됨! 공격할 수 없으니 대시로 거리를 벌리세요.");
    }

    public string GetContextPrompt()
    {
        if (hasEscaped || dismantleSession != null) return null;
        CorpseRunData corpse = FindNearbyCorpse();
        if (corpse != null) return $"[E] {corpse.Name} 해체";
        if (Vector2.Distance(player.position, entrance) < 1.15f) return "[E] 입구로 탈출 (현재 전리품 확정)";
        if (Vector2.Distance(player.position, specialGate) < 1.15f) return "[E] 특수 게이트로 탈출";
        return null;
    }

    public bool TryPlacePendingLoot(int lootIndex, int gridX, int gridY, bool rotated)
    {
        if (lootIndex < 0 || lootIndex >= pendingLoot.Count) return false;
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
        if (lootIndex < 0 || lootIndex >= pendingLoot.Count) return;
        LootDefinition loot = pendingLoot[lootIndex];
        pendingLoot.RemoveAt(lootIndex);
        if (pendingLoot.Count == 0) CloseLootPlacement($"{loot.Name}을(를) 포기했습니다.");
    }

    public bool TryMoveStoredLoot(int itemId, int gridX, int gridY)
    {
        if (!inventory.TryMove(itemId, gridX, gridY))
        {
            Say("그 위치에는 전리품을 옮길 수 없습니다.");
            return false;
        }
        return true;
    }

    private void FinishRun(string message)
    {
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

    private void CloseLootPlacement(string message)
    {
        lootPlacementOpen = false;
        Time.timeScale = 1f;
        movement.SetMovementEnabled(true);
        Say(message);
    }

    private void OnDestroy()
    {
        if (lootPlacementOpen) Time.timeScale = 1f;
    }

    private void Say(string message, float duration = 4.5f)
    {
        toast = message;
        toastTimer = duration;
    }
}
