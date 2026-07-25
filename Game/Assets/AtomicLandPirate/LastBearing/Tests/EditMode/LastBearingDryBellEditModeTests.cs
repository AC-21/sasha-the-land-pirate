#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingDryBellEditModeTests
    {
        [TestCase(ColonyComposition.HumanOnly)]
        [TestCase(ColonyComposition.RobotOnly)]
        [TestCase(ColonyComposition.Mixed)]
        public void BoundaryIsExactForEveryComposition(
            ColonyComposition composition)
        {
            LastBearingState source =
                LastBearingScenarioFactory.CreateInitial(composition, 3310);

            LastBearingReadModel oneMilli =
                LastBearingReadModel.FromState(
                    WithState(
                        source,
                        validate: true,
                        ("WaterMilli", 1L)));
            Assert.That(oneMilli.IsSettlementLost, Is.False);
            Assert.That(oneMilli.SettlementLossReason, Is.Null);

            LastBearingReadModel dry =
                LastBearingReadModel.FromState(
                    WithState(
                        source,
                        validate: true,
                        ("WaterMilli", 0L)));
            Assert.That(dry.IsSettlementLost, Is.True);
            Assert.That(
                dry.SettlementLossReason,
                Is.EqualTo(
                    LastBearingReadModel.DryBellSettlementLossReason));

            LastBearingReadModel repairedDry =
                LastBearingReadModel.FromState(
                    WithState(
                        source,
                        validate: false,
                        ("WaterMilli", 0L),
                        (
                            "TurbineCondition",
                            TurbineCondition.BearingRepaired)));
            Assert.That(repairedDry.IsSettlementLost, Is.False);
            Assert.That(repairedDry.SettlementLossReason, Is.Null);
        }

        [Test]
        public void LostStateFreezesRejectsAndRederivesOnRoundTrip()
        {
            LastBearingState lost = WithState(
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    3311),
                validate: true,
                ("WaterMilli", 0L));
            byte[] before = LastBearingCanonicalCodec.Encode(lost);
            long sequence = lost.NextCommandSequence;
            var kernel = new LastBearingKernel();

            LastBearingTickResult frozen = kernel.Step(
                lost,
                Array.Empty<LastBearingCommand>());
            Assert.That(
                LastBearingCanonicalCodec.Encode(frozen.State),
                Is.EqualTo(before));
            Assert.That(frozen.State.NextCommandSequence, Is.EqualTo(sequence));
            Assert.That(frozen.DomainEvents, Is.Empty);

            InvalidOperationException error =
                Assert.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        lost,
                        new LastBearingCommand[]
                        {
                            new SetPauseCommand(sequence, true),
                        }))!;
            Assert.That(
                error.Message,
                Is.EqualTo("LAST_BEARING_SETTLEMENT_LOST"));
            Assert.That(lost.NextCommandSequence, Is.EqualTo(sequence));
            Assert.That(
                LastBearingCanonicalCodec.Encode(lost),
                Is.EqualTo(before));

            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(before);
            Assert.That(decoded.Succeeded, Is.True, decoded.Code);
            Assert.That(decoded.State, Is.Not.Null);
            Assert.That(
                LastBearingCanonicalCodec.Encode(decoded.State!),
                Is.EqualTo(before));
            Assert.That(
                LastBearingReadModel.FromState(decoded.State!)
                    .IsSettlementLost,
                Is.True);
        }

        [Test]
        public void SameTickWaterShiftCompletionRescuesTheSettlement()
        {
            LastBearingState active = StartWaterShift();
            LastBearingState rescueReady = WithState(
                active,
                validate: true,
                (
                    "WaterMilli",
                    -LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick),
                (
                    "HotShiftElapsedTicks",
                    LastBearingBalanceV1
                        .WaterShiftRequiredSettlementTicks - 1));

            LastBearingTickResult rescued =
                new LastBearingKernel().Step(
                    rescueReady,
                    Array.Empty<LastBearingCommand>());

            LastBearingDomainEvent dryTick =
                rescued.DomainEvents.Single(item =>
                    item.Kind ==
                        LastBearingEventKind.HomeWaterChanged);
            Assert.That(dryTick.AfterValue, Is.Zero);
            Assert.That(
                rescued.DomainEvents.Any(item =>
                    item.Kind ==
                        LastBearingEventKind.WaterShiftCompleted),
                Is.True);
            Assert.That(
                rescued.State.WaterMilli,
                Is.EqualTo(
                    LastBearingBalanceV1
                        .WaterShiftOutputWaterMilli));
            Assert.That(rescued.ReadModel.IsSettlementLost, Is.False);
            Assert.That(rescued.ReadModel.SettlementLossReason, Is.Null);
        }

        private static LastBearingState StartWaterShift()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    3312);
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new AssignResidentCommand(
                        state.NextCommandSequence,
                        ResidentRoster.HumanResidentId),
                }).State;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new ActivateSliceInfrastructureCommand(
                        state.NextCommandSequence),
                }).State;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new SelectPreparationCommand(
                        state.NextCommandSequence,
                        PreparationChoice.WorkshopPush,
                        VehicleModule.WinchAssembly),
                }).State;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new InstallVehicleModuleCommand(
                        state.NextCommandSequence,
                        VehicleModule.WinchAssembly),
                }).State;
            return kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new RunWaterShiftCommand(
                        state.NextCommandSequence,
                        state.WaterShiftCompletedCount),
                }).State;
        }

        private static LastBearingState WithState(
            LastBearingState source,
            bool validate,
            params (string Name, object Value)[] values)
        {
            Type? builderType =
                typeof(LastBearingState).Assembly.GetType(
                    "AtomicLandPirate.Simulation.LastBearing." +
                    "LastBearingStateBuilder");
            Assert.That(builderType, Is.Not.Null);
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public;
            ConstructorInfo? builderConstructor =
                builderType!.GetConstructor(
                    flags,
                    binder: null,
                    new[] { typeof(LastBearingState) },
                    modifiers: null);
            Assert.That(builderConstructor, Is.Not.Null);
            object builder =
                builderConstructor!.Invoke(new object[] { source });
            foreach ((string name, object value) in values)
            {
                FieldInfo? field = builderType.GetField(name, flags);
                Assert.That(field, Is.Not.Null, name);
                field!.SetValue(builder, value);
            }

            if (validate)
            {
                MethodInfo? build =
                    builderType.GetMethod("Build", flags);
                Assert.That(build, Is.Not.Null);
                return (LastBearingState)build!.Invoke(
                    builder,
                    null);
            }

            ConstructorInfo? stateConstructor =
                typeof(LastBearingState).GetConstructor(
                    flags,
                    binder: null,
                    new[] { builderType },
                    modifiers: null);
            Assert.That(stateConstructor, Is.Not.Null);
            return (LastBearingState)stateConstructor!.Invoke(
                new[] { builder });
        }
    }
}
