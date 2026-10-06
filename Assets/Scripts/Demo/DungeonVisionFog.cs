using UnityEngine;

/// <summary>구 버전 월드 안개 식별용. 실제 시야는 DungeonVisionOverlayRenderer가 담당한다.</summary>
public sealed class DungeonVisionFog : MonoBehaviour
{
    public static void Ensure(Transform player)
    {
        // 호환성용 빈 진입점. 새 안개 오브젝트는 생성하지 않는다.
    }
}
