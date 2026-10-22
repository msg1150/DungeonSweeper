using UnityEngine;

/// <summary>은신 진입·발각·물리 상태 복구를 담당한다.</summary>
public sealed partial class DungeonRunController
{
    private bool TryGetNearbyHidingSpot(out Vector2Int cell)
    {
        cell = default;
        var map = DungeonLayoutFactory.ActiveMap;
        if (player == null || map?.hidingSpots == null) return false;
        float closest = HidingInteractionRange * HidingInteractionRange;
        bool found = false;
        foreach (var candidate in map.hidingSpots)
        {
            float distance = ((Vector2)player.position - map.Center(candidate)).sqrMagnitude;
            if (distance > closest) continue;
            closest = distance; cell = candidate; found = true;
        }
        return found;
    }

    public bool TryHide(out string error)
    {
        error = null;
        if (!IsRunActive || IsPlayerHidden || GameSession.IsLoading || GameShell.IsGameplayInputBlocked
            || dismantleSession != null || lootPlacementOpen || !TryGetNearbyHidingSpot(out var cell))
        { error = "작업을 끝내고 은신처 가까이에서 숨으세요."; return false; }
        EnemyAgent witness = null;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.CanRevealHidingAt(player.position)) continue;
            enemy.NoticeHidingAttempt(); witness ??= enemy;
        }
        // 숨는 순간 거리로 판정한다. 한 마리에게라도 들켰다면 플레이어는 계속 노출된다.
        if (witness != null)
        { error = $"{witness.DisplayName}에게 숨는 모습을 발각당했습니다! 더 거리를 벌리세요."; return false; }
        hidingCell = cell;
        SetPlayerHidden(true);
        Say("은신했습니다. [E]를 누르면 나옵니다.");
        GameSession.RequestAutosave();
        return true;
    }

    public void ExitHiding()
    {
        if (!IsPlayerHidden) return;
        SetPlayerHidden(false);
        Say("은신처에서 나왔습니다.");
        GameSession.RequestAutosave();
    }

    private void SetPlayerHidden(bool hidden)
    {
        if (IsPlayerHidden == hidden) return;
        if (hidden)
        {
            hidingPosition = player.position;
            if (playerBody != null)
            {
                bodySimulatedBeforeHiding = playerBody.simulated;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
                playerBody.position = hidingPosition;
                // 입력만 막으면 충돌 해결 과정에서 밀린다. 은신 중에는 몬스터의 통행도 막지 않는다.
                playerBody.simulated = false;
            }
        }
        else RestoreHidingPhysics();
        IsPlayerHidden = hidden;
        if (!hidden) hidingCell = default;
        movement.SetMovementEnabled(!hidden);
        if (playerSprites != null) foreach (var sprite in playerSprites) if (sprite != null) sprite.forceRenderingOff = hidden;
        if (hidden) foreach (var enemy in enemies) if (enemy != null) enemy.ForgetHiddenPlayer();
        player.GetComponent<SpriteDepthView>()?.Sync();
    }

    private void RestoreHidingPhysics()
    {
        if (!IsPlayerHidden) return;
        if (player != null) player.position = hidingPosition;
        if (playerBody == null) return;
        playerBody.position = hidingPosition;
        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.simulated = bodySimulatedBeforeHiding;
    }

    private void CreateHidingMarkers()
    {
        var map = DungeonLayoutFactory.ActiveMap;
        if (map?.hidingSpots == null) return;
        foreach (var cell in map.hidingSpots)
        {
            var marker = new GameObject("은신처"); marker.transform.position = map.Center(cell);
            marker.transform.localScale = new Vector3(.85f, .85f, 1f);
            var sprite = marker.AddComponent<SpriteRenderer>(); sprite.sprite = CasualArtLibrary.WhiteSprite;
            sprite.color = new Color(.15f, .65f, .6f); sprite.sortingOrder = 2;
        }
    }
}
