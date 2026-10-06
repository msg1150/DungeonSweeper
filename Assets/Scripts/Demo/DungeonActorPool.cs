using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>추가한 런타임 컴포넌트도 풀 반환 시 참조·타이머·구독을 정리할 수 있다.</summary>
public interface IDungeonPoolResettable
{
    void ResetForPool();
}

/// <summary>
/// 프리팹별 몬스터·시체 인스턴스를 씬 사이에서 재사용한다. 런 데이터는 풀에 저장하지 않는다.
/// 보관 한도는 비활성 인스턴스에만 적용하므로 던전 스폰 수를 줄이지 않는다.
/// </summary>
public sealed class DungeonActorPool : MonoBehaviour
{
    private sealed class Actor
    {
        public readonly GameObject Object;
        public readonly MonoBehaviour Prefab;
        private readonly Transform[] transforms;
        private readonly Vector3[] positions, scales;
        private readonly Quaternion[] rotations;
        private readonly bool[] childActive;
        private readonly SpriteRenderer[] renderers;
        private readonly Sprite[] sprites;
        private readonly Color[] colors;
        private readonly bool[] rendererEnabled, flipX, flipY;
        private readonly Collider2D[] colliders;
        private readonly bool[] colliderEnabled;
        private readonly Rigidbody2D[] bodies;
        private MonoBehaviour[] behaviours;

        public Actor(MonoBehaviour prefab, Transform parent)
        {
            Prefab = prefab;
            // 비활성 부모 아래 생성해 OnEnable이 런 상태 초기화보다 먼저 실행되지 않게 한다.
            Object = Instantiate(prefab.gameObject, parent, false);
            Object.SetActive(false);
            transforms = Object.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[transforms.Length]; scales = new Vector3[transforms.Length]; rotations = new Quaternion[transforms.Length];
            childActive = new bool[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            { positions[i] = transforms[i].localPosition; scales[i] = transforms[i].localScale; rotations[i] = transforms[i].localRotation; childActive[i] = transforms[i].gameObject.activeSelf; }
            renderers = Object.GetComponentsInChildren<SpriteRenderer>(true);
            sprites = new Sprite[renderers.Length]; colors = new Color[renderers.Length];
            rendererEnabled = new bool[renderers.Length]; flipX = new bool[renderers.Length]; flipY = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            { sprites[i] = renderers[i].sprite; colors[i] = renderers[i].color; rendererEnabled[i] = renderers[i].enabled; flipX[i] = renderers[i].flipX; flipY[i] = renderers[i].flipY; }
            colliders = Object.GetComponentsInChildren<Collider2D>(true);
            colliderEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
            bodies = Object.GetComponentsInChildren<Rigidbody2D>(true);
        }

        public void Prepare(Vector2 position)
        {
            for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null)
            {
                transforms[i].localPosition = positions[i]; transforms[i].localScale = scales[i]; transforms[i].localRotation = rotations[i];
                if (i > 0) transforms[i].gameObject.SetActive(childActive[i]);
            }
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null)
            { renderers[i].sprite = sprites[i]; renderers[i].color = colors[i]; renderers[i].enabled = rendererEnabled[i]; renderers[i].flipX = flipX[i]; renderers[i].flipY = flipY[i]; }
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderEnabled[i];
            Object.transform.position = position;
            foreach (var body in bodies) if (body != null)
            { body.position = body.transform.position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
        }

        public void Reset()
        {
            Object.SetActive(false);
            // 첫 Spawn에서 추가되는 애니메이터까지 포함해 한 번만 캐시한다.
            behaviours ??= Object.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
                if (behaviour != null && behaviour is IDungeonPoolResettable resettable) resettable.ResetForPool();
            foreach (var body in bodies) if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
        }
    }

    private static DungeonActorPool instance;
    private readonly Dictionary<MonoBehaviour, Stack<Actor>> idle = new();
    private readonly Dictionary<GameObject, Actor> leased = new();
    private readonly List<GameObject> destroyedLeases = new();
    private Transform storage;
    private int idleCount;
    private bool shuttingDown;
    public static int InactiveCount => instance != null ? instance.idleCount : 0;
    public static int LeasedCount => instance != null ? instance.leased.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        storage = new GameObject("Inactive Actors").transform;
        storage.SetParent(transform, false);
        storage.gameObject.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static T Rent<T>(T prefab, Vector2 position) where T : MonoBehaviour
    {
        if (prefab == null) throw new System.ArgumentNullException(nameof(prefab));
        if (instance == null) new GameObject("Dungeon Actor Pool").AddComponent<DungeonActorPool>();
        Actor actor = null;
        if (instance.idle.TryGetValue(prefab, out var bucket))
            while (bucket.Count > 0 && actor == null)
            {
                var candidate = bucket.Pop(); instance.idleCount--;
                if (candidate.Object != null) actor = candidate; // 외부에서 삭제된 인스턴스는 재사용하지 않는다.
            }
        actor ??= new Actor(prefab, instance.storage);
        actor.Object.transform.SetParent(instance.transform, false);
        actor.Prepare(position);
        instance.leased.Add(actor.Object, actor);
        // Spawn이 데이터·시각 효과·플레이어 참조를 설정한 뒤 직접 활성화한다.
        return actor.Object.GetComponent<T>();
    }

    /// <summary>중복 반환은 무시한다. 풀에서 빌리지 않은 기존 저장용 오브젝트는 false를 반환한다.</summary>
    public static bool Return(GameObject obj)
    {
        if (obj == null || instance == null || instance.shuttingDown || !instance.leased.Remove(obj, out var actor)) return false;
        actor.Reset();
        var tuning = DungeonTuning.Active;
        if (!instance.idle.TryGetValue(actor.Prefab, out var bucket)) instance.idle[actor.Prefab] = bucket = new Stack<Actor>();
        if (bucket.Count >= Mathf.Max(0, tuning.poolCapacityPerPrefab) || instance.idleCount >= Mathf.Max(0, tuning.poolTotalCapacity))
        { Destroy(obj); return true; }
        obj.transform.SetParent(instance.storage, false);
        bucket.Push(actor); instance.idleCount++;
        return true;
    }

    /// <summary>마을에서는 캐시를 유지하고 메인 메뉴로 돌아오면 비활성 캐시를 해제한다.</summary>
    public static void ClearInactive()
    {
        if (instance == null) return;
        foreach (var bucket in instance.idle.Values)
            foreach (var actor in bucket) if (actor.Object != null) Destroy(actor.Object);
        instance.idle.Clear(); instance.idleCount = 0;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 향후 전투 시스템 등이 인스턴스를 직접 삭제해도 죽은 참조를 씬 사이에 누적하지 않는다.
        destroyedLeases.Clear();
        foreach (var lease in leased) if (lease.Key == null) destroyedLeases.Add(lease.Key);
        foreach (var key in destroyedLeases) leased.Remove(key);
        if (mode == LoadSceneMode.Single && scene.name == GameFlowConfig.Active.mainMenuSceneName) ClearInactive();
    }
    private void OnApplicationQuit() => shuttingDown = true;
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
}
