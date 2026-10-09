using UnityEngine;

/// <summary>플레이어의 물리 이동과 분리된 걷기 시각 효과를 담당한다.</summary>
public sealed class PlayerVisualAnimator : MonoBehaviour
{
    private Rigidbody2D body;
    private Transform visual;
    private SpriteRenderer visualRenderer;
    private SpriteRenderer previousPoseRenderer;
    private Color baseColor;
    private Sprite idleSprite;
    private Sprite[] walkFrames;
    private Sprite[] dashFrames;
    private readonly System.Collections.Generic.List<Sprite> ownedFrames = new();
    private PlayerMovement movement;
    private float frameTimer;
    private float recoveryRemaining, blendRemaining, blendDuration;

    public static void Ensure(GameObject player)
    {
        if (player != null && player.GetComponent<PlayerVisualAnimator>() == null)
            player.AddComponent<PlayerVisualAnimator>();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.DashStarted += RestartAnimation;
            movement.DashEnded += FinishDashAnimation;
        }
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        GameObject visualObject = new GameObject("Animated Player Visual");
        visualObject.transform.SetParent(transform, false);
        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        idleSprite = source.sprite;
        visualRenderer.sprite = idleSprite;
        visualRenderer.color = source.color;
        baseColor = source.color;
        visualRenderer.sortingLayerID = source.sortingLayerID;
        visualRenderer.sortingOrder = source.sortingOrder;
        visualObject.transform.localScale = Vector3.one * profile.visualScale;
        var previousObject = new GameObject("Previous Pose");
        previousObject.transform.SetParent(visualObject.transform, false);
        previousPoseRenderer = previousObject.AddComponent<SpriteRenderer>();
        previousPoseRenderer.sharedMaterial = visualRenderer.sharedMaterial;
        previousPoseRenderer.sortingLayerID = visualRenderer.sortingLayerID;
        previousPoseRenderer.sortingOrder = visualRenderer.sortingOrder - 1;
        previousPoseRenderer.enabled = false;
        source.enabled = false;

        visual = visualObject.transform;
        walkFrames = CreateWalkFrames();
        dashFrames = CreateFrames(profile.dashSheetResource, profile);
        if (idleSprite == null && walkFrames.Length > 0) idleSprite = walkFrames[0];
        visualRenderer.sprite = idleSprite;
    }

    private void RestartAnimation()
    {
        frameTimer = recoveryRemaining = 0f;
        if (visualRenderer != null && dashFrames != null && dashFrames.Length > 0)
            SetPose(dashFrames[0], Facing(movement.DashDirection), true);
    }

    private void FinishDashAnimation()
    {
        frameTimer = 0f;
        recoveryRemaining = movement != null && movement.IsMovementEnabled
            ? PlayerVisualProfile.Active.dashRecoverySeconds : 0f;
        if (visualRenderer == null) return;
        if (recoveryRemaining > 0f && dashFrames != null && dashFrames.Length > 0)
            SetPose(dashFrames[dashFrames.Length - 1], visualRenderer.flipX, true);
        else SetPose(idleSprite, visualRenderer.flipX, false);
    }

    private void OnDestroy()
    {
        if (movement != null)
        {
            movement.DashStarted -= RestartAnimation;
            movement.DashEnded -= FinishDashAnimation;
        }
        // 칸 여백을 조정한 Sprite만 이 플레이어가 소유한다. 공용 시트/텍스처는 캐시에 남긴다.
        foreach (Sprite frame in ownedFrames) if (frame != null) Destroy(frame);
    }

    private void LateUpdate()
    {
        if (visual == null || Time.timeScale <= 0f || GameShell.IsGameplayInputBlocked) return;
        AdvanceBlend(Time.deltaTime);
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        Vector2 velocity = movement != null ? movement.Velocity : body != null ? body.linearVelocity : Vector2.zero;
        if (movement != null && movement.IsDashing && dashFrames != null && dashFrames.Length > 0)
        {
            recoveryRemaining = 0f;
            // 실제 이동 타이머를 따른다. 착지는 이동 종료 뒤 짧게 표시하므로 0.15초에 억지로 끼워 넣지 않는다.
            float duration = Mathf.Max(.01f, DungeonTuning.Active.playerDashDuration);
            int travelFrames = Mathf.Max(1, dashFrames.Length - 1);
            float dashFrameRate = Mathf.Max(profile.dashFramesPerSecond, travelFrames / duration);
            int dashFrame = Mathf.Clamp(Mathf.FloorToInt(movement.DashProgress * duration * dashFrameRate), 0, travelFrames - 1);
            SetPose(dashFrames[dashFrame], Facing(movement.DashDirection), true);
            return;
        }
        bool recovering = recoveryRemaining > 0f;
        if (recovering)
        {
            recoveryRemaining = Mathf.Max(0f, recoveryRemaining - Time.deltaTime);
            if (recoveryRemaining > 0f) return;
        }
        if (velocity.sqrMagnitude <= .0025f || walkFrames == null || walkFrames.Length == 0)
        {
            frameTimer = 0f;
            SetPose(idleSprite, Facing(velocity), recovering);
            return;
        }

        frameTimer = Mathf.Repeat(frameTimer + Time.deltaTime, walkFrames.Length / Mathf.Max(.01f, profile.walkFramesPerSecond));
        int frame = Mathf.FloorToInt(frameTimer * profile.walkFramesPerSecond) % walkFrames.Length;
        SetPose(walkFrames[frame], Facing(velocity), recovering);
    }

    private bool Facing(Vector2 direction) => Mathf.Abs(direction.x) > .02f ? direction.x < 0f : visualRenderer.flipX;

    private void SetPose(Sprite sprite, bool flipX, bool blend)
    {
        if (visualRenderer.sprite == sprite && visualRenderer.flipX == flipX) return;
        blendDuration = Mathf.Max(0f, PlayerVisualProfile.Active.poseBlendSeconds);
        if (blend && blendDuration > 0f && visualRenderer.sprite != null)
        {
            previousPoseRenderer.sprite = visualRenderer.sprite;
            previousPoseRenderer.flipX = visualRenderer.flipX;
            previousPoseRenderer.color = baseColor;
            previousPoseRenderer.enabled = true;
            blendRemaining = blendDuration;
            Color incoming = baseColor; incoming.a = 0f;
            visualRenderer.color = incoming;
        }
        else
        {
            blendRemaining = 0f;
            previousPoseRenderer.enabled = false;
            visualRenderer.color = baseColor;
        }
        visualRenderer.sprite = sprite;
        visualRenderer.flipX = flipX;
    }

    // 두 렌더러를 재사용한다. 프레임마다 Sprite/Material/GameObject를 새로 만들지 않는다.
    private void AdvanceBlend(float elapsed)
    {
        if (blendRemaining <= 0f) return;
        blendRemaining = Mathf.Max(0f, blendRemaining - elapsed);
        float progress = 1f - blendRemaining / blendDuration;
        Color incoming = baseColor, outgoing = baseColor;
        incoming.a *= progress; outgoing.a *= 1f - progress;
        visualRenderer.color = incoming;
        previousPoseRenderer.color = outgoing;
        if (blendRemaining <= 0f) previousPoseRenderer.enabled = false;
    }

    private Sprite[] CreateWalkFrames()
    {
        PlayerVisualProfile profile = PlayerVisualProfile.Active;
        return CreateFrames(profile.walkSheetResource, profile);
    }

    private Sprite[] CreateFrames(string resourcePath, PlayerVisualProfile profile)
    {
        Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null) return System.Array.Empty<Sprite>();
        int frameWidth = sheet.width / Mathf.Max(1, profile.columns);
        Sprite[] bottomFirst = CasualArtLibrary.LoadSheet(resourcePath, profile.columns, profile.rows, frameWidth, true);
        // 그림의 읽는 순서는 좌상단부터다. 공용 캐시 배열을 바꾸지 않고 행 순서만 뒤집는다.
        Sprite[] topFirst = new Sprite[bottomFirst.Length];
        for (int row = 0; row < profile.rows; row++)
            for (int column = 0; column < profile.columns; column++)
            {
                Sprite frame = bottomFirst[(profile.rows - 1 - row) * profile.columns + column];
                float inset = frame.rect.width * Mathf.Clamp(profile.frameLeftInset, 0f, .1f);
                if (inset <= 0f) { topFirst[row * profile.columns + column] = frame; continue; }
                // 새 그림의 왼쪽 빈 여백만 제외한다. 피벗은 원래 위치를 유지해 프레임이 흔들리지 않는다.
                Rect rect = frame.rect; rect.x += inset; rect.width -= inset;
                Vector2 pivot = new Vector2((frame.pivot.x - inset) / rect.width, frame.pivot.y / rect.height);
                topFirst[row * profile.columns + column] = Sprite.Create(frame.texture, rect, pivot, frame.pixelsPerUnit);
                ownedFrames.Add(topFirst[row * profile.columns + column]);
            }
        return topFirst;
    }
}
