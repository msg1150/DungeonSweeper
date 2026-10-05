using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 해체 작업의 전체 상태를 관리합니다.
/// 
/// 현재 단계에서는
/// - 해체 UI 열기
/// - 해체 UI 닫기
/// 만 담당합니다.
/// 
/// 다음 단계에서 Skill Check 로직을 여기에 추가합니다.
/// </summary>
public class DismantleController : MonoBehaviour
{
    public static DismantleController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    [Header("UI")]
    [SerializeField]
    private GameObject dismantlePanel;

    [Header("Player")]
    [SerializeField]
    private PlayerMovement playerMovement;

    private bool isDismantling;

    public bool IsDismantling => isDismantling;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dismantlePanel != null)
        {
            dismantlePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isDismantling || GameShell.IsGameplayInputBlocked || Time.timeScale <= 0f)
            return;

        if (Keyboard.current == null)
            return;

        // 해체 작업 취소
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDismantle();
        }
    }

    /// <summary>
    /// 해체 작업을 시작합니다.
    /// </summary>
    public void OpenDismantle()
    {
        if (isDismantling || !isActiveAndEnabled || GameShell.IsGameplayInputBlocked
            || dismantlePanel == null || playerMovement == null)
            return;

        isDismantling = true;

        dismantlePanel.SetActive(true);

        // 해체 중에는 플레이어 이동을 막습니다.
        playerMovement.SetMovementEnabled(false);
    }

    /// <summary>
    /// 해체 작업을 종료합니다.
    /// </summary>
    public void CloseDismantle()
    {
        if (!isDismantling)
            return;

        isDismantling = false;

        if (dismantlePanel != null) dismantlePanel.SetActive(false);

        if (playerMovement != null) playerMovement.SetMovementEnabled(true);
    }

    private void OnDisable() => CloseDismantle();
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
