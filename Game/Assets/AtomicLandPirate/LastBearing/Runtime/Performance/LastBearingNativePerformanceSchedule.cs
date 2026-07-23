#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Performance
{
    public interface ILastBearingNativePerformanceClock
    {
        double NowSeconds { get; }
    }

    public sealed class LastBearingUnityRealtimeClock :
        ILastBearingNativePerformanceClock
    {
        public double NowSeconds => Time.realtimeSinceStartupAsDouble;
    }

    public sealed class LastBearingNativePerformanceDurations
    {
        public LastBearingNativePerformanceDurations(
            double warmupSeconds,
            double pausedSeconds,
            double representativeUnpausedSeconds,
            int cityGarageCycles,
            double cycleHalfDwellSeconds)
        {
            if (warmupSeconds <= 0d ||
                pausedSeconds <= 0d ||
                representativeUnpausedSeconds <= 0d ||
                cityGarageCycles <= 0 ||
                cycleHalfDwellSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warmupSeconds),
                    "all durations and the cycle count must be positive");
            }

            WarmupSeconds = warmupSeconds;
            PausedSeconds = pausedSeconds;
            RepresentativeUnpausedSeconds =
                representativeUnpausedSeconds;
            CityGarageCycles = cityGarageCycles;
            CycleHalfDwellSeconds = cycleHalfDwellSeconds;
        }

        public double WarmupSeconds { get; }

        public double PausedSeconds { get; }

        public double RepresentativeUnpausedSeconds { get; }

        public int CityGarageCycles { get; }

        public double CycleHalfDwellSeconds { get; }

        public static LastBearingNativePerformanceDurations Production()
        {
            return new LastBearingNativePerformanceDurations(
                warmupSeconds: 300d,
                pausedSeconds: 300d,
                representativeUnpausedSeconds: 300d,
                cityGarageCycles: 100,
                cycleHalfDwellSeconds: 0.25d);
        }
    }

    public enum LastBearingNativePerformanceStage
    {
        NotStarted = 0,
        Warmup = 1,
        AwaitingPausedMeasurement = 2,
        PausedMeasurement = 3,
        AwaitingRepresentativeUnpausedMeasurement = 4,
        RepresentativeUnpausedMeasurement = 5,
        AwaitingCyclePause = 6,
        CityGarageCycles = 7,
        AwaitingPostCycleResume = 8,
        Complete = 9,
        SettlingPausedMeasurement = 10,
    }

    public enum LastBearingNativePerformanceAction
    {
        None = 0,
        BeginWarmup = 1,
        RequestPauseForPausedMeasurement = 2,
        BeginPausedMeasurement = 3,
        EndPausedMeasurementAndRequestResume = 4,
        BeginRepresentativeUnpausedMeasurement = 5,
        EndRepresentativeUnpausedMeasurementAndRequestPause = 6,
        BeginCityGarageCycles = 7,
        ShowGarage = 8,
        ShowCity = 9,
        EndCityGarageCyclesAndSubmitResume = 10,
        Complete = 11,
        PreparePausedMeasurement = 12,
        FailPausedMeasurementDrift = 13,
    }

    /// <summary>
    /// Deterministic wall-clock schedule for the native player harness. The
    /// clock and durations are injected so tests can exercise the exact phase
    /// order without waiting for production-length measurements.
    /// </summary>
    public sealed class LastBearingNativePerformanceSchedule
    {
        private const double PausedSettleSeconds = 5d;

        private readonly ILastBearingNativePerformanceClock _clock;
        private readonly LastBearingNativePerformanceDurations _durations;
        private double _stageStartedAt;
        private double _nextCycleActionAt;
        private bool _garageLegActive;

        public LastBearingNativePerformanceSchedule(
            ILastBearingNativePerformanceClock clock,
            LastBearingNativePerformanceDurations durations)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _durations = durations ??
                throw new ArgumentNullException(nameof(durations));
        }

        public LastBearingNativePerformanceStage Stage { get; private set; }

        public int CompletedCityGarageCycles { get; private set; }

        /// <summary>
        /// Rebases the measured interval after the harness has captured its
        /// collected baseline, so checkpoint work cannot shorten 300 seconds.
        /// </summary>
        public void ConfirmPausedMeasurementStarted()
        {
            if (Stage != LastBearingNativePerformanceStage.PausedMeasurement)
            {
                throw new InvalidOperationException(
                    "paused measurement can be confirmed only after it begins");
            }

            _stageStartedAt = _clock.NowSeconds;
        }

        /// <summary>
        /// Rebases the next transition after the requested presentation has
        /// actually been applied, preserving the full rendered half-dwell.
        /// </summary>
        public void ConfirmCyclePresentationApplied()
        {
            if (Stage != LastBearingNativePerformanceStage.CityGarageCycles)
            {
                throw new InvalidOperationException(
                    "cycle presentation can be confirmed only during cycles");
            }

            _nextCycleActionAt =
                _clock.NowSeconds + _durations.CycleHalfDwellSeconds;
        }

        public LastBearingNativePerformanceAction Start()
        {
            if (Stage != LastBearingNativePerformanceStage.NotStarted)
            {
                throw new InvalidOperationException(
                    "native performance schedule already started");
            }

            Stage = LastBearingNativePerformanceStage.Warmup;
            _stageStartedAt = _clock.NowSeconds;
            return LastBearingNativePerformanceAction.BeginWarmup;
        }

        public LastBearingNativePerformanceAction Advance(bool isPaused)
        {
            double now = _clock.NowSeconds;
            switch (Stage)
            {
                case LastBearingNativePerformanceStage.Warmup:
                    if (Elapsed(now) < _durations.WarmupSeconds)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    Stage = LastBearingNativePerformanceStage.AwaitingPausedMeasurement;
                    return LastBearingNativePerformanceAction
                        .RequestPauseForPausedMeasurement;

                case LastBearingNativePerformanceStage.AwaitingPausedMeasurement:
                    if (!isPaused)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    BeginStage(
                        LastBearingNativePerformanceStage
                            .SettlingPausedMeasurement,
                        now);
                    return LastBearingNativePerformanceAction
                        .PreparePausedMeasurement;

                case LastBearingNativePerformanceStage
                    .SettlingPausedMeasurement:
                    if (!isPaused)
                    {
                        return LastBearingNativePerformanceAction
                            .FailPausedMeasurementDrift;
                    }

                    if (Elapsed(now) < PausedSettleSeconds)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    BeginStage(
                        LastBearingNativePerformanceStage.PausedMeasurement,
                        now);
                    return LastBearingNativePerformanceAction.BeginPausedMeasurement;

                case LastBearingNativePerformanceStage.PausedMeasurement:
                    if (!isPaused)
                    {
                        return LastBearingNativePerformanceAction
                            .FailPausedMeasurementDrift;
                    }

                    if (Elapsed(now) < _durations.PausedSeconds)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    Stage = LastBearingNativePerformanceStage
                        .AwaitingRepresentativeUnpausedMeasurement;
                    return LastBearingNativePerformanceAction
                        .EndPausedMeasurementAndRequestResume;

                case LastBearingNativePerformanceStage
                    .AwaitingRepresentativeUnpausedMeasurement:
                    if (isPaused)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    BeginStage(
                        LastBearingNativePerformanceStage
                            .RepresentativeUnpausedMeasurement,
                        now);
                    return LastBearingNativePerformanceAction
                        .BeginRepresentativeUnpausedMeasurement;

                case LastBearingNativePerformanceStage
                    .RepresentativeUnpausedMeasurement:
                    if (Elapsed(now) <
                        _durations.RepresentativeUnpausedSeconds)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    Stage = LastBearingNativePerformanceStage.AwaitingCyclePause;
                    return LastBearingNativePerformanceAction
                        .EndRepresentativeUnpausedMeasurementAndRequestPause;

                case LastBearingNativePerformanceStage.AwaitingCyclePause:
                    if (!isPaused)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    BeginStage(
                        LastBearingNativePerformanceStage.CityGarageCycles,
                        now);
                    _nextCycleActionAt =
                        now + _durations.CycleHalfDwellSeconds;
                    _garageLegActive = false;
                    CompletedCityGarageCycles = 0;
                    return LastBearingNativePerformanceAction
                        .BeginCityGarageCycles;

                case LastBearingNativePerformanceStage.CityGarageCycles:
                    if (now < _nextCycleActionAt)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    if (CompletedCityGarageCycles >=
                        _durations.CityGarageCycles)
                    {
                        Stage = LastBearingNativePerformanceStage
                            .AwaitingPostCycleResume;
                        return LastBearingNativePerformanceAction
                            .EndCityGarageCyclesAndSubmitResume;
                    }

                    _nextCycleActionAt =
                        now + _durations.CycleHalfDwellSeconds;
                    if (!_garageLegActive)
                    {
                        _garageLegActive = true;
                        return LastBearingNativePerformanceAction.ShowGarage;
                    }

                    _garageLegActive = false;
                    CompletedCityGarageCycles++;
                    return LastBearingNativePerformanceAction.ShowCity;

                case LastBearingNativePerformanceStage.AwaitingPostCycleResume:
                    if (isPaused)
                    {
                        return LastBearingNativePerformanceAction.None;
                    }

                    Stage = LastBearingNativePerformanceStage.Complete;
                    return LastBearingNativePerformanceAction.Complete;

                default:
                    return LastBearingNativePerformanceAction.None;
            }
        }

        private double Elapsed(double now)
        {
            return Math.Max(0d, now - _stageStartedAt);
        }

        private void BeginStage(
            LastBearingNativePerformanceStage stage,
            double now)
        {
            Stage = stage;
            _stageStartedAt = now;
        }
    }
}
