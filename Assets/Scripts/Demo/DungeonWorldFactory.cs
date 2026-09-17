using UnityEngine;

/// <summary>프로토타입용 시각 요소와 동적 오브젝트 생성만 담당한다.</summary>
public sealed class DungeonWorldFactory
{
    private readonly Sprite squareSprite;

    public DungeonWorldFactory()
    {
        Texture2D texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }

    public CorpseRunData CreateCorpse(string name, Vector2 position, DismantleDifficulty difficulty, params LootDefinition[] loot)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        CreateRectangle("Blood", position + new Vector2(-.08f, -.26f), new Vector2(1.2f, .22f), new Color(.27f, .035f, .06f), 0, false).transform.SetParent(root.transform, true);
        CreateRectangle("Body", position, new Vector2(.9f, .38f), new Color(.62f, .25f, .28f), 1, false).transform.SetParent(root.transform, true);
        CreateRectangle("Head", position + new Vector2(.5f, .06f), new Vector2(.26f, .26f), new Color(.8f, .66f, .5f), 2, false).transform.SetParent(root.transform, true);
        return new CorpseRunData(root, name, difficulty, loot);
    }

    public EnemyAgent CreateEnemy(string name, Transform player, Vector2 pointA, Vector2 pointB)
    {
        GameObject root = CreateRectangle(name, pointA, new Vector2(.66f, .66f), new Color(.92f, .27f, .3f), 3, true);
        EnemyAgent enemy = root.AddComponent<EnemyAgent>();
        enemy.Initialize(player, pointA, pointB);
        return enemy;
    }

    public void CreateExitMarker(string name, Vector2 position, Color color, bool isGate)
    {
        CreateRectangle(name, position, isGate ? new Vector2(.45f, 1.25f) : new Vector2(.34f, 1.2f), color, 2, false);
    }

    private GameObject CreateRectangle(string name, Vector2 position, Vector2 size, Color color, int sortingOrder, bool withCollider)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (withCollider) obj.AddComponent<BoxCollider2D>();
        return obj;
    }
}
