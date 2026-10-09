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
        walk = SliceRow(transparent, 1, resourcePath);
        attack = SliceRow(transparent, 0, resourcePath);
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

    private static Sprite[] SliceRow(Texture2D texture, int row, string resourcePath)
    {
        Sprite[] result = new Sprite[3];
        float width = texture.width / 3f;
        float height = texture.height / 2f;
        for (int x = 0; x < 3; x++)
        {
            Rect rect = new Rect(x * width, row * height, width, height);
            Vector2 pivot = new Vector2(.5f, .35f);
            bool fixAttackSeam = row == 0 && (resourcePath == "Sprites/Monsters/goblin-sheet" || resourcePath == "Sprites/Monsters/orc-sheet");
            float seam = width * .13f, cornerY = height * .27f;
            // 타격 칸에는 궤적 전체를 포함한다. 넓어진 Rect에서도 본체 피벗/크기는 유지한다.
            if (fixAttackSeam && x == 1)
            {
                rect.width += seam;
                pivot.x = width * .5f / rect.width;
            }
            result[x] = Sprite.Create(texture, rect, pivot, 180f);
            // 윗쪽 궤적과 아랫쪽 오크 옷자락이 같은 열을 공유한다.
            // 사각형 전체를 자르면 옷자락까지 잘리므로 해당 모서리만 메시에서 제외한다.
            // OverrideGeometry 좌표는 픽셀 단위 Sprite Rect 공간이다. 피벗/UV 변환은 Unity가 처리한다.
            if (fixAttackSeam && x == 1)
                result[x].OverrideGeometry(new[] { new Vector2(0, 0), new Vector2(width, 0),
                    new Vector2(width, cornerY), new Vector2(width + seam, cornerY),
                    new Vector2(width + seam, height), new Vector2(0, height) },
                    new ushort[] { 0, 1, 2, 0, 2, 5, 2, 3, 4, 2, 4, 5 });
            else if (fixAttackSeam && x == 2)
                result[x].OverrideGeometry(new[] { new Vector2(0, 0), new Vector2(width, 0),
                    new Vector2(width, height), new Vector2(seam, height),
                    new Vector2(seam, cornerY), new Vector2(0, cornerY) },
                    new ushort[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 0, 4, 5 });
        }
        return result;
    }
}
