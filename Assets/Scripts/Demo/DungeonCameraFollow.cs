using UnityEngine;

/// <summary>넓은 던전을 탐색할 수 있도록 주 카메라를 플레이어에 고정한다.</summary>
public sealed class DungeonCameraFollow : MonoBehaviour
{
    private Transform target;
    public static void Ensure(Transform player)
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        DungeonCameraFollow follow = camera.GetComponent<DungeonCameraFollow>();
        if (follow == null) follow = camera.gameObject.AddComponent<DungeonCameraFollow>();
        follow.target = player;
    }
    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 position = target.position;
        position.z = transform.position.z;
        transform.position = position;
    }
}
