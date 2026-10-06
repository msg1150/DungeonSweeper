using System;
using System.Reflection;
using UnityEngine;

public static class FocusPauseChecks
{
    public static void Run(Action<bool, string> check)
    {
        var shell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
        void Call(string method, params object[] args) => typeof(GameShell)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, args);
        Call("OnApplicationFocus", true);
        Call("Resume");
        Call("OnApplicationFocus", false);
        check(Time.timeScale == 0f && !GameShell.IsMenuOpen && GameShell.IsGameplayInputBlocked,
            "focus loss freezes gameplay without opening a menu");
        Call("OnApplicationFocus", false);
        Call("OnApplicationFocus", true);
        check(Time.timeScale == 1f && !GameShell.IsMenuOpen && GameShell.IsFocusInputBlocked,
            "focus return automatically resumes and blocks the return input frame");
        Time.timeScale = .5f;
        Call("OnApplicationFocus", false);
        Call("OnApplicationFocus", true);
        check(Time.timeScale == .5f, "focus return preserves the previous simulation speed");
        Time.timeScale = 1f;
        Call("OpenPause");
        Call("OnApplicationFocus", false);
        Call("OnApplicationFocus", true);
        check(Time.timeScale == 0f && GameShell.IsMenuOpen, "focus return keeps a manually opened pause menu");
        Call("Resume");
        Call("OnApplicationFocus", false);
        Call("OpenPause");
        Call("OnApplicationFocus", true);
        check(Time.timeScale == 0f && GameShell.IsMenuOpen, "manual pause takes ownership of a focus pause");
        Call("OpenPause");
        Call("Resume");
        check(Time.timeScale == 1f, "manual resume after focus pause retains the original speed");
        var panel = UnityEngine.Object.FindAnyObjectByType<TownCommercePanel>();
        panel.Open(TownFacility.Warehouse);
        Call("OnApplicationFocus", false);
        Call("OnApplicationFocus", true);
        check(Time.timeScale == 0f && TownCommercePanel.IsOpen && !GameShell.IsMenuOpen,
            "warehouse stays open and paused across focus changes");
        panel.Close();
        check(Time.timeScale == 1f, "closing warehouse after focus changes resumes gameplay");
    }
}
