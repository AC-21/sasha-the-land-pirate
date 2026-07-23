#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class CityImprovementTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run(
                "auxiliary pump installation conserves cargo and resources",
                InstallConservesCargoAndResources);
            harness.Run(
                "auxiliary pump installation rejects invalid intents atomically",
                InvalidInstallationIntentsFailAtomically);
            harness.Run(
                "unsupported city decisions remain pending",
                UnsupportedDecisionRemainsPending);
            harness.Run(
                "city improvement survives v6 canonical round trip",
                ImprovementRoundTripsInV5);
            harness.Run(
                "forged city improvement states fail invariants",
                ForgedImprovementStatesFailInvariants);
            harness.Run(
                "prior canonical payloads refuse reinterpretation",
                PriorPayloadsRefuseReinterpretation);
            harness.Run(
                "all colony compositions install the same improvement",
                CompositionsShareExactInstallationMechanics);
        }

        private static void InstallConservesCargoAndResources()
        {
            CoreTestDriver driver = ReachInstallationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2201);
            long partsBefore = driver.State.PartsUnits;
            long trendBefore = driver.View.WaterTrendMilliPerSettlementTick;
            LastBearingTickResult result = Install(driver);

            TestHarness.Equal(
                CityImprovementKind.RefurbishedAuxiliaryPump,
                driver.State.InstalledCityImprovement,
                "installed improvement");
            TestHarness.Equal(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                driver.State.HeavyCargoCustody,
                "installed rotor custody");
            TestHarness.Equal(0, driver.State.TowSlotsUsed, "released tow slot");
            TestHarness.Equal(
                partsBefore - LastBearingBalanceV1.AuxiliaryPumpInstallationPartsUnits,
                driver.State.PartsUnits,
                "installation parts debit");
            TestHarness.True(
                driver.State.PartsUnits
                    >= LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                "post-install reserve");
            TestHarness.Equal(
                trendBefore
                    + LastBearingBalanceV1
                        .AuxiliaryPumpWaterModifierMilliPerSettlementTick,
                driver.View.WaterTrendMilliPerSettlementTick,
                "water trend benefit");
            TestHarness.Equal(
                NextCityDecision.None,
                driver.State.NextCityDecision,
                "consumed city decision");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityResourcesCommitted
                    && item.BeforeValue == partsBefore
                    && item.AfterValue == driver.State.PartsUnits),
                "resource commit event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.HeavyCargoTransferred
                    && item.BeforeValue == (long)HeavyCargoCustody.Settlement
                    && item.AfterValue
                        == (long)HeavyCargoCustody.InstalledAtAuxiliaryPump),
                "rotor install transfer event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityImprovementInstalled),
                "city improvement event");
            TestHarness.Equal(
                LastBearingEventKind.CityResourcesCommitted,
                result.DomainEvents[0].Kind,
                "installation event order 0");
            TestHarness.Equal(
                LastBearingEventKind.HeavyCargoTransferred,
                result.DomainEvents[1].Kind,
                "installation event order 1");
            TestHarness.Equal(
                LastBearingEventKind.CityImprovementInstalled,
                result.DomainEvents[2].Kind,
                "installation event order 2");

            long partsAfter = driver.State.PartsUnits;
            LastBearingTickResult replay = Install(driver);
            TestHarness.Equal(partsAfter, driver.State.PartsUnits, "replay debit");
            TestHarness.Equal(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                driver.State.HeavyCargoCustody,
                "replay custody");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "replay audit event");
            TestHarness.True(
                replay.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.CityResourcesCommitted
                    && item.Kind != LastBearingEventKind.HeavyCargoTransferred
                    && item.Kind != LastBearingEventKind.CityImprovementInstalled),
                "replay repeated installation effects");
            AssertRejectedWithoutMutation(
                driver.State,
                new InstallCityImprovementCommand(
                    driver.State.NextCommandSequence,
                    NextCityDecision.RefurbishAuxiliaryPump,
                    "city:last-bearing:socket:not-the-installed-pump",
                    LastBearingState.AuxiliaryPumpOrientationQuarterTurns),
                "LAST_BEARING_CITY_IMPROVEMENT_ALREADY_INSTALLED",
                "mismatched installed replay");
        }

        private static void InvalidInstallationIntentsFailAtomically()
        {
            CoreTestDriver ready = ReachInstallationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2202);
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.AuxiliaryPumpSocketId,
                    LastBearingState.AuxiliaryPumpOrientationQuarterTurns),
                "LAST_BEARING_CITY_IMPROVEMENT_DECISION_MISMATCH",
                "wrong decision");
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.RefurbishAuxiliaryPump,
                    "city:last-bearing:socket:forged",
                    LastBearingState.AuxiliaryPumpOrientationQuarterTurns),
                "LAST_BEARING_CITY_IMPROVEMENT_SOCKET_INVALID",
                "wrong socket");
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.RefurbishAuxiliaryPump,
                    LastBearingState.AuxiliaryPumpSocketId,
                    1),
                "LAST_BEARING_CITY_IMPROVEMENT_ORIENTATION_INVALID",
                "wrong orientation");

            var insufficientBuilder = new LastBearingStateBuilder(ready.State)
            {
                PartsUnits =
                    LastBearingBalanceV1.MinimumPostReturnPartsUnits
                    + LastBearingBalanceV1.AuxiliaryPumpInstallationPartsUnits
                    - 1,
            };
            LastBearingState insufficient = insufficientBuilder.Build();
            AssertRejectedWithoutMutation(
                insufficient,
                ExactCommand(insufficient.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_PARTS_INSUFFICIENT",
                "insufficient reserve");

            LastBearingState early = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2203);
            var earlyBuilder = new LastBearingStateBuilder(early)
            {
                NextCityDecision = NextCityDecision.RefurbishAuxiliaryPump,
            };
            early = earlyBuilder.Build();
            AssertRejectedWithoutMutation(
                early,
                ExactCommand(early.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_PHASE_INVALID",
                "early installation");

            CoreTestDriver wrongCustody = ReachRepairedHome(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                VehicleModule.SealedRangeTank,
                2207);
            var wrongCustodyBuilder = new LastBearingStateBuilder(
                wrongCustody.State)
            {
                NextCityDecision = NextCityDecision.RefurbishAuxiliaryPump,
            };
            LastBearingState wrongCustodyState = wrongCustodyBuilder.Build();
            AssertRejectedWithoutMutation(
                wrongCustodyState,
                ExactCommand(wrongCustodyState.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_CARGO_INVALID",
                "wrong rotor custody");
        }

        private static void UnsupportedDecisionRemainsPending()
        {
            CoreTestDriver ready = ReachInstallationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2204);
            foreach (NextCityDecision decision in new[]
            {
                NextCityDecision.ExpandEmergencyCistern,
                NextCityDecision.MachineSpareBearing,
                NextCityDecision.RestoreDepotAccess,
            })
            {
                var builder = new LastBearingStateBuilder(ready.State)
                {
                    NextCityDecision = decision,
                };
                LastBearingState unsupported = builder.Build();
                AssertRejectedWithoutMutation(
                    unsupported,
                    new InstallCityImprovementCommand(
                        unsupported.NextCommandSequence,
                        decision,
                        LastBearingState.AuxiliaryPumpSocketId,
                        LastBearingState.AuxiliaryPumpOrientationQuarterTurns),
                    "LAST_BEARING_CITY_IMPROVEMENT_UNSUPPORTED",
                    "unsupported city decision " + decision);
                TestHarness.Equal(
                    "await-next-city-decision-authority",
                    LastBearingReadModel.FromState(unsupported).NextObjective,
                    "unsupported decision objective " + decision);
            }
        }

        private static void ForgedImprovementStatesFailInvariants()
        {
            CoreTestDriver installed = ReachInstallationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2208);
            Install(installed);

            AssertInvariantRejected(
                new LastBearingStateBuilder(installed.State)
                {
                    InstalledCityImprovement = (CityImprovementKind)99,
                }.BuildUnchecked(),
                "LAST_BEARING_INSTALLED_CITY_IMPROVEMENT_INVALID",
                "invalid improvement enum");
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed.State)
                {
                    InstalledCityImprovement = CityImprovementKind.None,
                }.BuildUnchecked(),
                "LAST_BEARING_WINCH_ACTION_ROTOR_CUSTODY_INVALID",
                "installed custody without improvement");
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed.State)
                {
                    TowSlotsUsed = 1,
                }.BuildUnchecked(),
                "LAST_BEARING_INSTALLED_ROTOR_STATE_INVALID",
                "installed improvement retained tow occupancy");
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed.State)
                {
                    ExpeditionPhase = ExpeditionPhase.Returned,
                    TransactionPhase = TransactionPhase.CityCredited,
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_IMPROVEMENT_STATE_INVALID",
                "installed improvement outside home phase");
        }

        private static void ImprovementRoundTripsInV5()
        {
            CoreTestDriver driver = ReachInstallationReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                2205);
            Install(driver);
            byte[] encoded = LastBearingCanonicalCodec.Encode(driver.State);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "v6 improvement decode");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                "v6 improvement canonical bytes");
            TestHarness.Equal(
                CityImprovementKind.RefurbishedAuxiliaryPump,
                decoded.State!.InstalledCityImprovement,
                "v6 improvement field");
            TestHarness.Equal(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                decoded.State.HeavyCargoCustody,
                "v6 installed custody");
        }

        private static void PriorPayloadsRefuseReinterpretation()
        {
            foreach (byte priorVersion in new byte[] { 1, 2 })
            {
                byte[] encoded = LastBearingCanonicalCodec.Encode(
                    LastBearingScenarioFactory.CreateInitial(
                        ColonyComposition.HumanOnly,
                        2206));
                encoded[8] = priorVersion;
                encoded[9] = 0;
                LastBearingDecodeResult decoded =
                    LastBearingCanonicalCodec.TryDecode(encoded);
                TestHarness.True(
                    !decoded.Succeeded,
                    "v" + priorVersion + " payload was reinterpreted");
                TestHarness.Equal(
                    LastBearingCanonicalCodec.DecodeUnknownVersionCode,
                    decoded.Code,
                    "v" + priorVersion + " refusal code");
                TestHarness.True(
                    decoded.State == null,
                    "v" + priorVersion + " refusal returned state");
            }
        }

        private static void CompositionsShareExactInstallationMechanics()
        {
            CoreTestDriver human = Installed(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId);
            CoreTestDriver robot = Installed(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId);
            CoreTestDriver mixed = Installed(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId);
            string expected = LastBearingCanonicalCodec.ComputeMechanicalSha256(
                human.State);
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(robot.State),
                "robot installation mechanics");
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(mixed.State),
                "mixed installation mechanics");
        }

        private static CoreTestDriver Installed(
            ColonyComposition composition,
            string residentId)
        {
            CoreTestDriver driver = ReachInstallationReady(
                composition,
                residentId,
                2210);
            Install(driver);
            return driver;
        }

        internal static LastBearingState CreateInstalledStateForSaveTests()
        {
            return Installed(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId).State;
        }

        private static CoreTestDriver ReachInstallationReady(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            CoreTestDriver driver = ReachRepairedHome(
                composition,
                residentId,
                VehicleModule.WinchAssembly,
                worldSeed);
            TestHarness.True(
                driver.View.IsCityImprovementInstallationAvailable,
                "installation was not available");
            return driver;
        }

        private static CoreTestDriver ReachRepairedHome(
            ColonyComposition composition,
            string residentId,
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                residentId,
                PreparationChoice.WorkshopPush,
                module);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:city-improvement:" + worldSeed;
            string fingerprint = "fp:city-improvement:" + worldSeed;
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence => new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.TakeBearing));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                driver.Apply(sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Water));
            }
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            while (driver.View.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new FinalizeExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new InstallTurbineRepairCommand(sequence));
            return driver;
        }

        private static LastBearingTickResult Install(CoreTestDriver driver)
        {
            return driver.Apply(sequence => ExactCommand(sequence));
        }

        private static InstallCityImprovementCommand ExactCommand(long sequence)
        {
            return new InstallCityImprovementCommand(
                sequence,
                NextCityDecision.RefurbishAuxiliaryPump,
                LastBearingState.AuxiliaryPumpSocketId,
                LastBearingState.AuxiliaryPumpOrientationQuarterTurns);
        }

        private static void AssertRejectedWithoutMutation(
            LastBearingState state,
            LastBearingCommand command,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new[] { command }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(LastBearingCanonicalCodec.Encode(state)),
                label + " mutated state");
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
    }

    internal static class CityImprovementTestBuilderExtensions
    {
        internal static LastBearingState BuildUnchecked(
            this LastBearingStateBuilder builder)
        {
            return new LastBearingState(builder);
        }
    }
}
