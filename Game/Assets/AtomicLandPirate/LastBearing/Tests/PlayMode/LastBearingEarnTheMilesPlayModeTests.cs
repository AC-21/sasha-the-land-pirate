#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingPlayModeTests
    {
        [UnityTest]
        public IEnumerator CorrectTractionEarnsOutboundAndReturnMilesForBothRoutes()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<
                    LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.enabled = false;

            LastBearingState atHome =
                CreateAtHomeModuleState(VehicleModule.WinchAssembly);
            InstallControllerState(controller, atHome);
            byte[] atHomeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.ShowCityOverview();
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                controller.OpenGarageBay();
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                CollectionAssert.AreEqual(
                    atHomeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(
                    controller.ModeCoordinator!.ActiveModeCount,
                    Is.EqualTo(1));
            }

            var adapter = new EarnMilesRoadAdapter();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var cases = new[]
            {
                new EarnMilesAcceptedCase(
                    "winch apron outbound",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Concrete,
                    returning: false),
                new EarnMilesAcceptedCase(
                    "winch washboard outbound",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    returning: false),
                new EarnMilesAcceptedCase(
                    "range apron outbound",
                    VehicleModule.SealedRangeTank,
                    RoadFeelSurfaceKind.Concrete,
                    returning: false),
                new EarnMilesAcceptedCase(
                    "range sand outbound",
                    VehicleModule.SealedRangeTank,
                    RoadFeelSurfaceKind.Sand,
                    returning: false),
                new EarnMilesAcceptedCase(
                    "range gravel outbound",
                    VehicleModule.SealedRangeTank,
                    RoadFeelSurfaceKind.Gravel,
                    returning: false),
                new EarnMilesAcceptedCase(
                    "winch washboard return",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    returning: true),
                new EarnMilesAcceptedCase(
                    "range gravel return",
                    VehicleModule.SealedRangeTank,
                    RoadFeelSurfaceKind.Gravel,
                    returning: true),
            };

            foreach (EarnMilesAcceptedCase sample in cases)
            {
                LastBearingState state = sample.Returning
                    ? CreateEarnMilesReturningState(sample.Module)
                    : CreateOutboundState(module: sample.Module);
                InstallControllerState(controller, state);
                adapter.SetEvidence(
                    sourceActive: true,
                    sourceHealthy: true,
                    forwardSpeedMetresPerSecond: 3f,
                    groundedContacts: 4,
                    surface: sample.Surface,
                    recovering: false);
                controller.AttachRoadModeAdapter(adapter);

                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.Driving),
                    sample.Label);
                Assert.That(
                    controller.ReadModel!.ExpeditionPhase,
                    Is.EqualTo(
                        sample.Returning
                            ? ExpeditionPhase.Returning
                            : ExpeditionPhase.Outbound),
                    sample.Label);
                long progressBefore = controller.State!.RouteProgressTicks;
                long sequenceBefore = controller.State!.NextCommandSequence;
                byte[] canonicalBefore =
                    LastBearingCanonicalCodec.Encode(controller.State!);

                Press(keyboard.wKey);
                InvokeRoadPresentationInput(controller);
                InvokeEarnMilesRoadInputQueue(controller);
                Release(keyboard.wKey);

                Assert.That(
                    adapter.LastPresentationThrottleMilli,
                    Is.EqualTo(1000),
                    sample.Label);
                Assert.That(
                    adapter.LastRouteProfile,
                    Is.EqualTo(ExpectedRouteProfile(sample.Module)),
                    sample.Label);
                Assert.That(
                    controller.RoadTractionEvidence.Reason,
                    Is.EqualTo(RoadFeelTractionEvidenceReason.Ready),
                    sample.Label);
                LastBearingCommand[] pending = PendingCommands(controller);
                Assert.That(pending, Has.Length.EqualTo(1), sample.Label);
                Assert.That(
                    pending[0],
                    Is.TypeOf<DriveVehicleCommand>(),
                    sample.Label);
                Assert.That(
                    pending[0].Sequence,
                    Is.EqualTo(sequenceBefore),
                    sample.Label);
                CollectionAssert.AreEqual(
                    canonicalBefore,
                    LastBearingCanonicalCodec.Encode(controller.State!),
                    sample.Label + " mutated canonical state before its tick");

                InvokeSimulationTick(controller);

                Assert.That(
                    controller.State!.RouteProgressTicks,
                    Is.EqualTo(progressBefore + 1),
                    sample.Label);
                Assert.That(
                    controller.State!.NextCommandSequence,
                    Is.EqualTo(sequenceBefore + 1),
                    sample.Label);
                Assert.That(
                    PendingCommandCount(controller),
                    Is.Zero,
                    sample.Label);
            }
        }

        [UnityTest]
        public IEnumerator RejectedTractionPreservesStateQueueAndProtectedSaves()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<
                    LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            var adapter = new EarnMilesRoadAdapter();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var cases = new[]
            {
                new EarnMilesRejectedCase(
                    "winch on range sand",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Sand,
                    4,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.WrongSurface),
                new EarnMilesRejectedCase(
                    "range tank on winch washboard",
                    VehicleModule.SealedRangeTank,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.WrongSurface),
                new EarnMilesRejectedCase(
                    "airborne",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    0,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason: RoadFeelTractionEvidenceReason
                        .InsufficientGroundContacts),
                new EarnMilesRejectedCase(
                    "one wheel touching",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    1,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason: RoadFeelTractionEvidenceReason
                        .InsufficientGroundContacts),
                new EarnMilesRejectedCase(
                    "stationary",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    0f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.NotMovingForward),
                new EarnMilesRejectedCase(
                    "reverse",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    -3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.NotMovingForward),
                new EarnMilesRejectedCase(
                    "recovering",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: true,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.Recovering),
                new EarnMilesRejectedCase(
                    "adapter inactive",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    3f,
                    sourceActive: false,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: false,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.AdapterInactive),
                new EarnMilesRejectedCase(
                    "adapter fault",
                    VehicleModule.WinchAssembly,
                    RoadFeelSurfaceKind.Washboard,
                    4,
                    3f,
                    sourceActive: true,
                    sourceHealthy: true,
                    recovering: false,
                    throwOnCapture: true,
                    expectedReason:
                        RoadFeelTractionEvidenceReason.AdapterFaulted),
            };

            foreach (EarnMilesRejectedCase sample in cases)
            {
                LastBearingState state =
                    CreateOutboundState(module: sample.Module);
                InstallControllerState(controller, state);
                adapter.SetEvidence(
                    sample.SourceActive,
                    sample.SourceHealthy,
                    sample.ForwardSpeedMetresPerSecond,
                    sample.GroundedContacts,
                    sample.Surface,
                    sample.Recovering,
                    sample.ThrowOnCapture);
                controller.AttachRoadModeAdapter(adapter);
                controller.Save();
                Assert.That(
                    controller.SaveStatus,
                    Does.Not.Contain("failed"),
                    sample.Label);
                Assert.That(
                    controller.SaveStatus,
                    Does.Not.Contain("deferred"),
                    sample.Label);

                byte[] canonicalBefore =
                    LastBearingCanonicalCodec.Encode(controller.State!);
                string hashBefore = controller.CanonicalHash;
                long sequenceBefore = controller.State!.NextCommandSequence;
                long progressBefore = controller.State!.RouteProgressTicks;
                LastBearingCommand[] pendingBefore =
                    PendingCommands(controller);
                Dictionary<string, string> savesBefore =
                    SnapshotSaveFiles(profileDirectory);

                Press(keyboard.wKey);
                if (sample.ThrowOnCapture)
                {
                    LogAssert.Expect(
                        LogType.Warning,
                        "LAST_BEARING_ROAD_PRESENTATION_DISABLED " +
                        "capture-traction-evidence " +
                        "InvalidOperationException");
                }

                InvokeRoadPresentationInput(controller);
                InvokeEarnMilesRoadInputQueue(controller);
                Release(keyboard.wKey);

                Assert.That(
                    adapter.LastPresentationThrottleMilli,
                    Is.EqualTo(1000),
                    sample.Label + " swallowed physical input");
                Assert.That(
                    controller.RoadTractionEvidence.Reason,
                    Is.EqualTo(sample.ExpectedReason),
                    sample.Label);
                CollectionAssert.AreEqual(
                    canonicalBefore,
                    LastBearingCanonicalCodec.Encode(controller.State!),
                    sample.Label);
                Assert.That(
                    controller.CanonicalHash,
                    Is.EqualTo(hashBefore),
                    sample.Label);
                Assert.That(
                    controller.State!.NextCommandSequence,
                    Is.EqualTo(sequenceBefore),
                    sample.Label);
                Assert.That(
                    controller.State!.RouteProgressTicks,
                    Is.EqualTo(progressBefore),
                    sample.Label);
                CollectionAssert.AreEqual(
                    pendingBefore,
                    PendingCommands(controller),
                    sample.Label);
                AssertSaveSnapshot(
                    savesBefore,
                    SnapshotSaveFiles(profileDirectory));

                adapter.SetEvidence(
                    sourceActive: true,
                    sourceHealthy: true,
                    forwardSpeedMetresPerSecond: 3f,
                    groundedContacts: 4,
                    surface: ExpectedValidSurface(sample.Module),
                    recovering: false,
                    throwOnCapture: false);
                controller.AttachRoadModeAdapter(adapter);
                Press(keyboard.wKey);
                InvokeRoadPresentationInput(controller);
                InvokeEarnMilesRoadInputQueue(controller);
                Release(keyboard.wKey);
                Assert.That(
                    PendingCommands(controller),
                    Has.Length.EqualTo(1),
                    sample.Label + " left a hidden rejection latch");
                Assert.That(
                    controller.RoadTractionEvidence.CanAdvance,
                    Is.True,
                    sample.Label);

                InvokeSimulationTick(controller);

                Assert.That(
                    controller.State!.RouteProgressTicks,
                    Is.EqualTo(progressBefore + 1),
                    sample.Label + " did not recover on the next valid reading");
                Assert.That(
                    controller.State!.NextCommandSequence,
                    Is.EqualTo(sequenceBefore + 1),
                    sample.Label);
            }
        }

        private static LastBearingState CreateEarnMilesReturningState(
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilDepotRecoveryAvailable(
                CreateOutboundState(module: module));
            state = Apply(kernel, state, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            state = Apply(kernel, state, sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                state = Apply(kernel, state, sequence =>
                    new ChooseLiquidReturnCommand(
                        sequence,
                        LiquidCargoKind.Water));
            }

            string transactionId = state.TransactionId!;
            string fingerprint = state.TransactionFingerprint!;
            state = Apply(kernel, state, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            Assert.That(
                state.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(state.RouteProgressTicks, Is.Zero);
            return state;
        }

        private static void InvokeEarnMilesRoadInputQueue(
            LastBearingGameController controller)
        {
            MethodInfo? queue =
                typeof(LastBearingGameController).GetMethod(
                    "QueueDriveInputIfApplicable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queue, Is.Not.Null);
            queue!.Invoke(controller, null);
        }

        private static RoadFeelRouteProfile ExpectedRouteProfile(
            VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly
                ? RoadFeelRouteProfile.WinchLine
                : RoadFeelRouteProfile.RangeLine;
        }

        private static RoadFeelSurfaceKind ExpectedValidSurface(
            VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly
                ? RoadFeelSurfaceKind.Washboard
                : RoadFeelSurfaceKind.Gravel;
        }

        private readonly struct EarnMilesAcceptedCase
        {
            public EarnMilesAcceptedCase(
                string label,
                VehicleModule module,
                RoadFeelSurfaceKind surface,
                bool returning)
            {
                Label = label;
                Module = module;
                Surface = surface;
                Returning = returning;
            }

            public string Label { get; }
            public VehicleModule Module { get; }
            public RoadFeelSurfaceKind Surface { get; }
            public bool Returning { get; }
        }

        private readonly struct EarnMilesRejectedCase
        {
            public EarnMilesRejectedCase(
                string label,
                VehicleModule module,
                RoadFeelSurfaceKind surface,
                int groundedContacts,
                float forwardSpeedMetresPerSecond,
                bool sourceActive,
                bool sourceHealthy,
                bool recovering,
                bool throwOnCapture,
                RoadFeelTractionEvidenceReason expectedReason)
            {
                Label = label;
                Module = module;
                Surface = surface;
                GroundedContacts = groundedContacts;
                ForwardSpeedMetresPerSecond =
                    forwardSpeedMetresPerSecond;
                SourceActive = sourceActive;
                SourceHealthy = sourceHealthy;
                Recovering = recovering;
                ThrowOnCapture = throwOnCapture;
                ExpectedReason = expectedReason;
            }

            public string Label { get; }
            public VehicleModule Module { get; }
            public RoadFeelSurfaceKind Surface { get; }
            public int GroundedContacts { get; }
            public float ForwardSpeedMetresPerSecond { get; }
            public bool SourceActive { get; }
            public bool SourceHealthy { get; }
            public bool Recovering { get; }
            public bool ThrowOnCapture { get; }
            public RoadFeelTractionEvidenceReason ExpectedReason { get; }
        }

        private sealed class EarnMilesRoadAdapter :
            ILastBearingRoadModeAdapter,
            ILastBearingRoadTractionEvidenceSource
        {
            private bool _sourceActive = true;
            private bool _sourceHealthy = true;
            private float _forwardSpeedMetresPerSecond = 3f;
            private int _groundedContacts = 4;
            private RoadFeelSurfaceKind _surface =
                RoadFeelSurfaceKind.Washboard;
            private bool _recovering;
            private bool _throwOnCapture;

            public bool IsRoadModeActive { get; private set; }
            public int LastPresentationThrottleMilli { get; private set; }
            public RoadFeelRouteProfile LastRouteProfile { get; private set; }

            public void SetEvidence(
                bool sourceActive,
                bool sourceHealthy,
                float forwardSpeedMetresPerSecond,
                int groundedContacts,
                RoadFeelSurfaceKind surface,
                bool recovering,
                bool throwOnCapture = false)
            {
                _sourceActive = sourceActive;
                _sourceHealthy = sourceHealthy;
                _forwardSpeedMetresPerSecond =
                    forwardSpeedMetresPerSecond;
                _groundedContacts = groundedContacts;
                _surface = surface;
                _recovering = recovering;
                _throwOnCapture = throwOnCapture;
            }

            public RoadFeelTractionEvidence CaptureTractionEvidence(
                RoadFeelRouteProfile routeProfile)
            {
                LastRouteProfile = routeProfile;
                if (_throwOnCapture)
                {
                    throw new System.InvalidOperationException(
                        "earn-miles-traction-source-fault");
                }

                return RoadFeelTractionQuantizer.Evaluate(
                    IsRoadModeActive && _sourceActive,
                    _sourceHealthy,
                    routeProfile,
                    new RoadFeelTelemetry(
                        Mathf.Abs(_forwardSpeedMetresPerSecond),
                        _forwardSpeedMetresPerSecond,
                        0f,
                        0f,
                        0f,
                        _groundedContacts,
                        _groundedContacts > 0 ? 0.5f : 0f,
                        _surface,
                        0f,
                        RoadFeelDamageBand.Healthy,
                        _recovering));
            }

            public void SetRoadModeActive(bool active)
            {
                IsRoadModeActive = active;
            }

            public void ApplyQuantizedCommandShadow(
                int throttleMilli,
                int steeringMilli)
            {
            }

            public void ApplyPresentationOnlyControls(
                int brakeMilli,
                int handbrakeMilli)
            {
            }

            public void ApplyPresentationInputSample(
                int throttleMilli,
                int brakeMilli,
                int steeringMilli,
                int handbrakeMilli)
            {
                LastPresentationThrottleMilli = throttleMilli;
            }

            public void ApplyDerivedPresentationLoad(
                int cargoMassKilograms,
                LastBearingRoadDamageBand damageBand)
            {
            }

            public void SynchronizePresentationPose(
                Vector3 position,
                Quaternion rotation)
            {
            }

            public void ResetPresentation()
            {
                LastPresentationThrottleMilli = 0;
            }
        }
    }

    internal sealed class LastBearingReadyTractionRoadAdapter :
        ILastBearingRoadModeAdapter,
        ILastBearingRoadTractionEvidenceSource
    {
        public bool IsRoadModeActive { get; private set; }

        public RoadFeelTractionEvidence CaptureTractionEvidence(
            RoadFeelRouteProfile routeProfile)
        {
            RoadFeelSurfaceKind surface =
                routeProfile == RoadFeelRouteProfile.WinchLine
                    ? RoadFeelSurfaceKind.Washboard
                    : RoadFeelSurfaceKind.Gravel;
            return RoadFeelTractionQuantizer.Evaluate(
                adapterActive: IsRoadModeActive,
                adapterHealthy: true,
                route: routeProfile,
                telemetry: new RoadFeelTelemetry(
                    speedMetresPerSecond: 3f,
                    forwardSpeedMetresPerSecond: 3f,
                    yawRateDegreesPerSecond: 0f,
                    bodySlipDegrees: 0f,
                    steeringAngleDegrees: 0f,
                    groundedContacts: 4,
                    averageCompression: 0.5f,
                    dominantSurface: surface,
                    cargoMassKilograms: 0f,
                    damageBand: RoadFeelDamageBand.Healthy,
                    recovering: false));
        }

        public void SetRoadModeActive(bool active)
        {
            IsRoadModeActive = active;
        }

        public void ApplyQuantizedCommandShadow(
            int throttleMilli,
            int steeringMilli)
        {
        }

        public void ApplyPresentationOnlyControls(
            int brakeMilli,
            int handbrakeMilli)
        {
        }

        public void ApplyPresentationInputSample(
            int throttleMilli,
            int brakeMilli,
            int steeringMilli,
            int handbrakeMilli)
        {
        }

        public void ApplyDerivedPresentationLoad(
            int cargoMassKilograms,
            LastBearingRoadDamageBand damageBand)
        {
        }

        public void SynchronizePresentationPose(
            Vector3 position,
            Quaternion rotation)
        {
        }

        public void ResetPresentation()
        {
        }
    }
}
