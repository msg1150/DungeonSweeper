using UnityEngine;

/// <summary>플레이어의 물리 이동과 분리된 걷기 시각 효과를 담당한다.</summary>
public sealed class PlayerVisualAnimator : MonoBehaviour
{
    private Rigidbody2D body;
    private Transform visual;
    private SpriteRenderer visualRenderer;
    private Sprite[] walkFrames;
    private float frameTimer;

    public static void Ensure(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerVisualAnimator>() == null)
            player.AddComponent<PlayerVisualAnimator>();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        GameObject visualObject = new GameObject("Animated Player Visual");
        visualObject.transform.SetParent(transform, false);
        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = source.sprite;
        visualRenderer.color = source.color;
        visualRenderer.sortingLayerID = source.sortingLayerID;
        visualRenderer.sortingOrder = source.sortingOrder;
        source.enabled = false;

        visual = visualObject.transform;
        walkFrames = CreateWalkFrames();
        if (walkFrames.Length > 0) visualRenderer.sprite = walkFrames[0];
    }

    private void LateUpdate()
    {
        if (visual == null) return;
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        if (velocity.sqrMagnitude <= .0025f || walkFrames == null || walkFrames.Length == 0)
        {
            if (walkFrames != null && walkFrames.Length > 0) visualRenderer.sprite = walkFrames[0];
            return;
        }

        frameTimer += Time.unscaledDeltaTime;
        int frame = Mathf.FloorToInt(frameTimer * 9f) % walkFrames.Length;
        visualRenderer.sprite = walkFrames[frame];
    }

    private static Sprite[] CreateWalkFrames()
    {
        Texture2D sheet = Resources.Load<Texture2D>("Sprites/player-walk-cycle");
        if (sheet == null) return System.Array.Empty<Sprite>();
        int frameWidth = sheet.width / 2;
        int frameHeight = sheet.height / 2;
        Sprite[] frames = new Sprite[4];
        for (int index = 0; index < frames.Length; index++)
        {
            int x = (index % 2) * frameWidth;
            int y = (index / 2) * frameHeight;
            frames[index] = Sprite.Create(sheet, new Rect(x, y, frameWidth, frameHeight), new Vector2(.5f, .5f), frameWidth);
        }
        return frames;
    }
}
