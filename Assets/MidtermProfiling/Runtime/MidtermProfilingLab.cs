#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonSweeper.Midterm
{
    public enum MidtermMode { Baseline, NormalMarkers, Bottleneck, Optimized }

    [DisallowMultipleComponent]
    [AddComponentMenu("Midterm Profiling/Profiling Lab (Temporary)")]
    public sealed class MidtermProfilingLab : MonoBehaviour
    {
        public const int Seed = 20261006;
        public event Action<MidtermProfilingLab> Completed;
        public bool IsCapturing { get; private set; }
        public string LastOutputDirectory { get; private set; }
        public int CapturedFrames => sampleCount;
        public int WarmupRemaining => warmupRemaining;
        public ulong LastChecksum { get; private set; }
        public MidtermMode Mode { get; private set; }

        struct Sample { public int Frame; public double WorkMs, FrameMs; public long AllocCalls; }
        Sample[] samples;
        Sample pending;
        bool hasPending;
        int warmupRemaining, initialWarmup, sampleCount;
        MidtermLootWorkload normal, stress;
        MidtermAllocationProbe allocations;
        string initialScene;
        int initialWidth, initialHeight, initialVsync, initialFrameCap;
        float initialTimeScale;

        public void BeginCapture(MidtermMode mode, int items, int kinds, int warmup, int frames, string outputRoot)
        {
            if (IsCapturing) throw new InvalidOperationException("Capture already running.");
            if (!Enum.IsDefined(typeof(MidtermMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (frames < 30 || frames > 1000 || warmup < 1 || warmup > 600)
                throw new ArgumentOutOfRangeException(nameof(frames));
            // Initialization, verification and buffers are deliberately excluded from measurements.
            normal = new MidtermLootWorkload(256, 16, Seed);
            stress = new MidtermLootWorkload(items, kinds, Seed);
            normal.VerifyEquivalentResults();
            stress.VerifyEquivalentResults();
            allocations?.Dispose();
            allocations = new MidtermAllocationProbe();
            samples = new Sample[frames];
            sampleCount = 0;
            hasPending = false;
            warmupRemaining = initialWarmup = warmup;
            Mode = mode;
            initialScene = SceneManager.GetActiveScene().name;
            initialWidth = Screen.width;
            initialHeight = Screen.height;
            initialVsync = QualitySettings.vSyncCount;
            initialFrameCap = Application.targetFrameRate;
            initialTimeScale = Time.timeScale;
            LastOutputDirectory = Path.Combine(Path.GetFullPath(outputRoot),
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + mode);
            Directory.CreateDirectory(LastOutputDirectory);
            IsCapturing = true;
        }

        void Update()
        {
            if (!IsCapturing) return;
            if (SceneManager.GetActiveScene().name != initialScene || Screen.width != initialWidth
                || Screen.height != initialHeight || QualitySettings.vSyncCount != initialVsync
                || Application.targetFrameRate != initialFrameCap || Time.timeScale != initialTimeScale)
            {
                CancelCapture();
                UnityEngine.Debug.LogWarning("Midterm capture cancelled: scene, resolution, pause or frame settings changed. Repeat under fixed conditions.");
                return;
            }
            // deltaTime observed now belongs to the previous frame's workload.
            if (hasPending)
            {
                pending.FrameMs = Time.unscaledDeltaTime * 1000.0;
                samples[sampleCount++] = pending;
                hasPending = false;
                if (sampleCount == samples.Length)
                {
                    IsCapturing = false;
                    allocations.Dispose();
                    allocations = null;
                    Export(); // After the last measured frame, never inside the workload marker.
                    Completed?.Invoke(this);
                    return;
                }
            }
            allocations.Begin();
            long beforeTicks = Stopwatch.GetTimestamp();
            switch (Mode)
            {
                case MidtermMode.NormalMarkers: LastChecksum = normal.RunNormal(); break;
                case MidtermMode.Bottleneck: LastChecksum = stress.RunBottleneck(); break;
                case MidtermMode.Optimized: LastChecksum = stress.RunOptimized(); break;
                default: LastChecksum = 0; break;
            }
            double workMs = (Stopwatch.GetTimestamp() - beforeTicks) * 1000.0 / Stopwatch.Frequency;
            long allocCalls = allocations.End();
            if (warmupRemaining > 0) { warmupRemaining--; return; }
            pending = new Sample { Frame = Time.frameCount, WorkMs = workMs, AllocCalls = allocCalls };
            hasPending = true;
        }

        public void CancelCapture()
        {
            IsCapturing = false;
            hasPending = false;
            allocations?.Dispose();
            allocations = null;
        }
        void OnDisable() => CancelCapture();

        void Export()
        {
            var csv = new StringBuilder("sample,unity_frame,frame_interval_ms,lab_work_ms,lab_gc_alloc_calls\n");
            double[] work = new double[sampleCount], frame = new double[sampleCount];
            long allocated = 0;
            double workSum = 0, frameSum = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                Sample s = samples[i];
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:F6},{3:F6},{4}\n", i + 1, s.Frame, s.FrameMs, s.WorkMs, s.AllocCalls);
                work[i] = s.WorkMs; frame[i] = s.FrameMs;
                workSum += s.WorkMs; frameSum += s.FrameMs; allocated += s.AllocCalls;
            }
            Array.Sort(work); Array.Sort(frame);
            File.WriteAllText(Path.Combine(LastOutputDirectory, "frames.csv"), csv.ToString(), new UTF8Encoding(false));
            var report = new CaptureReport
            {
                mode = Mode.ToString(), unityVersion = Application.unityVersion, platform = Application.platform.ToString(),
                scene = initialScene, device = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                width = initialWidth, height = initialHeight, vSyncCount = initialVsync, targetFrameRate = initialFrameCap,
                timeScale = initialTimeScale, seed = Seed, frames = sampleCount, warmupFrames = initialWarmup,
                items = Mode == MidtermMode.Baseline ? 0 : Mode == MidtermMode.NormalMarkers ? normal.ItemCount : stress.ItemCount,
                kinds = Mode == MidtermMode.Baseline ? 0 : Mode == MidtermMode.NormalMarkers ? normal.KindCount : stress.KindCount,
                checksum = LastChecksum.ToString("X16"),
                meanWorkMs = workSum / sampleCount, medianWorkMs = Median(work), p95WorkMs = Percentile95(work),
                meanFrameIntervalMs = frameSum / sampleCount, medianFrameIntervalMs = Median(frame), p95FrameIntervalMs = Percentile95(frame),
                meanWorkGcAllocCalls = (double)allocated / sampleCount,
                note = "lab_work_ms is Stopwatch time for the added experiment, not whole-game CPU time. " +
                    "frame_interval_ms includes waits and Editor overhead. lab_gc_alloc_calls counts GC.Alloc samples in this workload on the main thread only; it is not bytes. " +
                    "Baseline runs no added workload. Dataset equivalence was verified before warmup."
            };
            File.WriteAllText(Path.Combine(LastOutputDirectory, "summary.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            UnityEngine.Debug.Log("Midterm capture exported: " + LastOutputDirectory);
        }
        static double Median(double[] sorted) => sorted.Length % 2 == 0
            ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2 : sorted[sorted.Length / 2];
        static double Percentile95(double[] sorted) => sorted[(int)Math.Ceiling(sorted.Length * .95) - 1];

        [Serializable]
        sealed class CaptureReport
        {
            public string mode, unityVersion, platform, scene, device, gpu, checksum, note;
            public int width, height, vSyncCount, targetFrameRate, seed, frames, warmupFrames, items, kinds;
            public float timeScale;
            public double meanWorkMs, medianWorkMs, p95WorkMs, meanFrameIntervalMs, medianFrameIntervalMs,
                p95FrameIntervalMs, meanWorkGcAllocCalls;
        }
    }
}
#endif
