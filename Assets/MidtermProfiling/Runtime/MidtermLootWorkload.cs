#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace DungeonSweeper.Midterm
{
    // Unity Mono can return zero from GC.GetAllocatedBytesForCurrentThread even when allocations occur.
    // Count actual GC.Alloc samples instead; inspect bytes in the Profiler's GC Alloc column.
    public sealed class MidtermAllocationProbe : IDisposable
    {
        ProfilerRecorder recorder;
        public MidtermAllocationProbe()
        {
            recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid)
            {
                recorder.Dispose();
                throw new InvalidOperationException("Unity GC.Alloc recorder is unavailable.");
            }
        }
        public void Begin() { recorder.Reset(); recorder.Start(); }
        public long End()
        {
            recorder.Stop();
            return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }
        public void Dispose() => recorder.Dispose();
    }

    // Synthetic warehouse data only; never reads or writes the game's inventory/save state.
    public sealed class MidtermLootWorkload
    {
        public const string NormalMarker = "Midterm.Normal.LootSummary";
        public const string SlowMarker = "Midterm.Bottleneck.LootSummary";
        public const string FastMarker = "Midterm.Optimized.LootSummary";
        static readonly ProfilerMarker Normal = new ProfilerMarker(NormalMarker);
        static readonly ProfilerMarker Slow = new ProfilerMarker(SlowMarker);
        static readonly ProfilerMarker Groups = new ProfilerMarker("Midterm.Bottleneck.ScanAndAllocateGroups");
        static readonly ProfilerMarker Aggregate = new ProfilerMarker("Midterm.Bottleneck.AggregateGroups");
        static readonly ProfilerMarker Fast = new ProfilerMarker(FastMarker);
        static readonly ProfilerMarker Clear = new ProfilerMarker("Midterm.Optimized.ClearBuffers");
        static readonly ProfilerMarker SinglePass = new ProfilerMarker("Midterm.Optimized.SinglePass");

        struct Loot { public int Kind, Value; }
        readonly Loot[] items;
        readonly int[] counts;
        readonly long[] values;
        public int ItemCount => items.Length;
        public int KindCount => counts.Length;

        public MidtermLootWorkload(int itemCount, int kindCount, int seed)
        {
            if (itemCount < 0 || itemCount > 131072) throw new ArgumentOutOfRangeException(nameof(itemCount));
            if (kindCount < 1 || kindCount > 256) throw new ArgumentOutOfRangeException(nameof(kindCount));
            items = new Loot[itemCount];
            counts = new int[kindCount];
            values = new long[kindCount];
            var random = new Random(seed); // Does not change UnityEngine.Random's gameplay state.
            for (int i = 0; i < items.Length; i++)
                items[i] = new Loot { Kind = random.Next(kindCount), Value = random.Next(1, 1001) };
        }

        public ulong RunNormal()
        {
            using (Normal.Auto()) return SinglePassSummary();
        }

        public ulong RunBottleneck()
        {
            using (Slow.Auto())
            {
                for (int kind = 0; kind < KindCount; kind++)
                {
                    Loot[] group;
                    using (Groups.Auto())
                    {
                        var matches = new List<Loot>();
                        for (int i = 0; i < items.Length; i++)
                            if (items[i].Kind == kind) matches.Add(items[i]);
                        group = matches.ToArray(); // Intentionally allocates every frame.
                    }
                    using (Aggregate.Auto())
                    {
                        counts[kind] = group.Length;
                        long total = 0;
                        for (int i = 0; i < group.Length; i++) total += group[i].Value;
                        values[kind] = total;
                    }
                }
                return Checksum();
            }
        }

        public ulong RunOptimized()
        {
            using (Fast.Auto()) return SinglePassSummary();
        }

        ulong SinglePassSummary()
        {
            using (Clear.Auto())
            {
                Array.Clear(counts, 0, counts.Length);
                Array.Clear(values, 0, values.Length);
            }
            using (SinglePass.Auto())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    Loot item = items[i];
                    counts[item.Kind]++;
                    values[item.Kind] += item.Value;
                }
            }
            return Checksum();
        }

        ulong Checksum()
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int kind = 0; kind < counts.Length; kind++)
                {
                    hash = (hash ^ (ulong)counts[kind]) * 1099511628211UL;
                    hash = (hash ^ (ulong)values[kind]) * 1099511628211UL;
                }
                return hash;
            }
        }

        // Exact per-category checks as well as the checksum; run outside the measured frames.
        public void VerifyEquivalentResults()
        {
            ulong slow = RunBottleneck();
            int[] expectedCounts = (int[])counts.Clone();
            long[] expectedValues = (long[])values.Clone();
            if (slow != RunOptimized()) throw new InvalidOperationException("Loot summary checksum differs.");
            for (int i = 0; i < counts.Length; i++)
                if (expectedCounts[i] != counts[i] || expectedValues[i] != values[i])
                    throw new InvalidOperationException("Loot summary differs for kind " + i);
        }
    }
}
#endif
