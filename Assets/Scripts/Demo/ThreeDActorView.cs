using UnityEngine;

/// <summary>논리 XY 좌표의 배우를 XZ 바닥의 실제 스키닝 모델로 표시한다.</summary>
public sealed class ThreeDActorView : MonoBehaviour
{
    private GameObject model;
    private Animation animation;
    private PlayerMovement movement;
    private EnemyAgent enemy;
    private Vector2 previous;
    private string species, state;
    private bool corpse;
    private float dashRecovery;
    private GameObject prefabOverride;
    private PlayerHealth health;
    private Transform faceOrigin, faceForward;
    private Quaternion facingCorrection = Quaternion.identity;
    public GameObject Model => model;
    public string CurrentAnimation => state;
    public Vector2 VisibleForward => faceOrigin != null && faceForward != null
        ? new Vector2(faceForward.position.x-faceOrigin.position.x, faceForward.position.z-faceOrigin.position.z).normalized
        : model != null ? new Vector2(model.transform.forward.x, model.transform.forward.z) : Vector2.down;

    public static ThreeDActorView Attach(GameObject owner, string kind, bool defeated = false, GameObject modelPrefab = null)
    {
        if (ThreeDPresentationConfig.SpritesEnabled) { SpriteDepthView.Attach(owner); return null; }
        var view = owner.GetComponent<ThreeDActorView>();
        if (view == null) view = owner.AddComponent<ThreeDActorView>();
        view.species = kind; view.corpse = defeated;
        view.prefabOverride = modelPrefab; view.health = owner.GetComponent<PlayerHealth>();
        view.movement = owner.GetComponent<PlayerMovement>(); view.enemy = owner.GetComponent<EnemyAgent>();
        view.EnsureModel(); return view;
    }

    private void OnEnable()
    {
        previous = transform.position; state = null; dashRecovery = 0;
        if (model != null) { model.SetActive(true); SetState(corpse ? "Defeated" : "Idle", true); }
    }
    private void OnDisable() { if (model != null) model.SetActive(false); }
    private void OnDestroy() { if (model != null) Destroy(model); }

    private void EnsureModel()
    {
        if (model != null || ThreeDWorldView.Current == null || string.IsNullOrEmpty(species)) return;
        var prefab = prefabOverride != null ? prefabOverride : ThreeDPresentationConfig.Active.Model(species);
        if (prefab == null) return;
        // 풀 배우의 모델도 DontDestroyOnLoad 풀 아래 보관해 재입장 시 다시 생성하지 않는다.
        Transform parent = GetComponent<MonsterPrefab>() != null || GetComponent<CorpsePrefab>() != null
            ? transform.parent : ThreeDWorldView.Current.transform;
        // FBX 축 변환/루트 애니메이션은 안쪽에 두고, 바깥 앵커가 게임 좌표와 방향을 소유한다.
        model = new GameObject(name + " 3D"); model.transform.SetParent(parent, false);
        Instantiate(prefab, model.transform, false);
        ThreeDWorldView.SetRenderLayer(model);
        model.transform.position = ThreeDWorldView.Map(transform.position);
        model.transform.rotation = Quaternion.Euler(0, 180, 0);
        animation = model.GetComponentInChildren<Animation>();
        if (animation != null)
        {
            // Animation.AddClip 별칭은 프리팹에 직렬화되지 않으므로 FBX 원본 이름을 런타임에 연결한다.
            var clips = new System.Collections.Generic.List<AnimationClip>();
            foreach (AnimationState item in animation) if (item.clip != null) clips.Add(item.clip);
            foreach (string key in new[] { "Idle", "Walk", "Dash", "Attack", "Loot", "Defeated" })
            {
                var clip = clips.Find(item => item.name.EndsWith("_" + key) || item.name.EndsWith("|" + key) || item.name == key);
                if (clip == null) continue;
                animation.AddClip(clip, key);
                animation[key].wrapMode = key == "Idle" || key == "Walk" || key == "Loot" ? WrapMode.Loop : WrapMode.ClampForever;
            }
        }
        previous = transform.position; state = null;
        SetState(corpse ? "Defeated" : "Idle", true);
        var nodes = model.GetComponentsInChildren<Transform>();
        faceOrigin = System.Array.Find(nodes, item => item.name == "FaceOrigin");
        faceForward = System.Array.Find(nodes, item => item.name == "FaceForward");
        if (faceOrigin != null && faceForward != null)
        {
            Vector3 local = model.transform.InverseTransformDirection(faceForward.position-faceOrigin.position); local.y=0;
            if(local.sqrMagnitude>.001f) facingCorrection=Quaternion.FromToRotation(local.normalized,Vector3.forward);
        }
        Face(Vector2.down,true);
    }

    private void LateUpdate()
    {
        EnsureModel(); if (model == null) return;
        var run = DungeonRunController.Instance;
        bool hidden = run != null && run.Player == transform && run.IsPlayerHidden;
        model.SetActive(!hidden);
        if (hidden) { previous = transform.position; return; }
        Vector2 position = transform.position;
        model.transform.position = ThreeDWorldView.Map(position);
        Vector2 velocity = Time.deltaTime > 0 ? (position - previous) / Time.deltaTime : Vector2.zero;
        previous = position;
        if (corpse || Time.timeScale <= 0 || GameShell.IsGameplayInputBlocked) return;
        Vector2 direction = velocity;
        string next = velocity.sqrMagnitude > .02f && velocity.sqrMagnitude < 10000f ? "Walk" : "Idle";
        if (movement != null)
        {
            if (movement.IsDashing) { next = "Dash"; direction = movement.DashDirection; dashRecovery = .08f; }
            else if (dashRecovery > 0) { dashRecovery = Mathf.Max(0, dashRecovery - Time.deltaTime); next = "Dash"; }
            else if (DungeonRunController.Instance != null && DungeonRunController.Instance.ActiveSession != null) next = "Loot";
            if (health != null && health.Current <= 0) next = "Defeated";
        }
        if (enemy != null && enemy.IsAttacking)
        {
            next = "Attack"; direction = enemy.AttackDirection;
        }
        if (direction.sqrMagnitude > .0001f && next != "Defeated")
        {
            Face(direction, next == "Attack" || (movement != null && movement.IsDashing));
        }
        SetState(next);
        if (next == "Attack" && animation != null && animation["Attack"] != null)
        {
            animation["Attack"].normalizedTime = enemy.AttackProgress;
            animation.Sample(); // 판정 시점과 같은 정규화 시간으로 무기를 표시한다.
        }
        if (next == "Walk" && animation != null && animation["Walk"] != null)
            animation["Walk"].speed = Mathf.Clamp(velocity.magnitude / (movement != null ? 4f : 1.6f), .65f, 2.2f);
    }

    public void Face(Vector2 direction, bool immediate)
    {
        if (model == null || direction.sqrMagnitude < .0001f) return;
        var desired=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y))*facingCorrection;
        model.transform.rotation=immediate ? desired : Quaternion.Slerp(model.transform.rotation,desired,1-Mathf.Exp(-18*Time.deltaTime));
    }

    private void SetState(string next, bool immediate = false)
    {
        if (animation == null || (!immediate && state == next) || animation[next] == null) return;
        state = next;
        if (immediate) animation.Play(next); else animation.CrossFade(next, .09f);
        if (immediate && next != "Defeated") { animation[next].time = 0; animation.Sample(); }
        if (next == "Dash") animation[next].speed = animation[next].length / (DungeonTuning.Active.playerDashDuration + .08f);
        if (next == "Defeated")
        {
            animation.Play(next); animation[next].time = animation[next].length;
            animation.Sample(); animation.Stop();
        }
    }
}
