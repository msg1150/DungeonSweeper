#if UNITY_EDITOR
using System;
using UnityEngine;
public sealed class ValidationRuntimeDriver : MonoBehaviour
{
    public static Action TickAction;
    private void Awake() => DontDestroyOnLoad(gameObject);
    private void Update() => TickAction?.Invoke();
}
#endif
