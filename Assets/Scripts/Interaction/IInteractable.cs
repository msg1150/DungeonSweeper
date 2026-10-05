/// <summary>
/// 플레이어가 상호작용할 수 있는 오브젝트가 구현하는 공통 인터페이스입니다.
/// 
/// 이후 시체뿐만 아니라
/// - 탈출구
/// - 특수 게이트
/// - 기타 상호작용 오브젝트
/// 에도 그대로 사용할 수 있습니다.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 실제 상호작용을 실행합니다.
    /// </summary>
    void Interact();

    /// <summary>
    /// UI 등에 표시할 상호작용 이름입니다.
    /// </summary>
    string GetInteractionText();
}