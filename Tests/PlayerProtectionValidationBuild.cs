using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PlayerProtectionValidationBuild
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!root.Replace('\\', '/').EndsWith("/.utmp/UnityValidation"))
            throw new InvalidOperationException("Build validation must run in the isolated project.");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = ReleaseBuildValidator.EnabledScenes(),
            locationPathName = Path.Combine(root, "SaveValidationPlayer.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Release protection test build failed: " + report.summary.result);
        Debug.Log("RELEASE PROTECTION BUILD PASS");
    }
}
