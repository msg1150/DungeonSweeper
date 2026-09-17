using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>마을에서 던전 입구로 진입하는 최소 허브 흐름.</summary>
public class TownHubController : MonoBehaviour
{
    private Transform player;
    private readonly Vector2 dungeonEntrance = new Vector2(4.8f, 0f);

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerMovement>()?.transform;
        if (player != null) PlayerVisualAnimator.Ensure(player.gameObject);
    }

    private void Update()
    {
        if (player == null || Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame && Vector2.Distance(player.position, dungeonEntrance) < 1.2f)
            SceneManager.LoadScene("Dungeon");
    }

    private void OnGUI()
    {
        if (player == null || Vector2.Distance(player.position, dungeonEntrance) >= 1.2f) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 18;
        GUI.color = new Color(1f, .87f, .35f);
        GUI.Label(new Rect(Screen.width * .5f - 180f, Screen.height - 70f, 360f, 28f), "[E] 던전으로 출발하기");
    }
}
