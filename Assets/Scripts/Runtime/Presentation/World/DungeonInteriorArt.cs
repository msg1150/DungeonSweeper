using System;
using UnityEngine;

[Serializable]
public sealed class DungeonArtRegion
{
    public Texture2D texture;
    [Tooltip("PNG 전체 기준 UV 영역. 원본 픽셀을 바꾸지 않고 투명 여백을 제외합니다.")]
    public Rect uv = new(0, 0, 1, 1);
    public bool IsValid => texture != null && ValueValidation.Finite(uv.position) && ValueValidation.Finite(uv.size)
        && uv.width > 0f && uv.height > 0f && uv.xMin >= 0f && uv.yMin >= 0f && uv.xMax <= 1.00001f && uv.yMax <= 1.00001f;
    public float Aspect => texture.width * uv.width / (texture.height * uv.height);
}

/// <summary>던전 도면에 입히는 원화. 실제 충돌·통행·스폰 좌표와 저장 도면은 바꾸지 않는다.</summary>
[CreateAssetMenu(menuName = "Dungeon Sweeper/Dungeon Interior Art")]
public sealed class DungeonInteriorArt : ScriptableObject
{
    // 한 칸 1.45에서 양쪽 경계벽 0.18과 여유 0.02를 제외한다.
    public const float MaximumPropWidth = 1.25f;
    public Shader surfaceShader;
    public Texture2D dustyFloor, cleanFloor;
    [Range(1, 12)] public float floorRepeatCells = 6f;
    public DungeonArtRegion horizontalTop, verticalTop, cornerTop, junctionTop, endTop, wallFace;
    public DungeonArtRegion portal, hideoutOpen, hideoutClosed, stairs;
    [Tooltip("한 칸 통로 안에 들어가는 종류별 고정 폭. 배치 위치에 따라 크기가 변하지 않습니다.")]
    [Range(.4f, MaximumPropWidth)] public float portalWidth = 1.25f, hideoutWidth = 1.2f, stairWidth = 1.25f;
    private static DungeonInteriorArt active;
    public static DungeonInteriorArt Active => active != null ? active : active = Resources.Load<DungeonInteriorArt>("DungeonInteriorArt");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;
    public bool IsValid() => surfaceShader != null && dustyFloor != null && cleanFloor != null
        && ValueValidation.Positive(floorRepeatCells) && floorRepeatCells <= 12f
        && Valid(horizontalTop) && Valid(verticalTop) && Valid(cornerTop) && Valid(junctionTop) && Valid(endTop) && Valid(wallFace)
        && Valid(portal) && Valid(hideoutOpen) && Valid(hideoutClosed) && Valid(stairs)
        && Width(portalWidth) && Width(hideoutWidth) && Width(stairWidth);
    private static bool Valid(DungeonArtRegion region) => region != null && region.IsValid;
    private static bool Width(float value) => ValueValidation.Finite(value) && value >= .4f && value <= MaximumPropWidth;

    // 서·동·남·북 이웃 비트. 16가지 연결이 모두 같은 분류 규칙을 사용한다.
    public DungeonArtRegion WallTop(int neighbors)
    {
        neighbors &= 15;
        int count = ((neighbors & 1) != 0 ? 1 : 0) + ((neighbors & 2) != 0 ? 1 : 0)
            + ((neighbors & 4) != 0 ? 1 : 0) + ((neighbors & 8) != 0 ? 1 : 0);
        if (count >= 3) return junctionTop;
        if (count < 2) return endTop;
        return neighbors == 3 ? horizontalTop : neighbors == 12 ? verticalTop : cornerTop;
    }
}
