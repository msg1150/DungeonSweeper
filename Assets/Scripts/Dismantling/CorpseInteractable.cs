using UnityEngine;

/// <summary>
/// 던전에 존재하는 몬스터 시체의 상호작용을 담당합니다.
/// 
/// 현재는 플레이어가 상호작용하면
/// 해체 UI를 여는 역할만 수행합니다.
/// </summary>
public class CorpseInteractable : MonoBehaviour, IInteractable
{
    [Header("Corpse")]
    [SerializeField]
    private string corpseName = "고블린 시체";

    public void Interact()
    {
        if (DismantleController.Instance == null)
            return;

        DismantleController.Instance.OpenDismantle();
    }

    public string GetInteractionText()
    {
        return $"{corpseName} 해체";
    }
}