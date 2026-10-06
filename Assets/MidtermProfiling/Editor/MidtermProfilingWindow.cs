using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace DungeonSweeper.Midterm
{
    public sealed class MidtermProfilingWindow : EditorWindow
    {
        MidtermMode mode;
        int items = 65536, kinds = 128, warmup = 120, frames = 300;
        MidtermProfilingLab lab;
        bool ownsRecorder, previousRecording;
        string lastOutput;

        [MenuItem("Dungeon Sweeper/Midterm Profiling/Open Lab")]
        public static void Open() => GetWindow<MidtermProfilingWindow>("Midterm Profiling");

        void OnEnable() => EditorApplication.update += Monitor;
        void OnDisable()
        {
            EditorApplication.update -= Monitor;
            RemoveLab();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Play the game in a fixed scene. Set Profiler target to Play Mode and turn Deep Profile OFF. " +
                "This lab adds only a temporary object; no scene or gameplay script is changed.", MessageType.Info);
            bool running = lab != null && lab.IsCapturing;
            using (new EditorGUI.DisabledScope(running || ownsRecorder))
            {
                mode = (MidtermMode)EditorGUILayout.EnumPopup("Mode", mode);
                items = EditorGUILayout.IntSlider("Stress loot items", items, 1024, 131072);
                kinds = EditorGUILayout.IntSlider("Stress loot kinds", kinds, 8, 256);
                warmup = EditorGUILayout.IntSlider("Warmup frames", warmup, 30, 600);
                frames = EditorGUILayout.IntSlider("Measured frames", frames, 30, 1000);
                EditorGUILayout.LabelField("NormalMarkers uses 256 items / 16 kinds.");
                EditorGUILayout.LabelField("Baseline runs no synthetic workload.");
                if (GUILayout.Button("Open Unity Profiler")) EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorApplication.isPaused))
                    if (GUILayout.Button("Capture CSV + Profiler .data")) Begin();
            }
            if (running)
            {
                EditorGUILayout.LabelField("Warmup remaining", lab.WarmupRemaining.ToString());
                EditorGUILayout.LabelField("Captured frames", lab.CapturedFrames + " / " + frames);
                if (GUILayout.Button("Cancel and remove temporary lab")) RemoveLab();
            }
            else if (lab != null && GUILayout.Button("Remove temporary lab")) RemoveLab();
            if (!string.IsNullOrEmpty(lastOutput))
            {
                EditorGUILayout.SelectableLabel(lastOutput, EditorStyles.textField, GUILayout.Height(42));
                if (GUILayout.Button("Show last output folder")) EditorUtility.RevealInFinder(lastOutput);
            }
            EditorGUILayout.HelpBox("For comparison use identical loot counts, resolution, VSync, frame cap and gameplay conditions. " +
                "Profiler history may include warmup/older frames; use unity_frame in frames.csv to select the measured range. " +
                "Set Profiler Frame Count high enough to retain the entire capture.", MessageType.None);
        }

        void Begin()
        {
            try
            {
                if (Profiler.enableBinaryLog || !string.IsNullOrEmpty(Profiler.logFile))
                    throw new InvalidOperationException("A binary profiler capture is already configured. Finish it before using this lab.");
                if (ProfilerDriver.deepProfiling)
                    throw new InvalidOperationException("Turn Deep Profile OFF before comparing these captures.");
                if (ProfilerDriver.profileEditor)
                    throw new InvalidOperationException("Set Profiler target to Play Mode before starting.");
                if (lab == null)
                {
                    var temporary = new GameObject("[Midterm] Temporary Profiling Lab");
                    temporary.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    lab = temporary.AddComponent<MidtermProfilingLab>();
                    lab.Completed += OnCompleted;
                }
                lab.BeginCapture(mode, items, kinds, warmup, frames,
                    Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/MidtermProfiling")));
                previousRecording = ProfilerDriver.enabled;
                ownsRecorder = true;
                ProfilerDriver.enabled = true;
            }
            catch (Exception error)
            {
                RemoveLab();
                Debug.LogException(error);
            }
        }

        void OnCompleted(MidtermProfilingLab completed)
        {
            string output = completed.LastOutputDirectory;
            // Wait until Unity has submitted the last measured frame before saving its history.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    ProfilerDriver.enabled = false;
                    ProfilerDriver.SaveProfile(Path.Combine(output, "profiler.data"));
                    lastOutput = output;
                    Debug.Log("Midterm Profiler data saved: " + output);
                }
                catch (Exception error) { Debug.LogException(error); }
                finally { RestoreRecorder(); }
            };
        }

        void Monitor()
        {
            if (ownsRecorder && (lab == null || !EditorApplication.isPlaying)) RestoreRecorder();
            else if (ownsRecorder && lab != null && !lab.IsCapturing)
                EditorApplication.delayCall += RestoreRecorder;
            if (lab != null && lab.IsCapturing) Repaint();
        }

        void RestoreRecorder()
        {
            if (!ownsRecorder) return;
            ProfilerDriver.enabled = previousRecording;
            ownsRecorder = false;
        }

        void RemoveLab()
        {
            if (lab != null)
            {
                lab.Completed -= OnCompleted;
                lab.CancelCapture();
                DestroyImmediate(lab.gameObject);
                lab = null;
            }
            RestoreRecorder();
        }
    }
}
