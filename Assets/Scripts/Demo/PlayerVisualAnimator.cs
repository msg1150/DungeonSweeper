using UnityEngine;

/// <summary>플레이어의 물리 이동과 분리된 걷기 시각 효과를 담당한다.</summary>
public sealed class PlayerVisualAnimator : MonoBehaviour
{
    private Rigidbody2D body;
    private Transform visual;
    private SpriteRenderer visualRenderer;
    private Sprite[] walkFrames;
    private Sprite[] dashFrames;
    private PlayerMovement movement;
    private float frameTimer;

    public static void Ensure(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerVisualAnimator>() == null)
            player.AddComponent<PlayerVisualAnimator>();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.DashStarted += RestartAnimation;
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        GameObject visualObject = new GameObject("Animated Player Visual");
        visualObject.transform.SetParent(transform, false);
        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = source.sprite;
        visualRenderer.color = source.color;
        visualRenderer.sortingLayerID = source.sortingLayerID;
        visualRenderer.sortingOrder = source.sortingOrder;
        visualObject.transform.localScale = Vector3.one * profile.visualScale;
        source.enabled = false;

        visual = visualObject.transform;
        walkFrames = CreateWalkFrames();
        dashFrames = CreateFrames(profile.dashSheetResource, profile);
        if (walkFrames.Length > 0) visualRenderer.sprite = walkFrames[0];
    }

    private void RestartAnimation() => frameTimer = 0f;

    private void OnDestroy()
    {
        if (movement != null) movement.DashStarted -= RestartAnimation;
    }

    private void LateUpdate()
    {
        if (visual == null || Time.timeScale <= 0f || GameShell.IsGameplayInputBlocked) return;
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        Vector2 velocity = movement != null ? movement.Velocity : body != null ? body.linearVelocity : Vector2.zero;
        if (velocity.x < -.02f) visualRenderer.flipX = true;
        else if (velocity.x > .02f) visualRenderer.flipX = false;
        if (movement != null && movement.IsDashing && dashFrames != null && dashFrames.Length > 0)
        {
            frameTimer += Time.unscaledDeltaTime;
            visualRenderer.sprite = dashFrames[Mathf.FloorToInt(frameTimer * profile.dashFramesPerSecond) % dashFrames.Length];
            return;
        }
        if (velocity.sqrMagnitude <= .0025f || walkFrames == null || walkFrames.Length == 0)
        {
            if (walkFrames != null && walkFrames.Length > 0) visualRenderer.sprite = walkFrames[0];
            return;
        }

        frameTimer += Time.unscaledDeltaTime;
        int frame = Mathf.FloorToInt(frameTimer * profile.walkFramesPerSecond) % walkFrames.Length;
        visualRenderer.sprite = walkFrames[frame];
    }

    private static Sprite[] CreateWalkFrames()
    {
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        return CreateFrames(profile.walkSheetResource, profile);
    }

    private static Sprite[] CreateFrames(string resourcePath, PlayerVisualProfile profile)
    {
        Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null) return System.Array.Empty<Sprite>();
        int frameWidth = sheet.width / Mathf.Max(1, profile.columns);
        return CasualArtLibrary.LoadSheet(resourcePath, profile.columns, profile.rows, frameWidth, true);
    }
}
