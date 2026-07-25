#nullable enable

using System;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingRoadDeskPresenterTests
    {
        private const string TransactionId =
            "transaction:eyes-on-bone-road:4701";
        private const string Fingerprint =
            "fingerprint:eyes-on-bone-road:4701";

        [Test]
        public void OutboundProjectionIsPureAndReadsTheStrictBoneLine()
        {
            LastBearingState state =
                CreateDrivingState(VehicleModule.WinchAssembly);
            for (var index = 0; index < 16; index++)
            {
                state = Apply(
                    state,
                    sequence =>
                        new DriveVehicleCommand(sequence, 0, 1000));
            }

            LastBearingReadModel model =
                LastBearingReadModel.FromState(state);
            byte[] before = LastBearingCanonicalCodec.Encode(state);

            LastBearingRoadDeskProjection projection =
                LastBearingRoadDeskPresenter.Present(model);

            Assert.That(projection.Leg, Is.EqualTo("OUTBOUND"));
            Assert.That(projection.Route, Does.StartWith("WRECK LINE · 0 / "));
            Assert.That(projection.RouteProgressPercent, Is.Zero);
            Assert.That(
                projection.Scout,
                Is.EqualTo("SCOUT · 1000 / 1000 · HEALTHY"));
            Assert.That(projection.Cargo, Is.EqualTo("LOAD · EMPTY"));
            Assert.That(projection.CargoMassKilograms, Is.Zero);
            Assert.That(
                projection.EdgeState,
                Is.EqualTo(LastBearingRoadSafeLineState.RightRisk));
            Assert.That(
                projection.Edge,
                Is.EqualTo("RIGHT OXIDE EDGE · STEER LEFT"));
            Assert.That(
                projection.NextVerb,
                Is.EqualTo("KEEP SCOUT ON THE BONE ROAD"));
            Assert.That(projection.Controls, Does.Contain("W / RT GO"));
            Assert.That(projection.Controls, Does.Contain("E / A WORK"));
            Assert.That(projection.Controls, Does.Not.Contain("RECENTER"));
            Assert.That(
                LastBearingRoadDeskPresenter.Present(
                    model,
                    canRecoverRoadPresentation: true).Controls,
                Does.Contain("R / Y RECENTER"));
            Assert.That(
                LastBearingCanonicalCodec.Encode(state),
                Is.EqualTo(before));
        }

        [Test]
        public void ExplicitPauseOffersOnlyTheOperativeResumeKey()
        {
            LastBearingState state =
                CreateDrivingState(VehicleModule.WinchAssembly);
            state = Apply(
                state,
                sequence => new SetPauseCommand(
                    sequence,
                    isPaused: true));

            Assert.That(
                LastBearingRoadDeskPresenter.Present(
                    LastBearingReadModel.FromState(state)).NextVerb,
                Is.EqualTo("ROAD CLOCK HELD · P TO RESUME"));
        }

        [TestCase(
            VehicleModule.WinchAssembly,
            "E / A · WORK THE WRECK-LINE WINCH")]
        [TestCase(
            VehicleModule.SealedRangeTank,
            "E / A · SEAL SCOUT FOR THE DUST LINE")]
        public void WreckLineGateNamesTheExistingPhysicalVerb(
            VehicleModule module,
            string expectedVerb)
        {
            LastBearingState state = DriveUntil(
                CreateDrivingState(module),
                model => model.IsWreckLineModulePointAvailable);
            LastBearingRoadDeskProjection projection =
                LastBearingRoadDeskPresenter.Present(
                    LastBearingReadModel.FromState(state));

            Assert.That(projection.NextVerb, Is.EqualTo(expectedVerb));
            Assert.That(
                projection.Route,
                Does.StartWith(
                    module == VehicleModule.WinchAssembly
                        ? "WRECK LINE"
                        : "DUST LINE"));
        }

        [Test]
        public void HomeboundProjectionSummarizesTheLoadedScout()
        {
            LastBearingState state = DriveUntil(
                CreateDrivingState(VehicleModule.WinchAssembly),
                model => model.IsWreckLineModulePointAvailable);
            LastBearingReadModel gate =
                LastBearingReadModel.FromState(state);
            state = Apply(
                state,
                sequence => new OperateWreckLineModuleCommand(
                    sequence,
                    gate.RouteActionKind));
            LastBearingRoadDeskProjection recovered =
                LastBearingRoadDeskPresenter.Present(
                    LastBearingReadModel.FromState(state));
            Assert.That(recovered.Cargo, Does.Contain("PUMP ROTOR"));
            Assert.That(
                recovered.CargoMassKilograms,
                Is.EqualTo(
                    LastBearingModeCoordinator
                        .PumpRotorPresentationMassKilograms));

            state = DriveUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable);
            Assert.That(
                LastBearingRoadDeskPresenter.Present(
                    LastBearingReadModel.FromState(state)).NextVerb,
                Is.EqualTo("E / A · SEAT THE DEPOT BRIDLE"));
            state = Apply(
                state,
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            state = Apply(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            state = Apply(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    Fingerprint));

            LastBearingRoadDeskProjection returning =
                LastBearingRoadDeskPresenter.Present(
                    LastBearingReadModel.FromState(state));
            Assert.That(returning.Leg, Is.EqualTo("HOMEBOUND"));
            Assert.That(returning.Cargo, Does.Contain("PUMP ROTOR"));
            Assert.That(returning.Cargo, Does.Contain("CERAMIC BEARING"));
            Assert.That(
                returning.CargoMassKilograms,
                Is.EqualTo(
                    LastBearingModeCoordinator
                        .PumpRotorPresentationMassKilograms +
                    LastBearingModeCoordinator
                        .CeramicBearingPresentationMassKilograms));
            Assert.That(
                returning.NextVerb,
                Is.EqualTo("KEEP SCOUT POINTED HOME"));
        }

        [Test]
        public void NonDrivingPhaseFailsClosed()
        {
            LastBearingReadModel city = LastBearingReadModel.FromState(
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    4701));

            Assert.That(
                () => LastBearingRoadDeskPresenter.Present(city),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "LAST_BEARING_ROAD_DESK_REQUIRES_DRIVING_PHASE"));
        }

        private static LastBearingState CreateDrivingState(
            VehicleModule module)
        {
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    4701);
            state = Apply(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));
            state = Apply(
                state,
                sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
            state = Apply(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    module));
            state = Apply(
                state,
                sequence =>
                    new InstallVehicleModuleCommand(sequence, module));
            for (var guard = 0;
                 LastBearingReadModel.FromState(state).PreparationPhase !=
                     PreparationPhase.Ready &&
                 guard < 1000;
                 guard++)
            {
                state = Advance(state);
            }

            Assert.That(
                LastBearingReadModel.FromState(state).PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));
            state = Apply(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    Fingerprint));
            state = Apply(
                state,
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    Fingerprint));
            Assert.That(
                LastBearingReadModel.FromState(state).ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            return state;
        }

        private static LastBearingState DriveUntil(
            LastBearingState state,
            Func<LastBearingReadModel, bool> predicate)
        {
            for (var guard = 0; guard < 1000; guard++)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (predicate(model))
                {
                    return state;
                }

                state = Apply(
                    state,
                    sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
            }

            Assert.Fail("Road predicate was not reached.");
            return state;
        }

        private static LastBearingState Apply(
            LastBearingState state,
            Func<long, LastBearingCommand> createCommand)
        {
            LastBearingCommand command =
                createCommand(state.NextCommandSequence);
            return new LastBearingKernel()
                .Step(state, new[] { command })
                .State;
        }

        private static LastBearingState Advance(LastBearingState state)
        {
            return new LastBearingKernel()
                .Step(state, Array.Empty<LastBearingCommand>())
                .State;
        }
    }
}
