#nullable enable

using System;
using System.Globalization;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class RepeatWreckLineTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "repeat launch resets only current-trip witnesses",
                RepeatLaunchIsFreshAndBounded);
            harness.Run(
                "repeat circuit returns four parts at normal road cost",
                RepeatCircuitCostsAndSalvageAreExact);
            harness.Run(
                "repeat circuit charges and preserves the authored route toll",
                PersistentRouteTollIsApplied);
            harness.Run(
                "repeat circuit works for both modules and all compositions",
                BothModulesAndAllCompositionsWork);
            harness.Run(
                "repeat launch waits for service and urgent return work",
                ServiceAndUrgentWorkGateLaunch);
            harness.Run(
                "repeat launch rejects early duplicate and stale intents atomically",
                EarlyDuplicateAndStaleIntentsFailAtomically);
            harness.Run(
                "repaired turbine and forged repeat identity cannot weaken invariants",
                ForgedRepeatLineageFailsClosed);
            harness.Run(
                "repeat circuit preserves first-run history",
                FirstRunHistoryIsPreserved);
            harness.Run(
                "repeat circuit preserves an installed auxiliary pump",
                InstalledAuxiliaryPumpHistoryIsPreserved);
            harness.Run(
                "repeat lineage permits bounded authored road-edge damage",
                OffRoadRepeatConditionRemainsBounded);
            harness.Run(
                "repeat states round trip throughout schema 9",
                RepeatStatesRoundTrip);
            harness.Run(
                "two serviced repeat circuits cannot duplicate salvage",
                TwoConsecutiveCircuitsAreBounded);
        }

        private static void RepeatLaunchIsFreshAndBounded()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3401);
            LastBearingState before = driver.State;
            string previousTransactionId = before.TransactionId!;
            string previousFingerprint = before.TransactionFingerprint!;
            string expectedSuffix = before.NextCommandSequence.ToString(
                CultureInfo.InvariantCulture);

            LastBearingTickResult result = PrepareRepeat(driver);

            TestHarness.Equal(
                TransactionPhase.Prepared,
                driver.State.TransactionPhase,
                "repeat transaction phase");
            TestHarness.Equal(
                ExpeditionPhase.AtHome,
                driver.State.ExpeditionPhase,
                "repeat preparation location");
            TestHarness.Equal(
                "tx:repeat:" + expectedSuffix,
                driver.State.TransactionId,
                "fresh transaction id");
            TestHarness.Equal(
                "fp:repeat:" + expectedSuffix,
                driver.State.TransactionFingerprint,
                "fresh transaction fingerprint");
            TestHarness.Equal(0L, driver.State.RouteProgressTicks, "route progress");
            TestHarness.True(!driver.State.RouteActionUsed, "route action reset");
            TestHarness.True(!driver.State.ReturnPayloadFrozen, "return freeze reset");
            TestHarness.True(
                !driver.State.HasArrivalClaimSnapshot,
                "arrival snapshot reset");
            TestHarness.Equal(
                FrameRailSalvageCustody.WreckLine,
                driver.State.FrameRailSalvageCustody,
                "repeat salvage source");
            TestHarness.Equal(
                0L,
                driver.State.OrdinaryCargoUsedUnits,
                "repeat ordinary cargo");
            TestHarness.Equal(0, driver.State.TowSlotsUsed, "repeat tow use");
            TestHarness.Equal(
                before.VehicleConditionMilli,
                driver.State.VehicleConditionMilli,
                "launch changed service condition");
            TestHarness.Equal(
                before.PartsUnits,
                driver.State.PartsUnits,
                "launch changed parts");
            TestHarness.Equal(
                before.FuelUnits,
                driver.State.FuelUnits,
                "launch debited fuel early");
            TestHarness.Equal(
                1,
                result.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind
                            .ExpeditionTransactionPrepared),
                "repeat prepare event count");
            TestHarness.Equal(
                LastBearingEventKind.ExpeditionTransactionPrepared,
                result.DomainEvents[0].Kind,
                "repeat prepare event");
            TestHarness.Equal(
                1,
                result.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind
                            .FrameRailSalvageTransferred),
                "repeat salvage event count");
            TestHarness.True(
                !string.Equals(
                    previousTransactionId,
                    driver.State.TransactionId,
                    StringComparison.Ordinal),
                "completed transaction id was reused");
            TestHarness.Equal(
                previousFingerprint,
                before.TransactionFingerprint,
                "completed fingerprint changed");
        }

        private static void RepeatCircuitCostsAndSalvageAreExact()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.HumanOnly,
                VehicleModule.WinchAssembly,
                3402);
            long partsBefore = driver.State.PartsUnits;
            long fuelBefore = driver.State.FuelUnits;
            long conditionBefore = driver.State.VehicleConditionMilli;
            HeavyCargoCustody rotorBefore = driver.State.HeavyCargoCustody;

            RunRepeatCircuit(driver);

            TestHarness.Equal(
                checked(
                    partsBefore
                    + LastBearingBalanceV1
                        .WreckLineFrameRailSalvagePartsUnits),
                driver.State.PartsUnits,
                "repeat parts credit");
            TestHarness.Equal(
                checked(
                    fuelBefore
                    - LastBearingBalanceV1.RouteFuelCost(
                        VehicleModule.WinchAssembly)),
                driver.State.FuelUnits,
                "repeat fuel cost");
            TestHarness.Equal(
                checked(
                    conditionBefore
                    - LastBearingBalanceV1.RouteConditionLoss(
                        VehicleModule.WinchAssembly,
                        driver.State.RigUpgrade)),
                driver.State.VehicleConditionMilli,
                "repeat condition cost");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                driver.State.FrameRailSalvageCustody,
                "repeat salvage credited");
            TestHarness.Equal(
                0L,
                driver.State.OrdinaryCargoUsedUnits,
                "repeat cargo released");
            TestHarness.Equal(
                rotorBefore,
                driver.State.HeavyCargoCustody,
                "repeat created a second rotor");
        }

        private static void BothModulesAndAllCompositionsWork()
        {
            var compositions = new[]
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };
            var modules = new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            };
            int seed = 3410;

            foreach (VehicleModule module in modules)
            {
                foreach (ColonyComposition composition in compositions)
                {
                    CoreTestDriver driver = ReachRepeatReady(
                        composition,
                        module,
                        seed);
                    long partsBefore = driver.State.PartsUnits;
                    long fuelBefore = driver.State.FuelUnits;
                    RunRepeatCircuit(driver);

                    TestHarness.Equal(
                        checked(
                            partsBefore
                            + LastBearingBalanceV1
                                .WreckLineFrameRailSalvagePartsUnits),
                        driver.State.PartsUnits,
                        composition + " " + module + " parts");
                    TestHarness.Equal(
                        checked(
                            fuelBefore
                            - LastBearingBalanceV1.RouteFuelCost(module)),
                        driver.State.FuelUnits,
                        composition + " " + module + " fuel");
                    TestHarness.Equal(
                        ExpeditionPhase.AtHome,
                        driver.State.ExpeditionPhase,
                        composition + " " + module + " home");
                    TestHarness.Equal(
                        TransactionPhase.Finalized,
                        driver.State.TransactionPhase,
                        composition + " " + module + " finalized");
                    seed++;
                }
            }
        }

        private static void PersistentRouteTollIsApplied()
        {
            CoreTestDriver driver = ReachAdverseRepeatReady(3419);
            long fuelBefore = driver.State.FuelUnits;
            long tollBefore = driver.State.FutureRouteTollFuelUnits;
            TestHarness.Equal(
                LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                tollBefore,
                "adverse future toll fixture");
            TestHarness.True(
                driver.State.RoutePermitGranted,
                "adverse route permit fixture");

            RunRepeatCircuit(driver);

            TestHarness.Equal(
                checked(
                    fuelBefore
                    - LastBearingBalanceV1.RouteFuelCost(
                        VehicleModule.WinchAssembly)
                    - tollBefore),
                driver.State.FuelUnits,
                "repeat route plus toll fuel");
            TestHarness.Equal(
                tollBefore,
                driver.State.FutureRouteTollFuelUnits,
                "repeat consumed persistent toll history");
            TestHarness.True(
                driver.State.RoutePermitGranted,
                "repeat cleared route permit history");
        }

        private static void ServiceAndUrgentWorkGateLaunch()
        {
            CoreTestDriver unserviced = ReachUrgentWorkResolved(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3420);
            TestHarness.True(
                unserviced.View.IsVehicleServiceAvailable,
                "unserviced fixture");
            AssertRepeatRejected(
                unserviced.State,
                unserviced.State.TransactionId!,
                unserviced.State.TransactionFingerprint!,
                "LAST_BEARING_REPEAT_EXPEDITION_NOT_READY",
                "unserviced scout");

            CoreTestDriver queuedAid = ReachRepairedReturn(
                ColonyComposition.Mixed,
                VehicleModule.SealedRangeTank,
                3421);
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterQueued,
                queuedAid.State.FactionAidPolicy,
                "queued aid fixture");
            AssertRepeatRejected(
                queuedAid.State,
                queuedAid.State.TransactionId!,
                queuedAid.State.TransactionFingerprint!,
                "LAST_BEARING_REPEAT_EXPEDITION_NOT_READY",
                "queued emergency aid");

            CoreTestDriver insufficientFuel = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3422);
            LastBearingState noFuel = new LastBearingStateBuilder(
                insufficientFuel.State)
            {
                FuelUnits = checked(
                    LastBearingBalanceV1.RouteFuelCost(
                        VehicleModule.WinchAssembly) - 1),
            }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(noFuel)
                    .IsRepeatExpeditionAvailable,
                "insufficient fuel exposed repeat");
            AssertRepeatRejected(
                noFuel,
                noFuel.TransactionId!,
                noFuel.TransactionFingerprint!,
                "LAST_BEARING_REPEAT_EXPEDITION_NOT_READY",
                "insufficient route fuel");
        }

        private static void EarlyDuplicateAndStaleIntentsFailAtomically()
        {
            CoreTestDriver ready = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3430);
            string firstId = ready.State.TransactionId!;
            string firstFingerprint = ready.State.TransactionFingerprint!;
            PrepareRepeat(ready);

            AssertRepeatRejected(
                ready.State,
                firstId,
                firstFingerprint,
                "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                "duplicate repeat prepare");
            AssertRepeatRejected(
                ready.State,
                ready.State.TransactionId!,
                ready.State.TransactionFingerprint!,
                "LAST_BEARING_REPEAT_EXPEDITION_NOT_READY",
                "early active transaction");

            CoreTestDriver staleSequence = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3431);
            AssertRepeatRejected(
                staleSequence.State,
                staleSequence.State.TransactionId!,
                staleSequence.State.TransactionFingerprint!,
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "stale command sequence",
                checked(staleSequence.State.NextCommandSequence + 1));

            CoreTestDriver completed = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3432);
            string staleId = completed.State.TransactionId!;
            string staleFingerprint = completed.State.TransactionFingerprint!;
            RunRepeatCircuit(completed);
            completed.Apply(sequence => new ServiceScoutCommand(sequence));
            AssertRepeatRejected(
                completed.State,
                staleId,
                staleFingerprint,
                "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                "stale completed predecessor");
        }

        private static void FirstRunHistoryIsPreserved()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.RobotOnly,
                VehicleModule.SealedRangeTank,
                3440);
            LastBearingState before = driver.State;
            RunRepeatCircuit(driver);
            LastBearingState after = driver.State;

            TestHarness.Equal(
                before.DepotResolution,
                after.DepotResolution,
                "depot resolution");
            TestHarness.Equal(
                before.TurbineCondition,
                after.TurbineCondition,
                "turbine repair");
            TestHarness.Equal(
                before.RepairCargoKind,
                after.RepairCargoKind,
                "repair kind");
            TestHarness.Equal(
                before.RepairCargoCustody,
                after.RepairCargoCustody,
                "repair custody");
            TestHarness.Equal(
                before.FactionMemory,
                after.FactionMemory,
                "faction memory");
            TestHarness.Equal(before.FactionTrust, after.FactionTrust, "trust");
            TestHarness.Equal(
                before.FactionGrievance,
                after.FactionGrievance,
                "grievance");
            TestHarness.Equal(
                before.FactionAccessPolicy,
                after.FactionAccessPolicy,
                "access policy");
            TestHarness.Equal(
                before.FactionAidPolicy,
                after.FactionAidPolicy,
                "aid policy");
            TestHarness.Equal(
                before.EmergencyAidWaterMilli,
                after.EmergencyAidWaterMilli,
                "aid manifest");
            TestHarness.Equal(
                before.MaintenanceRecipe,
                after.MaintenanceRecipe,
                "maintenance recipe");
            TestHarness.Equal(
                before.MaintenanceObligationActive,
                after.MaintenanceObligationActive,
                "maintenance obligation");
            TestHarness.Equal(
                before.MaintenancePartsUnits,
                after.MaintenancePartsUnits,
                "maintenance parts");
            TestHarness.Equal(
                before.RoutePermitGranted,
                after.RoutePermitGranted,
                "route permit");
            TestHarness.Equal(
                before.InstalledCityImprovement,
                after.InstalledCityImprovement,
                "city improvement");
            TestHarness.Equal(
                before.HeavyCargoCustody,
                after.HeavyCargoCustody,
                "rotor history");
            TestHarness.Equal(
                before.LiquidCargoKind,
                after.LiquidCargoKind,
                "liquid history");
            TestHarness.Equal(
                before.LiquidCargoQuantityMilli,
                after.LiquidCargoQuantityMilli,
                "liquid quantity");
            TestHarness.Equal(
                before.LiquidCargoCustody,
                after.LiquidCargoCustody,
                "liquid custody");
        }

        private static void InstalledAuxiliaryPumpHistoryIsPreserved()
        {
            CoreTestDriver driver = ReachRepairedReturn(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3441,
                preparation: PreparationChoice.WorkshopPush);
            if (driver.State.FactionAidPolicy
                == FactionAidPolicy.EmergencyWaterQueued)
            {
                driver.Apply(sequence =>
                    new ReceiveEmergencyAidCommand(sequence));
            }

            TestHarness.Equal(
                NextCityDecision.RefurbishAuxiliaryPump,
                driver.State.NextCityDecision,
                "installed-pump fixture decision");
            driver.Apply(sequence =>
                new InstallCityImprovementCommand(
                    sequence,
                    NextCityDecision.RefurbishAuxiliaryPump,
                    LastBearingState.AuxiliaryPumpSocketId,
                    LastBearingState
                        .AuxiliaryPumpOrientationQuarterTurns));
            driver.Apply(sequence => new ServiceScoutCommand(sequence));

            RunRepeatCircuit(driver);

            TestHarness.Equal(
                CityImprovementKind.RefurbishedAuxiliaryPump,
                driver.State.InstalledCityImprovement,
                "repeat cleared auxiliary pump");
            TestHarness.Equal(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                driver.State.HeavyCargoCustody,
                "repeat moved installed rotor");
            TestHarness.Equal(
                0,
                driver.State.TowSlotsUsed,
                "repeat occupied tow slot with installed rotor");
        }

        private static void OffRoadRepeatConditionRemainsBounded()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3442);
            long conditionBefore = driver.State.VehicleConditionMilli;

            RunRepeatCircuit(driver, driveOffRoad: true);

            TestHarness.Equal(
                checked(
                    conditionBefore
                    - LastBearingBalanceV1.RouteOneWayTicks(
                        VehicleModule.WinchAssembly)
                    - LastBearingBalanceV1.RouteConditionLoss(
                        VehicleModule.WinchAssembly,
                        driver.State.RigUpgrade)),
                driver.State.VehicleConditionMilli,
                "repeat road-edge plus route condition cost");
            TestHarness.True(
                LastBearingRepeatExpedition.IsLineage(driver.State),
                "bounded off-road repeat lost lineage");
        }

        private static void ForgedRepeatLineageFailsClosed()
        {
            CoreTestDriver ready = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3435);
            LastBearingState repairedOnly =
                ForgePreparedRepeatShape(
                    ready.State,
                    "tx:ordinary:forged",
                    "fp:ordinary:forged",
                    ready.State.FactionMemory!);
            TestHarness.True(
                !LastBearingRepeatExpedition.IsLineage(repairedOnly),
                "repaired turbine alone became repeat lineage");
            AssertInvariantRejected(
                repairedOnly,
                "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID",
                "repaired-only prepared state");

            FactionMemoryRecord memory = ready.State.FactionMemory!;
            var forgedMemory = new FactionMemoryRecord(
                "memory:last-bearing:cooperate:forged",
                memory.WitnessedAction,
                memory.AffectedFactionId,
                memory.Magnitude,
                memory.DoctrineTag,
                memory.EncounterTick,
                memory.ConsequenceCode);
            LastBearingState forgedHistory =
                ForgePreparedRepeatShape(
                    ready.State,
                    "tx:repeat:forged-history",
                    "fp:repeat:forged-history",
                    forgedMemory);
            TestHarness.True(
                !LastBearingRepeatExpedition.IsLineage(forgedHistory),
                "forged faction history became repeat lineage");
            AssertInvariantRejected(
                forgedHistory,
                "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID",
                "forged-history prepared state");

            CoreTestDriver departed = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3436);
            PrepareRepeat(departed);
            departed.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                departed.State.TransactionId!,
                departed.State.TransactionFingerprint!));
            LastBearingState forgedDeparture =
                new LastBearingStateBuilder(departed.State)
                {
                    VehicleConditionMilli = checked(
                        LastBearingBalanceV1
                            .StartingVehicleConditionMilli - 1),
                }.BuildUnchecked();
            TestHarness.True(
                !LastBearingRepeatExpedition.IsLineage(forgedDeparture),
                "unreachable post-service condition became repeat lineage");
            AssertInvariantRejected(
                forgedDeparture,
                "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID",
                "forged post-service departure");

            TestHarness.Throws<ArgumentException>(
                () => new PrepareExpeditionTransactionCommand(
                    0,
                    "tx:repeat:reserved",
                    "fp:repeat:reserved"),
                "ordinary prepare accepted reserved repeat identity");
            TestHarness.Throws<ArgumentException>(
                () => new PrepareRepeatExpeditionTransactionCommand(
                    0,
                    ready.State.TransactionId!,
                    ready.State.TransactionFingerprint!,
                    "tx:repeat:0",
                    "fp:repeat:1"),
                "repeat prepare accepted mismatched identity suffixes");
            TestHarness.Throws<ArgumentException>(
                () => new PrepareRepeatExpeditionTransactionCommand(
                    7,
                    ready.State.TransactionId!,
                    ready.State.TransactionFingerprint!,
                    "tx:repeat:8",
                    "fp:repeat:8"),
                "repeat prepare accepted a non-sequence identity");
        }

        private static void RepeatStatesRoundTrip()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3450);
            driver = new CoreTestDriver(RoundTrip(driver.State, "ready"));
            PrepareRepeat(driver);
            driver = new CoreTestDriver(RoundTrip(driver.State, "prepared"));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                driver.State.TransactionId!,
                driver.State.TransactionFingerprint!));
            driver = new CoreTestDriver(RoundTrip(driver.State, "outbound"));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "repeat depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver = new CoreTestDriver(RoundTrip(driver.State, "at depot"));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                driver.State.TransactionId!,
                driver.State.TransactionFingerprint!));
            driver = new CoreTestDriver(RoundTrip(driver.State, "returning"));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "repeat home return");
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                driver.State.TransactionId!,
                driver.State.TransactionFingerprint!));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    driver.State.TransactionId!,
                    driver.State.TransactionFingerprint!));
            LastBearingState finalized = RoundTrip(
                driver.State,
                "finalized");
            TestHarness.Equal(9, finalized.SchemaVersion, "repeat schema");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                finalized.FrameRailSalvageCustody,
                "restored repeat salvage");
        }

        private static void TwoConsecutiveCircuitsAreBounded()
        {
            CoreTestDriver driver = ReachRepeatReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3460);
            long startingParts = driver.State.PartsUnits;
            long startingFuel = driver.State.FuelUnits;
            HeavyCargoCustody rotor = driver.State.HeavyCargoCustody;

            RunRepeatCircuit(driver);
            string firstRepeatId = driver.State.TransactionId!;
            string firstRepeatFingerprint =
                driver.State.TransactionFingerprint!;
            TestHarness.True(
                driver.View.IsVehicleServiceAvailable,
                "first repeat did not require service");
            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            TestHarness.True(
                driver.View.IsRepeatExpeditionAvailable,
                "service did not reopen repeat");
            RunRepeatCircuit(driver);

            TestHarness.Equal(
                checked(
                    startingParts
                    + (2 * LastBearingBalanceV1
                        .WreckLineFrameRailSalvagePartsUnits)
                    - LastBearingBalanceV1.HotShiftOutputPartsUnits),
                driver.State.PartsUnits,
                "two-circuit parts conservation");
            TestHarness.Equal(
                checked(
                    startingFuel
                    - (2 * LastBearingBalanceV1.RouteFuelCost(
                        VehicleModule.WinchAssembly))),
                driver.State.FuelUnits,
                "two-circuit fuel conservation");
            TestHarness.Equal(
                rotor,
                driver.State.HeavyCargoCustody,
                "two circuits duplicated rotor");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                driver.State.FrameRailSalvageCustody,
                "second salvage not finalized");

            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            byte[] beforeReuse = LastBearingCanonicalCodec.Encode(driver.State);
            TestHarness.Throws<ArgumentException>(
                () => new PrepareRepeatExpeditionTransactionCommand(
                    driver.State.NextCommandSequence,
                    driver.State.TransactionId!,
                    driver.State.TransactionFingerprint!,
                    firstRepeatId,
                    firstRepeatFingerprint),
                "nonadjacent repeat identity reuse was accepted");
            TestHarness.True(
                beforeReuse.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "nonadjacent identity rejection mutated state");
        }

        private static CoreTestDriver ReachRepeatReady(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachUrgentWorkResolved(
                composition,
                module,
                worldSeed);
            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            TestHarness.True(
                driver.View.IsRepeatExpeditionAvailable,
                "repeat-ready fixture unavailable");
            TestHarness.Equal(
                "prepare-repeat-expedition",
                driver.View.NextObjective,
                "repeat-ready objective");
            return driver;
        }

        private static CoreTestDriver ReachUrgentWorkResolved(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachRepairedReturn(
                composition,
                module,
                worldSeed);
            if (driver.State.FactionAidPolicy
                == FactionAidPolicy.EmergencyWaterQueued)
            {
                driver.Apply(sequence =>
                    new ReceiveEmergencyAidCommand(sequence));
            }

            TestHarness.Equal(
                NextCityDecision.None,
                driver.State.NextCityDecision,
                "repeat fixture retained city work");
            TestHarness.True(
                driver.View.IsVehicleServiceAvailable,
                "repeat fixture service unavailable");
            return driver;
        }

        private static CoreTestDriver ReachAdverseRepeatReady(
            int worldSeed)
        {
            CoreTestDriver driver = ReachRepairedReturn(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                worldSeed,
                EncounterChoice.TakeBearing);
            TestHarness.Equal(
                NextCityDecision.MachineSpareBearing,
                driver.State.NextCityDecision,
                "adverse repeat decision");
            driver.Apply(sequence =>
                new StartSpareBearingBatchCommand(sequence));
            AdvanceUntil(
                driver,
                model => model.SpareBearingBatchPhase
                    == SpareBearingBatchPhase.Complete,
                drive: false,
                "adverse repeat batch");
            driver.Apply(sequence =>
                new BarterSpareBearingLotCommand(sequence));
            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            TestHarness.True(
                driver.View.IsRepeatExpeditionAvailable,
                "adverse repeat-ready fixture unavailable");
            return driver;
        }

        private static CoreTestDriver ReachRepairedReturn(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed,
            EncounterChoice outcome = EncounterChoice.Cooperate,
            PreparationChoice preparation = PreparationChoice.CivicBuffer)
        {
            string residentId =
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId;
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                residentId,
                preparation,
                module);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "first preparation");

            string transactionId = "tx:first:" + worldSeed;
            string fingerprint = "fp:first:" + worldSeed;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "first depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    outcome));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                driver.Apply(sequence =>
                    new ChooseLiquidReturnCommand(
                        sequence,
                        LiquidCargoKind.Fuel));
            }

            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "first home return");
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            return driver;
        }

        private static LastBearingTickResult PrepareRepeat(
            CoreTestDriver driver)
        {
            return driver.Apply(sequence =>
                new PrepareRepeatExpeditionTransactionCommand(
                    sequence,
                    driver.State.TransactionId!,
                    driver.State.TransactionFingerprint!,
                    "tx:repeat:"
                        + sequence.ToString(CultureInfo.InvariantCulture),
                    "fp:repeat:"
                        + sequence.ToString(CultureInfo.InvariantCulture)));
        }

        private static void RunRepeatCircuit(
            CoreTestDriver driver,
            bool driveOffRoad = false)
        {
            PrepareRepeat(driver);
            string transactionId = driver.State.TransactionId!;
            string fingerprint = driver.State.TransactionFingerprint!;
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            if (driveOffRoad)
            {
                for (var index = 0; index < 16; index++)
                {
                    driver.Apply(sequence =>
                        new DriveVehicleCommand(sequence, 0, 1000));
                }
            }
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "repeat depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "repeat home return");
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
        }

        private static LastBearingState RoundTrip(
            LastBearingState state,
            string label)
        {
            byte[] encoded = LastBearingCanonicalCodec.Encode(state);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                label + " decode");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                label + " canonical bytes");
            return decoded.State!;
        }

        private static LastBearingState ForgePreparedRepeatShape(
            LastBearingState source,
            string transactionId,
            string fingerprint,
            FactionMemoryRecord factionMemory)
        {
            return new LastBearingStateBuilder(source)
            {
                TransactionId = transactionId,
                TransactionFingerprint = fingerprint,
                TransactionPhase = TransactionPhase.Prepared,
                ExpeditionFuelManifestUnits = 0,
                RouteProgressTicks = 0,
                RouteMovementAccumulatorMilli = 0,
                VehicleLateralMilli = 0,
                RouteActionUsed = false,
                ReturnPayloadFrozen = false,
                HasArrivalClaimSnapshot = false,
                ArrivalFactionClaimProgressMilli = 0,
                ArrivalFactionClaimState =
                    FactionClaimState.Telegraphed,
                OrdinaryCargoUsedUnits = 0,
                TowSlotsUsed = 0,
                FrameRailSalvageCustody =
                    FrameRailSalvageCustody.WreckLine,
                FactionMemory = factionMemory,
            }.BuildUnchecked();
        }

        private static void AssertRepeatRejected(
            LastBearingState state,
            string completedId,
            string completedFingerprint,
            string expectedCode,
            string label,
            long? sequence = null)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            long commandSequence =
                sequence ?? state.NextCommandSequence;
            string suffix = commandSequence.ToString(
                CultureInfo.InvariantCulture);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new LastBearingCommand[]
                        {
                            new PrepareRepeatExpeditionTransactionCommand(
                                commandSequence,
                                completedId,
                                completedFingerprint,
                                "tx:repeat:" + suffix,
                                "fp:repeat:" + suffix),
                        }),
                    label + " was accepted");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " rejection code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated source state");
        }

        private static void AssertInvariantRejected(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingReadModel.FromState(state),
                    label + " passed invariants");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " invariant code");
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            for (var ticks = 0; ticks < 7000; ticks++)
            {
                if (predicate(driver.View))
                {
                    return;
                }

                driver.OperateWreckLineIfAvailable();
                if (drive)
                {
                    driver.Apply(sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
                }
                else
                {
                    driver.Advance(1);
                }
            }

            throw new InvalidOperationException(
                label + " did not converge");
        }
    }
}
