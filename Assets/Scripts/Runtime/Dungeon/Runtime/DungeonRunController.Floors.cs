using UnityEngine;

/// <summary>포탈·계단 표시와 층 이동을 담당한다.</summary>
public sealed partial class DungeonRunController
{
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
            || IsPlayerHidden || dismantleSession != null || lootPlacementOpen || NearbyStair() != direction)
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
}
