using System.Collections.Generic;
using UnityEngine;

/// <summary>3x2 시트의 윗줄은 이동, 아랫줄은 공격으로 재생한다.</summary>
public sealed class MonsterVisualAnimator : MonoBehaviour, IDungeonPoolResettable
{
    private sealed class Frames { public Sprite[] Walk; public Sprite[] Attack; }
    private static readonly Dictionary<string, Frames> cache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cache.Clear();
    private SpriteRenderer target;
    private Sprite[] walk;
    private Sprite[] attack;
    private float clock;

    public void Initialize(string resourcePath, SpriteRenderer renderer)
    {
        ResetForPool();
        target = renderer;
        if (cache.TryGetValue(resourcePath, out Frames cached) && cached?.Walk != null && cached.Walk.Length > 0 && cached.Walk[0] != null)
        {
            walk = cached.Walk;
            attack = cached.Attack;
            target.sprite = walk[0];
            return;
        }
        Texture2D source = Resources.Load<Texture2D>(resourcePath);
        if (source == null) return;
        Texture2D transparent = CasualArtLibrary.CreateReadableCopy(source);
        Color[] pixels = transparent.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].r > .72f && pixels[i].b > .72f && pixels[i].g < .45f) pixels[i].a = 0f;
        transparent.SetPixels(pixels);
        transparent.Apply();
        walk = SliceRow(transparent, 1);
        attack = SliceRow(transparent, 0);
        // 시트 가공이 끝난 사본은 렌더링만 사용하므로 CPU 픽셀 버퍼를 해제한다.
        transparent.Apply(false, true);
        cache[resourcePath] = new Frames { Walk = walk, Attack = attack };
        target.sprite = walk[0];
    }

    // 시트 애니메이션을 끈 프리팹도 이전 대여의 프레임·뒤집기 상태를 남기지 않는다.
    public void ResetForPool() { clock = 0f; target = null; walk = null; attack = null; }

    public void Tick(bool attacking, float attackProgress, bool moving, Vector2 direction)
    {
        if (target == null || walk == null) return;
        if (Mathf.Abs(direction.x) > .02f) target.flipX = direction.x < 0f;
        if (attacking) target.sprite = attack[Mathf.Clamp(Mathf.FloorToInt(attackProgress * 3f), 0, 2)];
        else
        {
            if (moving) clock = Mathf.Repeat(clock + Time.deltaTime * 7f, 3f);
            target.sprite = walk[moving ? Mathf.FloorToInt(clock) % 3 : 0];
        }
    }

    private static Sprite[] SliceRow(Texture2D texture, int row)
    {
        Sprite[] result = new Sprite[3];
        float width = texture.width / 3f;
        float height = texture.height / 2f;
        for (int x = 0; x < 3; x++) result[x] = Sprite.Create(texture, new Rect(x * width, row * height, width, height), new Vector2(.5f, .35f), 180f);
        return result;
    }
}
