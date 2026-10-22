using UnityEngine;

/// <summary>프로토타입용 시각 요소와 동적 오브젝트 생성만 담당한다.</summary>
public sealed class DungeonWorldFactory
{
    private readonly Sprite fallbackSprite;
    private readonly Sprite[] corpseSprites;
    private readonly Sprite portalSprite;

    public DungeonWorldFactory()
    {
        fallbackSprite = CasualArtLibrary.WhiteSprite;
        corpseSprites = CasualArtLibrary.LoadSheet("Sprites/Monsters/corpse-sheet", 3, 1, 180f, true);
        portalSprite = Resources.Load<Sprite>("Sprites/escape-portal");
    }

    public CorpseRunData CreateCorpse(string name, Vector2 position, DismantleDifficulty difficulty, int monsterIndex, params LootDefinition[] loot)
    {
        Sprite corpse = corpseSprites.Length == 0 ? fallbackSprite : corpseSprites[Mathf.Clamp(monsterIndex, 0, corpseSprites.Length - 1)];
        GameObject root = CreateSprite(name, position, new Vector2(.4f, .4f), corpse, Color.white, 2, false);
        return new CorpseRunData(root, name, difficulty, loot) { MonsterIndex = monsterIndex };
    }

    public EnemyAgent CreateEnemy(MonsterDefinition definition, Transform player, Vector2 pointA, Vector2 pointB)
    {
        // 프레임 원본은 512px 정사각형이므로 0.55 배율에서 플레이어와 비슷한 월드 크기가 된다.
        GameObject root = CreateSprite($"배회 {definition.displayName}", pointA, new Vector2(.55f, .55f), fallbackSprite, Color.white, 3, true);
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        MonsterVisualAnimator visual = root.AddComponent<MonsterVisualAnimator>();
        visual.Initialize(definition.spriteSheetResource, renderer);
        EnemyAgent enemy = root.AddComponent<EnemyAgent>();
        enemy.Initialize(player, pointA, pointB, definition, visual);
        return enemy;
    }

    public void CreateExitMarker(string name, Vector2 position, Color color, bool isGate)
    {
        Color tint = isGate ? Color.white : Color.Lerp(Color.white, color, .35f);
        CreateSprite(name, position, isGate ? new Vector2(2.1f, 2.1f) : new Vector2(1.45f, 1.45f), portalSprite, tint, 2, false);
    }

    private GameObject CreateSprite(string name, Vector2 position, Vector2 size, Sprite sprite, Color color, int sortingOrder, bool withCollider)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : fallbackSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (withCollider) obj.AddComponent<BoxCollider2D>();
        return obj;
    }
}
