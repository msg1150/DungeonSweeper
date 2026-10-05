using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 주변에서 가장 가까운 상호작용 오브젝트를 찾아
/// E 키 입력 시 상호작용을 실행합니다.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField]
    private float interactionRadius = 1.2f;

    [SerializeField]
    private LayerMask interactableLayer;

    private void Update()
    {
        if (GameShell.IsGameplayInputBlocked || Time.timeScale <= 0f || Keyboard.current == null)
            return;

        // E키를 눌렀을 때만 주변 오브젝트를 검색합니다.
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Collider2D[] results = Physics2D.OverlapCircleAll(
            transform.position,
            interactionRadius,
            interactableLayer
        );

        IInteractable closestInteractable = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D result in results)
        {
            IInteractable interactable =
                result.GetComponent<IInteractable>();

            if (interactable == null)
                continue;

            float distance = Vector2.Distance(
                transform.position,
                result.transform.position
            );

            // 여러 오브젝트가 범위 안에 있다면
            // 가장 가까운 것을 선택합니다.
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestInteractable = interactable;
            }
        }

        closestInteractable?.Interact();
    }

    /// <summary>
    /// Scene 뷰에서 상호작용 범위를 확인하기 위한 Gizmo입니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
