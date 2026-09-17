using UnityEngine;

/// <summary>프로토타입용 시각 요소와 동적 오브젝트 생성만 담당한다.</summary>
public sealed class DungeonWorldFactory
{
    private readonly Sprite fallbackSprite;
    private readonly Sprite corpseSprite;
    private readonly Sprite enemySprite;
    private readonly Sprite portalSprite;

    public DungeonWorldFactory()
    {
        Texture2D texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        corpseSprite = Resources.Load<Sprite>("Sprites/corpse");
        enemySprite = Resources.Load<Sprite>("Sprites/enemy-goblin");
        portalSprite = Resources.Load<Sprite>("Sprites/escape-portal");
    }

    public CorpseRunData CreateCorpse(string name, Vector2 position, DismantleDifficulty difficulty, params LootDefinition[] loot)
    {
        GameObject root = CreateSprite(name, position, new Vector2(1.7f, 1.7f), corpseSprite, Color.white, 2, false);
        return new CorpseRunData(root, name, difficulty, loot);
    }

    public EnemyAgent CreateEnemy(string name, Transform player, Vector2 pointA, Vector2 pointB)
    {
        GameObject root = CreateSprite(name, pointA, new Vector2(1.2f, 1.2f), enemySprite, Color.white, 3, true);
        EnemyAgent enemy = root.AddComponent<EnemyAgent>();
        enemy.Initialize(player, pointA, pointB);
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
