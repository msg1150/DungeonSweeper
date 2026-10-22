#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>게임을 Play하지 않고 격리 프로젝트의 임시 장면에서 충돌·복원만 검사한다.</summary>
public static class HidingPhysicsEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal)
            || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Hiding physics checks require the isolated editor outside Play mode.");
        var previousSimulation = Physics2D.simulationMode;
        // 마지막 독립 검사에서 검증 복사본의 미저장 장면만 빈 검사 장면으로 교체한다.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Physics2D.simulationMode = SimulationMode2D.Script;
        var physics = scene.GetPhysicsScene2D();
        var player = new GameObject("Physics-only hiding player", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerMovement));
        var passer = new GameObject("Physics-only passing body", typeof(Rigidbody2D), typeof(BoxCollider2D));
        var owner = new GameObject("Hiding state check"); owner.SetActive(false);
        var body = player.GetComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var other = passer.GetComponent<Rigidbody2D>(); other.bodyType = RigidbodyType2D.Kinematic;
        var movement = player.GetComponent<PlayerMovement>();
        var run = owner.AddComponent<DungeonRunController>();
        // 검증 프로세스의 다른 장면과 충분히 떨어뜨린다. 원본 프로젝트에서는 실행하지 않는다.
        var anchor = new Vector2(1024f, 1024f);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var hidden = typeof(DungeonRunController).GetMethod("SetPlayerHidden", flags);
        typeof(DungeonRunController).GetField("player", flags).SetValue(run, player.transform);
        typeof(DungeonRunController).GetField("playerBody", flags).SetValue(run, body);
        typeof(DungeonRunController).GetField("movement", flags).SetValue(run, movement);
        var health = player.AddComponent<PlayerHealth>(); health.Initialize();
        typeof(DungeonRunController).GetField("playerHealth", flags).SetValue(run, health);
        typeof(DungeonRunController).GetField("inventory", flags).SetValue(run, new GridInventory(5, 4));
        void Hide(bool value) => hidden.Invoke(run, new object[] { value });
        try
        {
            body.position = anchor; player.transform.position = anchor;
            other.position = anchor + Vector2.left * 2;
            physics.Simulate(.02f);
            check(physics.OverlapPoint(anchor) == player.GetComponent<Collider2D>(), "visible player participates in the isolated physics scene");
            body.linearVelocity = Vector2.right; body.angularVelocity = 2;
            Hide(true); Hide(true);
            check(run.IsPlayerHidden && !movement.IsMovementEnabled && !body.simulated && body.linearVelocity == Vector2.zero && body.angularVelocity == 0,
                "hiding stops motion and removes the player body from simulation; repeated hide is safe");
            check(physics.OverlapPoint(anchor) == null, "hidden player is absent from contact and overlap queries without disabling its collider component");
            for (int i = 0; i <= 80; i++)
            {
                other.position = anchor + Vector2.right * Mathf.Lerp(-2, 2, i / 80f);
                physics.Simulate(.02f);
            }
            check((body.position - anchor).sqrMagnitude < .000001f && ((Vector2)player.transform.position - anchor).sqrMagnitude < .000001f,
                "a solid body crossing the hiding point cannot displace the hidden player");
            Hide(false); physics.Simulate(.02f);
            check(!run.IsPlayerHidden && movement.IsMovementEnabled && body.simulated && (body.position - anchor).sqrMagnitude < .000001f,
                "leaving hiding restores the original position and normal physics");
            check(body.constraints == RigidbodyConstraints2D.FreezeRotation && player.GetComponent<Collider2D>().enabled
                && physics.OverlapPoint(anchor) == player.GetComponent<Collider2D>(), "normal collision components and constraints survive hiding");

            Hide(true);
            player.transform.position = anchor + Vector2.up; body.position = anchor + Vector2.up;
            check((run.CaptureSave().playerPosition - anchor).sqrMagnitude < .000001f,
                "hidden save capture uses the stored anchor even if the transform is changed externally");
            Hide(false);
            check((body.position - anchor).sqrMagnitude < .000001f && ((Vector2)player.transform.position - anchor).sqrMagnitude < .000001f,
                "exit restores the stored anchor even if another system changes the hidden transform");
            body.simulated = false; Hide(true); Hide(false);
            check(!body.simulated, "exit preserves a body that was already not simulated before hiding");
            body.simulated = true;
            body.position = anchor + new Vector2(.2f, .15f); player.transform.position = body.position;
            Hide(true);
            var resumedPosition = body.position;
            var saved = JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(run.CaptureSave()));
            Hide(false);
            player.transform.position = anchor + Vector2.one * 10; body.position = player.transform.position;
            typeof(DungeonRunController).GetMethod("RestorePlayerAndBag", flags).Invoke(run, new object[] { saved });
            check(!body.simulated && run.IsPlayerHidden && (resumedPosition - (anchor + new Vector2(.2f, .15f))).sqrMagnitude < .000001f,
                "restoring a hidden save reapplies physics isolation at the saved position");
            check((body.position - resumedPosition).sqrMagnitude < .000001f && ((Vector2)player.transform.position - resumedPosition).sqrMagnitude < .000001f,
                "hidden save round trip restores the original coordinates without snapping to the cell center");
            typeof(DungeonRunController).GetMethod("OnDestroy", flags).Invoke(run, null);
            check(body.simulated && (body.position - resumedPosition).sqrMagnitude < .000001f,
                "run teardown restores its player's simulation state");
        }
        finally
        {
            Physics2D.simulationMode = previousSimulation;
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(passer);
        }
    }
}
#endif
