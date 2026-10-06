using UnityEngine;

public static class TownCasualVisuals
{
    public static void Apply()
    {
        GameObject warehouse = GameObject.Find("Town Warehouse");
        if (warehouse == null)
        {
            warehouse = new GameObject("Town Warehouse");
            warehouse.transform.position = new Vector3(-.4f, -2.6f, 0);
            SpriteRenderer marker = warehouse.AddComponent<SpriteRenderer>();
            marker.sprite = CasualArtLibrary.WhiteSprite; marker.color = new Color(.2f, .5f, .45f);
            marker.sortingOrder = 1; warehouse.transform.localScale = new Vector3(1.3f, .8f, 1);
        }
        Sprite background = CasualArtLibrary.LoadFull("Sprites/Environment/town-casual", 100f);
        Sprite[] structures = CasualArtLibrary.LoadSheet("Sprites/Environment/town-structures", 3, 1, 180f, true);
        SetBackgroundCover("Town Ground", background);
        GameObject path = GameObject.Find("Main Path"); if (path != null) path.SetActive(false);
        if (structures.Length >= 3)
        {
            SetSprite("Salvager Guild", structures[0], 4.8f, 1);
            SetSprite("Supply Shop", structures[1], 4.1f, 1);
            SetSprite("Dungeon Entrance", structures[2], 3.1f, 1);
        }
    }

    public static void RefreshBackground() => SetBackgroundCover("Town Ground",
        CasualArtLibrary.LoadFull("Sprites/Environment/town-casual", 100f));

    private static void SetBackgroundCover(string objectName, Sprite sprite)
    {
        GameObject obj = GameObject.Find(objectName);
        Camera camera = Camera.main;
        if (obj == null || sprite == null || camera == null) return;
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = -10;
        float viewHeight = camera.orthographicSize * 2f;
        float viewWidth = viewHeight * camera.aspect;
        float scale = Mathf.Max(viewWidth / Mathf.Max(.01f, sprite.bounds.size.x), viewHeight / Mathf.Max(.01f, sprite.bounds.size.y));
        // 해상도 반올림이나 Game View 리사이즈에서도 가장자리가 비지 않도록 3% 여유를 둔다.
        scale *= 1.03f;
        obj.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SetSprite(string objectName, Sprite sprite, float targetWidth, int order)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null || sprite == null) return;
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = order;
        float scale = targetWidth / Mathf.Max(.01f, sprite.bounds.size.x);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
