using UnityEngine;
using UnityEngine.InputSystem;

public interface IPlayerInputSource { Vector2 ReadMove(); bool DashPressedThisFrame(); }

public sealed class KeyboardPlayerInputSource : IPlayerInputSource
{
    public Vector2 ReadMove()
    {
        if (Keyboard.current == null) return Vector2.zero;
        Vector2 value = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) value.y++;
        if (Keyboard.current.sKey.isPressed) value.y--;
        if (Keyboard.current.aKey.isPressed) value.x--;
        if (Keyboard.current.dKey.isPressed) value.x++;
        return value.normalized;
    }
    public bool DashPressedThisFrame() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
}
