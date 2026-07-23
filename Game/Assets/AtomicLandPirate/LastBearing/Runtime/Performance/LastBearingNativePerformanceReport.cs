#nullable enable

using System;
using System.Diagnostics;

namespace AtomicLandPirate.Presentation.LastBearing.Performance
{
    [Serializable]
    public sealed class LastBearingNativePerformanceReport
    {
        public int schema_version = 1;
        public string report_kind =
            "WP0002_VGR13_NATIVE_PERFORMANCE_REPORT_V1";
        public string status = string.Empty;
        public string failure_code = string.Empty;
        public string request_nonce = string.Empty;
        public string request_sha256 = string.Empty;
        public string run_started_utc = string.Empty;
        public string report_generated_utc = string.Empty;
        public double requested_warmup_seconds;
        public double actual_warmup_seconds;
        public LastBearingNativePerformanceBuildProof build = new();
        public LastBearingNativePerformanceEnvironmentProof environment = new();
        public LastBearingNativePerformancePhaseReport paused = new();
        public LastBearingNativePerformancePhaseReport representative_unpaused =
            new();
        public LastBearingNativePerformancePhaseReport city_garage_cycles = new();
        public LastBearingNativePerformanceCycleReport cycles = new();
        public LastBearingNativePerformanceTopologyReport topology = new();
        public LastBearingNativePerformanceRetentionReport paused_retention =
            new();
        public LastBearingNativePerformanceAcceptanceReport acceptance = new();
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceBuildProof
    {
        public string source_commit = string.Empty;
        public string source_tree_sha256 = string.Empty;
        public string build_identity_sha256 = string.Empty;
        public string executable_sha256 = string.Empty;
        public string application_build_guid = string.Empty;
        public string unity_version = string.Empty;
        public bool enable_il2cpp;
        public string process_architecture = string.Empty;
        public bool arm64_process;
        public bool development_build;
        public bool request_runtime_identity_matched;
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceEnvironmentProof
    {
        public int requested_width;
        public int requested_height;
        public int actual_width;
        public int actual_height;
        public string actual_full_screen_mode = string.Empty;
        public bool exact_resolution_verified;
    }

    [Serializable]
    public sealed class LastBearingNativePerformancePhaseReport
    {
        public string phase_id = string.Empty;
        public double started_at_realtime_seconds;
        public double ended_at_realtime_seconds;
        public double measured_seconds;
        public int frame_sample_count;
        public bool frame_sample_capacity_exceeded;
        public double gc_allocated_average_bytes;
        public long gc_allocated_p95_bytes;
        public long gc_allocated_max_bytes;
        public double frame_time_average_ms;
        public double frame_time_p95_ms;
        public double frame_time_p99_ms;
        public double frame_time_max_ms;
        public int simulation_tick_sample_count;
        public bool simulation_tick_capacity_exceeded;
        public double simulation_tick_average_ms;
        public double simulation_tick_p95_ms;
        public double simulation_tick_max_ms;
    }

    [Serializable]
    public struct LastBearingNativePerformanceMemoryCheckpoint
    {
        public int completed_cycles;
        public long managed_heap_bytes;
        public long unity_allocated_bytes;
        public long unity_reserved_bytes;
    }

    public enum LastBearingNativePerformanceMemoryMetric
    {
        ManagedHeap = 0,
        UnityAllocated = 1,
        UnityReserved = 2,
    }

    public static class LastBearingNativePerformanceMemoryTrend
    {
        public static bool HasMonotonicGrowth(
            LastBearingNativePerformanceMemoryCheckpoint[] checkpoints,
            LastBearingNativePerformanceMemoryMetric metric)
        {
            if (checkpoints == null || checkpoints.Length < 2)
            {
                return true;
            }

            long first = Read(checkpoints[0], metric);
            long previous = first;
            for (var index = 1; index < checkpoints.Length; index++)
            {
                long current = Read(checkpoints[index], metric);
                if (current < previous)
                {
                    return false;
                }

                previous = current;
            }

            return previous > first;
        }

        private static long Read(
            LastBearingNativePerformanceMemoryCheckpoint checkpoint,
            LastBearingNativePerformanceMemoryMetric metric)
        {
            switch (metric)
            {
                case LastBearingNativePerformanceMemoryMetric.ManagedHeap:
                    return checkpoint.managed_heap_bytes;
                case LastBearingNativePerformanceMemoryMetric.UnityAllocated:
                    return checkpoint.unity_allocated_bytes;
                case LastBearingNativePerformanceMemoryMetric.UnityReserved:
                    return checkpoint.unity_reserved_bytes;
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric));
            }
        }
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceCycleReport
    {
        public int requested_cycles;
        public int completed_cycles;
        public bool canonical_state_unchanged;
        public string canonical_sha256_before = string.Empty;
        public string canonical_sha256_after = string.Empty;
        public bool topology_stable_at_all_checkpoints;
        public bool managed_heap_monotonic_growth_detected;
        public bool unity_allocated_monotonic_growth_detected;
        public bool unity_reserved_monotonic_growth_detected;
        public bool no_monotonic_memory_growth;
        public int post_cycle_first_submit_command_delta;
        public int post_cycle_duplicate_submit_command_delta;
        public bool post_cycle_exactly_one_action;
        public LastBearingNativePerformanceMemoryCheckpoint[]
            memory_checkpoints = Array.Empty<
                LastBearingNativePerformanceMemoryCheckpoint>();
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceTopologyReport
    {
        public int initial_owned_unity_objects;
        public int final_owned_unity_objects;
        public int initial_visual_elements;
        public int final_visual_elements;
        public int initial_bindings;
        public int final_bindings;
        public int initial_registered_callbacks;
        public int final_registered_callbacks;
        public bool exact_identity_set_retained;
        public int initial_ui_documents;
        public int final_ui_documents;
        public bool exact_ui_document_set_retained;
        public int initial_cameras;
        public int final_cameras;
        public bool exact_camera_set_retained;
        public int initial_audio_listeners;
        public int final_audio_listeners;
        public bool exact_audio_listener_set_retained;
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceRetentionReport
    {
        public long managed_heap_bytes_before;
        public long managed_heap_bytes_after;
        public long managed_heap_growth_bytes;
        public double managed_heap_growth_percent;
        public bool managed_heap_not_grown;
        public long unity_allocated_bytes_before;
        public long unity_allocated_bytes_after;
        public long unity_allocated_growth_bytes;
        public double unity_allocated_growth_percent;
        public bool unity_allocated_not_grown;
        public long unity_reserved_bytes_before;
        public long unity_reserved_bytes_after;
        public long unity_reserved_growth_bytes;
        public double unity_reserved_growth_percent;
        public bool unity_reserved_not_grown;
        public bool no_retained_memory_growth;
        public bool canonical_state_unchanged;
        public string canonical_sha256_before = string.Empty;
        public string canonical_sha256_after = string.Empty;
        public bool exact_topology_retained;
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceAcceptanceReport
    {
        public bool paused_p95_zero_bytes_per_frame;
        public bool representative_unpaused_p95_zero_bytes_per_frame;
        public bool representative_unpaused_average_below_1024_bytes_per_frame;
        public bool all_sample_buffers_populated_without_overflow;
        public bool exact_100_cycles;
        public bool stable_retained_topology;
        public bool paused_no_retained_memory_growth;
        public bool cycles_no_monotonic_memory_growth;
        public bool post_cycle_exactly_one_action;
        public bool exact_2560x1600;
        public bool passed;
    }

    internal sealed class LastBearingNativePerformanceSampleBuffer
    {
        private readonly long[] _gcAllocatedBytes;
        private readonly double[] _frameMilliseconds;
        private readonly long[] _simulationStopwatchTicks;
        private int _frameCount;
        private int _simulationCount;

        internal LastBearingNativePerformanceSampleBuffer(
            string phaseId,
            int frameCapacity,
            int simulationCapacity)
        {
            PhaseId = phaseId;
            _gcAllocatedBytes = new long[frameCapacity];
            _frameMilliseconds = new double[frameCapacity];
            _simulationStopwatchTicks = new long[simulationCapacity];
        }

        internal string PhaseId { get; }

        internal double StartedAtSeconds { get; set; }

        internal double EndedAtSeconds { get; set; }

        internal bool FrameCapacityExceeded { get; private set; }

        internal bool SimulationCapacityExceeded { get; private set; }

        internal void RecordFrame(long gcAllocatedBytes, double frameMilliseconds)
        {
            if (_frameCount >= _gcAllocatedBytes.Length)
            {
                FrameCapacityExceeded = true;
                return;
            }

            _gcAllocatedBytes[_frameCount] = Math.Max(0L, gcAllocatedBytes);
            _frameMilliseconds[_frameCount] =
                Math.Max(0d, frameMilliseconds);
            _frameCount++;
        }

        internal void RecordSimulationTick(long stopwatchTicks)
        {
            if (_simulationCount >= _simulationStopwatchTicks.Length)
            {
                SimulationCapacityExceeded = true;
                return;
            }

            _simulationStopwatchTicks[_simulationCount] =
                Math.Max(0L, stopwatchTicks);
            _simulationCount++;
        }

        internal LastBearingNativePerformancePhaseReport BuildReport()
        {
            var report = new LastBearingNativePerformancePhaseReport
            {
                phase_id = PhaseId,
                started_at_realtime_seconds = StartedAtSeconds,
                ended_at_realtime_seconds = EndedAtSeconds,
                measured_seconds = Math.Max(
                    0d,
                    EndedAtSeconds - StartedAtSeconds),
                frame_sample_count = _frameCount,
                frame_sample_capacity_exceeded = FrameCapacityExceeded,
                simulation_tick_sample_count = _simulationCount,
                simulation_tick_capacity_exceeded =
                    SimulationCapacityExceeded,
            };

            if (_frameCount > 0)
            {
                double gcSum = 0d;
                long gcMaximum = 0L;
                double frameSum = 0d;
                double frameMaximum = 0d;
                for (var index = 0; index < _frameCount; index++)
                {
                    long gc = _gcAllocatedBytes[index];
                    double frame = _frameMilliseconds[index];
                    gcSum += gc;
                    frameSum += frame;
                    if (gc > gcMaximum) gcMaximum = gc;
                    if (frame > frameMaximum) frameMaximum = frame;
                }

                report.gc_allocated_average_bytes = gcSum / _frameCount;
                report.gc_allocated_max_bytes = gcMaximum;
                report.frame_time_average_ms = frameSum / _frameCount;
                report.frame_time_max_ms = frameMaximum;
                Array.Sort(_gcAllocatedBytes, 0, _frameCount);
                Array.Sort(_frameMilliseconds, 0, _frameCount);
                report.gc_allocated_p95_bytes =
                    Percentile(_gcAllocatedBytes, _frameCount, 0.95d);
                report.frame_time_p95_ms =
                    Percentile(_frameMilliseconds, _frameCount, 0.95d);
                report.frame_time_p99_ms =
                    Percentile(_frameMilliseconds, _frameCount, 0.99d);
            }

            if (_simulationCount > 0)
            {
                double sumTicks = 0d;
                long maximumTicks = 0L;
                for (var index = 0; index < _simulationCount; index++)
                {
                    long ticks = _simulationStopwatchTicks[index];
                    sumTicks += ticks;
                    if (ticks > maximumTicks) maximumTicks = ticks;
                }

                Array.Sort(
                    _simulationStopwatchTicks,
                    0,
                    _simulationCount);
                double millisecondsPerTick =
                    1000d / Stopwatch.Frequency;
                report.simulation_tick_average_ms =
                    sumTicks / _simulationCount * millisecondsPerTick;
                report.simulation_tick_p95_ms =
                    Percentile(
                        _simulationStopwatchTicks,
                        _simulationCount,
                        0.95d) * millisecondsPerTick;
                report.simulation_tick_max_ms =
                    maximumTicks * millisecondsPerTick;
            }

            return report;
        }

        internal static double GrowthPercent(double before, double after)
        {
            if (before <= 0d)
            {
                return after <= 0d ? 0d : 100d;
            }

            return (after - before) / before * 100d;
        }

        private static long Percentile(
            long[] sorted,
            int count,
            double percentile)
        {
            int index = Math.Max(
                0,
                Math.Min(
                    count - 1,
                    (int)Math.Ceiling(percentile * count) - 1));
            return sorted[index];
        }

        private static double Percentile(
            double[] sorted,
            int count,
            double percentile)
        {
            int index = Math.Max(
                0,
                Math.Min(
                    count - 1,
                    (int)Math.Ceiling(percentile * count) - 1));
            return sorted[index];
        }

    }
}
