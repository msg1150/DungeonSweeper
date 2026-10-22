using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>시체 해체·회수품 배치·탈출·사망의 상호작용을 처리한다.</summary>
public sealed partial class DungeonRunController
{
    private void TryInteract()
    {
        if (IsPlayerHidden) { ExitHiding(); return; }
        int stair = NearbyStair();
        if (stair != 0) { if (!TryTravelFloor(stair, out string error)) Say(error); return; }
        if (TryGetNearbyHidingSpot(out _)) { if (!TryHide(out string error)) Say(error); return; }
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
        else Say("시체·은신처·탈출 지점 가까이에서 [E]를 누르세요.");
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
        if (IsPlayerHidden) return "은신 중 · [E] 은신처에서 나오기";
        int stair = NearbyStair();
        if (stair != 0) return stair > 0 ? "[E] 아래층으로 내려가기" : "[E] 위층으로 올라가기";
        if (TryGetNearbyHidingSpot(out _)) return "[E] 은신처에 숨기 · 가까운 몬스터에게는 발각됩니다";
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
}
