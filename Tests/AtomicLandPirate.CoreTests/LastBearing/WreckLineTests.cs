#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class WreckLineTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run(
                "both modules stop at the computed Wreck Line gate",
                BothModulesStopAtComputedGate);
            harness.Run(
                "Wreck Line rejects early wrong-module and bypass commands",
                GateRejectsInvalidCommandsAtomically);
            harness.Run(
                "Wreck Line verbs are distinct and one-shot",
                ModuleVerbsAreDistinctAndOneShot);
            harness.Run(
                "winched rotor custody survives load and later freeze",
                RotorCustodySurvivesLoadAndFreeze);
            harness.Run(
                "forged Wreck Line action and rotor custody states fail closed",
                ForgedActionAndCustodyStatesFailClosed);
        }

        private static void BothModulesStopAtComputedGate()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                CoreTestDriver driver = ReachGate(module, 2041);
                long expected = LastBearingBalanceV1.WreckLineGateTicks(module);
                TestHarness.Equal(expected, driver.State.RouteProgressTicks, module + " gate ticks");
                TestHarness.Equal(expected, driver.View.WreckLineGateTicks, module + " read gate ticks");
                TestHarness.Equal(
                    driver.State.RouteTargetTicks / 2,
                    expected,
                    module + " gate is not computed from route length");
                TestHarness.Equal(
                    ExpeditionPhase.Outbound,
                    driver.State.ExpeditionPhase,
                    module + " gate phase");
                TestHarness.True(
                    driver.View.IsWreckLineModulePointAvailable,
                    module + " gate unavailable");
                TestHarness.True(
                    !driver.State.RouteActionUsed,
                    module + " action was silently operated");
                TestHarness.True(
                    !driver.State.HasArrivalClaimSnapshot,
                    module + " gate incorrectly authored depot arrival");
            }
        }

        private static void GateRejectsInvalidCommandsAtomically()
        {
            CoreTestDriver early = ReadyForRoad(VehicleModule.WinchAssembly, 2042);
            AssertRejectedWithoutMutation(
                early.State,
                new OperateWreckLineModuleCommand(
                    early.State.NextCommandSequence,
                    RouteActionKind.DeployWinch),
                "LAST_BEARING_WRECK_LINE_MODULE_POINT_NOT_READY",
                "early Wreck Line operation");

            CoreTestDriver gate = ReachGate(VehicleModule.WinchAssembly, 2043);
            AssertRejectedWithoutMutation(
                gate.State,
                new OperateWreckLineModuleCommand(
                    gate.State.NextCommandSequence,
                    RouteActionKind.CrossExposedDustRoute),
                "LAST_BEARING_WRECK_LINE_MODULE_ACTION_MISMATCH",
                "wrong module verb");
            AssertRejectedWithoutMutation(
                gate.State,
                new DriveVehicleCommand(
                    gate.State.NextCommandSequence,
                    1000,
                    0),
                "LAST_BEARING_WRECK_LINE_MODULE_ACTION_REQUIRED",
                "Wreck Line drive bypass");
        }

        private static void ModuleVerbsAreDistinctAndOneShot()
        {
            CoreTestDriver winch = ReachGate(VehicleModule.WinchAssembly, 2044);
            LastBearingTickResult winchResult = winch.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.DeployWinch));
            TestHarness.Equal(HeavyCargoKind.PumpRotor, winch.State.HeavyCargoKind, "winch rotor kind");
            TestHarness.Equal(
                HeavyCargoCustody.Vehicle,
                winch.State.HeavyCargoCustody,
                "winch rotor custody");
            TestHarness.Equal(1, winch.State.TowSlotsUsed, "winch tow slot");
            TestHarness.True(winch.State.RouteActionUsed, "winch action not durable");
            TestHarness.True(
                winchResult.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.HeavyCargoTransferred &&
                    item.BeforeValue == (long)HeavyCargoCustody.Depot &&
                    item.AfterValue == (long)HeavyCargoCustody.Vehicle),
                "winch transfer event missing");

            LastBearingTickResult replay = winch.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.DeployWinch));
            TestHarness.Equal(1, winch.State.TowSlotsUsed, "duplicate winch tow slot");
            TestHarness.Equal(
                HeavyCargoCustody.Vehicle,
                winch.State.HeavyCargoCustody,
                "duplicate winch custody");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "duplicate winch was not replay-safe");
            TestHarness.True(
                replay.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.HeavyCargoTransferred),
                "duplicate winch emitted a second transfer");

            CoreTestDriver tank = ReachGate(VehicleModule.SealedRangeTank, 2044);
            LastBearingTickResult tankResult = tank.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.CrossExposedDustRoute));
            TestHarness.Equal(
                HeavyCargoCustody.Depot,
                tank.State.HeavyCargoCustody,
                "tank moved heavy cargo");
            TestHarness.Equal(0, tank.State.TowSlotsUsed, "tank used tow slot");
            TestHarness.True(tank.State.RouteActionUsed, "dust crossing not durable");
            TestHarness.True(
                tankResult.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.RouteActionUsed &&
                    item.AfterValue ==
                        (long)RouteActionKind.CrossExposedDustRoute),
                "dust crossing event missing");
        }

        private static void RotorCustodySurvivesLoadAndFreeze()
        {
            CoreTestDriver driver = ReachGate(VehicleModule.WinchAssembly, 2045);
            driver.OperateWreckLineIfAvailable();
            byte[] encoded = LastBearingCanonicalCodec.Encode(driver.State);
            LastBearingDecodeResult decoded = LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(decoded.Succeeded && decoded.State != null, "Wreck Line load failed");
            TestHarness.True(
                encoded.SequenceEqual(LastBearingCanonicalCodec.Encode(decoded.State!)),
                "Wreck Line load changed canonical bytes");
            TestHarness.Equal(
                HeavyCargoCustody.Vehicle,
                decoded.State!.HeavyCargoCustody,
                "loaded rotor custody");
            TestHarness.True(decoded.State.RouteActionUsed, "loaded route action");

            driver = new CoreTestDriver(decoded.State);
            DriveToDepotRecovery(driver);
            driver.Apply(sequence => new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.Cooperate));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                driver.State.TransactionId!,
                driver.State.TransactionFingerprint!));
            TestHarness.Equal(
                HeavyCargoCustody.Vehicle,
                driver.State.HeavyCargoCustody,
                "freeze moved or duplicated the rotor");
            TestHarness.Equal(1, driver.State.TowSlotsUsed, "freeze duplicated tow use");

            LastBearingTickResult postOperationReplay = driver.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.DeployWinch));
            TestHarness.True(
                postOperationReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "post-operation replay was rejected");
        }

        private static void ForgedActionAndCustodyStatesFailClosed()
        {
            CoreTestDriver earlyTank = ReadyForRoad(
                VehicleModule.SealedRangeTank,
                2046);
            var earlyActionBuilder = new LastBearingStateBuilder(
                earlyTank.State)
            {
                RouteActionUsed = true,
            };
            LastBearingState earlyAction = new LastBearingState(
                earlyActionBuilder);
            AssertInvariantRejected(
                earlyAction,
                "LAST_BEARING_WRECK_LINE_ACTION_BEFORE_GATE",
                "forged early route action");
            LastBearingDecodeResult earlyDecode =
                LastBearingCanonicalCodec.TryDecode(
                    EncodeUnchecked(earlyAction));
            TestHarness.True(!earlyDecode.Succeeded, "forged early action decoded");
            TestHarness.Equal(
                LastBearingCanonicalCodec.DecodeInvalidCode,
                earlyDecode.Code,
                "forged early action decode code");
            TestHarness.True(
                earlyDecode.State == null,
                "forged early action returned state");

            CoreTestDriver winchGate = ReachGate(
                VehicleModule.WinchAssembly,
                2047);
            var missingRotorBuilder = new LastBearingStateBuilder(
                winchGate.State)
            {
                RouteActionUsed = true,
            };
            AssertInvariantRejected(
                new LastBearingState(missingRotorBuilder),
                "LAST_BEARING_WINCH_ACTION_ROTOR_CUSTODY_INVALID",
                "forged winch action without rotor transfer");

            winchGate.OperateWreckLineIfAvailable();
            var earlySettlementBuilder = new LastBearingStateBuilder(
                winchGate.State)
            {
                HeavyCargoCustody = HeavyCargoCustody.Settlement,
            };
            AssertInvariantRejected(
                new LastBearingState(earlySettlementBuilder),
                "LAST_BEARING_ROTOR_SETTLEMENT_PHASE_INVALID",
                "forged early settlement custody");
        }

        private static CoreTestDriver ReachGate(
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReadyForRoad(module, worldSeed);
            var guard = 0;
            while (!driver.View.IsWreckLineModulePointAvailable && guard < 1000)
            {
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            TestHarness.True(
                driver.View.IsWreckLineModulePointAvailable,
                module + " did not reach Wreck Line");
            return driver;
        }

        private static CoreTestDriver ReadyForRoad(
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly, worldSeed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                module);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            const string transactionId = "tx:wreck-line";
            const string fingerprint = "fp:wreck-line";
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            return driver;
        }

        private static void DriveToDepotRecovery(CoreTestDriver driver)
        {
            var guard = 0;
            while (!driver.View.IsDepotApproachRecoveryAvailable && guard < 1000)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            TestHarness.True(
                driver.View.IsDepotApproachRecoveryAvailable,
                "depot recovery was not reached after Wreck Line");
        }

        private static void AssertRejectedWithoutMutation(
            LastBearingState state,
            LastBearingCommand command,
            string expectedCode,
            string label)
        {
            string before = LastBearingCanonicalCodec.ComputeSha256(state);
            InvalidOperationException error = TestHarness.Throws<InvalidOperationException>(
                () => new LastBearingKernel().Step(
                    state,
                    new[] { command }),
                label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.Equal(
                before,
                LastBearingCanonicalCodec.ComputeSha256(state),
                label + " mutated input");
        }

        private static void AssertInvariantRejected(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingInvariants.Validate(state),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
        }

        private static byte[] EncodeUnchecked(LastBearingState state)
        {
            MethodInfo? method = typeof(LastBearingCanonicalCodec).GetMethod(
                "EncodeInternal",
                BindingFlags.NonPublic | BindingFlags.Static);
            TestHarness.True(method != null, "unchecked codec helper missing");
            object? encoded = method!.Invoke(
                null,
                new object[] { state, true });
            TestHarness.True(encoded is byte[], "unchecked codec returned no bytes");
            return (byte[])encoded!;
        }
    }
}
