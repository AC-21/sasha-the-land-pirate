#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class ScenarioTests
    {
        public static void Run(
            TestHarness harness,
            string repoRoot,
            string scenarioId)
        {
            harness.Run(scenarioId + " pinned inputs", () =>
                ScenarioIntegrityPreflight.Verify(repoRoot, scenarioId));
            RunMechanics(harness, scenarioId);
        }

        public static void RunMechanics(TestHarness harness, string scenarioId)
        {
            harness.Run(scenarioId + " authoritative mechanics", () =>
            {
                switch (scenarioId)
                {
                    case "SCN_COMPOSITION_LOOP_SMOKE":
                        CompositionLoop();
                        break;
                    case "SCN_TIME_POLICY":
                        TimePolicy();
                        break;
                    case "SCN_PREPARATION_MODULE_MATRIX":
                        PreparationMatrix();
                        break;
                    case "SCN_FACTION_WAIT_CLAIM":
                        FactionWaitClaim();
                        break;
                    case "SCN_BEARING_COOPERATE":
                        BearingCooperate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "unknown protected scenario " + scenarioId);
                }
            });
        }

        private static void CompositionLoop()
        {
            LastBearingReadModel human = CompleteCooperateLoop(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2011);
            LastBearingReadModel robot = CompleteCooperateLoop(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                2011);
            LastBearingReadModel mixed = CompleteCooperateLoop(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                2011);
            AssertRoster(human, ResidentRoster.HumanResidentId);
            AssertRoster(robot, ResidentRoster.RobotResidentId);
            AssertRoster(
                mixed,
                ResidentRoster.HumanResidentId,
                ResidentRoster.RobotResidentId);
            string expected = MechanicalOutcome(human);
            TestHarness.Equal(expected, MechanicalOutcome(robot), "robot-only loop mechanics");
            TestHarness.Equal(expected, MechanicalOutcome(mixed), "mixed loop mechanics");
        }

        private static void TimePolicy()
        {
            const string transactionId = "tx:time";
            const string fingerprint = "fp:time";
            CoreTestDriver driver = ReadyForRoad(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                transactionId,
                fingerprint,
                2014);
            TestHarness.Equal(ExpeditionPhase.Outbound, driver.View.ExpeditionPhase, "road phase");
            LastBearingReadModel outboundBefore = driver.View;
            long settlementBefore = driver.View.SettlementTick;
            long factionBefore = driver.View.FactionTick;
            long crisisBefore = driver.View.CrisisTick;
            long roadBefore = driver.View.RoadTick;
            for (int index = 0; index < 20; index++)
            {
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            long outboundRoadTicks = driver.View.RoadTick - roadBefore;
            long expectedOutboundHomeTicks = ExpectedSlowedTicks(outboundRoadTicks);
            TestHarness.Equal(20L, outboundRoadTicks, "full-rate road clock");
            TestHarness.True(
                expectedOutboundHomeTicks > 0 && expectedOutboundHomeTicks < outboundRoadTicks,
                "selected policy is not slowed continuation");
            TestHarness.Equal(
                expectedOutboundHomeTicks,
                driver.View.SettlementTick - settlementBefore,
                "slowed settlement clock");
            TestHarness.Equal(
                expectedOutboundHomeTicks,
                driver.View.FactionTick - factionBefore,
                "slowed faction clock");
            TestHarness.Equal(
                expectedOutboundHomeTicks,
                driver.View.CrisisTick - crisisBefore,
                "slowed crisis clock");
            AssertForecastsAdvanced(
                outboundBefore,
                driver.View,
                includeFactionClaim: true,
                includeArrival: true,
                phase: "outbound");

            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            LastBearingReadModel explicitPause = driver.View;
            driver.Advance(10);
            AssertClocksAndForecastsFrozen(explicitPause, driver.View, "explicit pause");
            TestHarness.Equal(PauseCause.Explicit, driver.View.PauseCause, "explicit pause cause");

            driver.Apply(sequence => new SetPauseCommand(sequence, false));
            DriveUntilDepotRecoveryAvailable(driver, 400);
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            TestHarness.Equal(0L, RequiredForecast(
                driver.View.RouteArrivalGlobalTicks,
                "arrival forecast at depot"), "arrival forecast at depot");

            driver.Apply(sequence => new TriggerAutoPauseAlertCommand(sequence));
            TestHarness.Equal(PauseCause.AutoAlert, driver.View.PauseCause, "auto-pause cause");
            LastBearingReadModel autoPause = driver.View;
            driver.Advance(10);
            AssertClocksAndForecastsFrozen(autoPause, driver.View, "auto pause");
            TestHarness.Throws<InvalidOperationException>(
                () => driver.Apply(sequence => new SetPauseCommand(sequence, false)),
                "explicit unpause cleared an unresolved auto-alert");
            TestHarness.Equal(
                PauseCause.AutoAlert,
                driver.View.PauseCause,
                "failed unpause changed auto-alert state");

            long trustBeforeEncounter = driver.View.FactionTrust;
            LastBearingTickResult resolution = driver.Apply(
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));
            TestHarness.Equal(PauseCause.None, driver.View.PauseCause, "encounter clears auto-pause");
            TestHarness.True(
                resolution.DomainEvents.Count > 1,
                "encounter resolution omitted expected domain events");
            LastBearingDomainEvent resume = resolution.DomainEvents[0];
            TestHarness.Equal(
                LastBearingEventKind.PauseChanged,
                resume.Kind,
                "encounter outcome preceded auto-pause resume event");
            TestHarness.Equal(
                LastBearingEventCause.SystemTransition,
                resume.Cause,
                "auto-pause resume event cause");
            TestHarness.Equal(
                (long)PauseCause.AutoAlert,
                resume.BeforeValue,
                "auto-pause resume event before value");
            TestHarness.Equal(
                (long)PauseCause.None,
                resume.AfterValue,
                "auto-pause resume event after value");
            TestHarness.True(
                driver.View.FactionTrust > trustBeforeEncounter,
                "encounter did not apply the cooperative consequence");
            TestHarness.Equal(
                0L,
                driver.View.ClaimContestedFactionTicks,
                "resolved depot contested-claim forecast");
            TestHarness.Equal(
                0L,
                driver.View.ClaimedFactionTicks,
                "resolved depot claimed forecast");

            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            TestHarness.Equal(ExpeditionPhase.Returning, driver.View.ExpeditionPhase, "return phase");
            TestHarness.Equal(
                driver.View.RouteTargetTicks,
                RequiredForecast(driver.View.RouteReturnGlobalTicks, "return forecast"),
                "initial return forecast");
            LastBearingReadModel returnBefore = driver.View;
            DriveUntil(driver, ExpeditionPhase.Returned, 400);
            LastBearingReadModel returned = driver.View;
            long returnRoadTicks = returned.RoadTick - returnBefore.RoadTick;
            TestHarness.Equal(
                driver.View.RouteTargetTicks,
                returnRoadTicks,
                "full-rate return road clock");
            TestHarness.Equal(
                ExpectedSlowedTicks(returnRoadTicks),
                returned.SettlementTick - returnBefore.SettlementTick,
                "slowed settlement clock during return");
            TestHarness.Equal(
                ExpectedSlowedTicks(returnRoadTicks),
                returned.FactionTick - returnBefore.FactionTick,
                "slowed faction clock during return");
            TestHarness.Equal(
                ExpectedSlowedTicks(returnRoadTicks),
                returned.CrisisTick - returnBefore.CrisisTick,
                "slowed crisis clock during return");
            TestHarness.Equal(
                0L,
                RequiredForecast(returned.RouteReturnGlobalTicks, "completed return forecast"),
                "completed return forecast");
            AssertForecastsAdvanced(
                returnBefore,
                returned,
                includeFactionClaim: false,
                includeArrival: false,
                phase: "return");

            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new FinalizeExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            TestHarness.Equal(ExpeditionPhase.AtHome, driver.View.ExpeditionPhase, "home phase");
            TestHarness.Equal(TransactionPhase.Finalized, driver.View.TransactionPhase, "finalized return");
            LastBearingReadModel homeBefore = driver.View;
            driver.Advance(10);
            TestHarness.Equal(
                10L,
                driver.View.SettlementTick - homeBefore.SettlementTick,
                "full-rate home settlement clock");
            TestHarness.Equal(
                10L,
                driver.View.FactionTick - homeBefore.FactionTick,
                "full-rate home faction clock");
            TestHarness.Equal(
                10L,
                driver.View.CrisisTick - homeBefore.CrisisTick,
                "full-rate home crisis clock");
            TestHarness.Equal(
                homeBefore.RoadTick,
                driver.View.RoadTick,
                "home road clock remains stopped");
            AssertForecastsAdvanced(
                homeBefore,
                driver.View,
                includeFactionClaim: false,
                includeArrival: false,
                phase: "home");
        }

        private static void PreparationMatrix()
        {
            var outcomes = new HashSet<string>(StringComparer.Ordinal);
            int viable = 0;
            int changedNextDecision = 0;
            foreach (PreparationChoice choice in new[]
            {
                PreparationChoice.WorkshopPush,
                PreparationChoice.CivicBuffer,
            })
            {
                foreach (VehicleModule module in new[]
                {
                    VehicleModule.WinchAssembly,
                    VehicleModule.SealedRangeTank,
                })
                {
                    var initial = new CoreTestDriver(ColonyComposition.HumanOnly, 2012);
                    CoreTestDriver driver = CompleteReturnLoop(
                        ColonyComposition.HumanOnly,
                        ResidentRoster.HumanResidentId,
                        choice,
                        module,
                        "tx:matrix:" + choice + ":" + module,
                        "fp:matrix:" + choice + ":" + module,
                        2012);
                    TestHarness.Equal(
                        ExpeditionPhase.AtHome,
                        driver.View.ExpeditionPhase,
                        "matrix finalized home phase");
                    TestHarness.Equal(
                        TransactionPhase.Finalized,
                        driver.View.TransactionPhase,
                        "matrix transaction phase");
                    outcomes.Add(string.Join(
                        "|",
                        driver.View.PreparationChoice,
                        driver.View.VehicleModule,
                        driver.View.RouteKind,
                        driver.View.RouteTargetTicks,
                        driver.View.PartsUnits,
                        driver.View.FuelUnits,
                        driver.View.WaterMilli,
                        driver.View.HeavyCargoKind,
                        driver.View.LiquidCargoKind,
                        driver.View.RepairCargoKind,
                        driver.View.RepairCargoCustody,
                        driver.View.NextCityDecision));
                    if (driver.View.PartsUnits >= 0 &&
                        driver.View.FuelUnits >= 0 &&
                        driver.View.WaterMilli >= 0)
                    {
                        viable++;
                    }

                    if (driver.View.NextCityDecision != initial.View.NextCityDecision)
                    {
                        changedNextDecision++;
                    }
                }
            }

            TestHarness.Equal(4, outcomes.Count, "systemically distinct matrix outcomes");
            TestHarness.True(viable >= 2, "fewer than two viable matrix rows");
            TestHarness.True(
                changedNextDecision >= 2,
                "fewer than two returns changed the next city decision");
        }

        private static void FactionWaitClaim()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2013);
            driver.Advance(9000);
            TestHarness.True(
                driver.View.FactionClaimProgressMilli >=
                    LastBearingBalanceV1.FactionClaimThresholdMilli,
                "faction claim did not reach threshold");
            TestHarness.Equal(FactionClaimState.Claimed, driver.View.FactionClaimState, "claim state");
            TestHarness.Equal(DepotControl.FactionClaimed, driver.View.DepotControl, "depot control");
            TestHarness.Equal(
                FactionAccessPolicy.PermitRequired,
                driver.View.FactionAccessPolicy,
                "persistent access terms");
        }

        private static void BearingCooperate()
        {
            var before = new CoreTestDriver(ColonyComposition.HumanOnly, 2002);
            LastBearingReadModel outcome = CompleteCooperateLoop(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2002);
            TestHarness.Equal(TransactionPhase.Finalized, outcome.TransactionPhase, "transaction phase");
            TestHarness.Equal(TurbineCondition.SleeveRepaired, outcome.TurbineCondition, "turbine repair");
            TestHarness.True(outcome.IsWaterRecovering, "water did not recover");
            TestHarness.Equal(MaintenanceRecipe.FieldSleeveService, outcome.MaintenanceRecipe, "recipe");
            TestHarness.True(outcome.MaintenanceObligationActive, "maintenance obligation missing");
            TestHarness.True(outcome.FactionTrust > 0, "cooperation did not build trust");
            int factionBehaviorChanges = 0;
            factionBehaviorChanges += before.View.FactionAccessPolicy != outcome.FactionAccessPolicy ? 1 : 0;
            factionBehaviorChanges += before.View.FactionAidPolicy != outcome.FactionAidPolicy ? 1 : 0;
            factionBehaviorChanges += before.View.FactionTrust != outcome.FactionTrust ? 1 : 0;
            factionBehaviorChanges += before.View.FactionGrievance != outcome.FactionGrievance ? 1 : 0;
            TestHarness.True(
                factionBehaviorChanges >= 2,
                "cooperation changed fewer than two faction behaviors");

            int changedDomains = 0;
            changedDomains += before.View.TurbineCondition != outcome.TurbineCondition ? 1 : 0;
            changedDomains += before.View.TransactionPhase != outcome.TransactionPhase ? 1 : 0;
            changedDomains += factionBehaviorChanges > 0 ? 1 : 0;
            changedDomains += before.View.MaintenanceRecipe != outcome.MaintenanceRecipe ? 1 : 0;
            TestHarness.True(changedDomains >= 3, "fewer than three persistent domains changed");
        }

        private static CoreTestDriver ReadyForRoad(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            string transactionId,
            string fingerprint,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(residentId, choice, module);
            int guard = 0;
            while (driver.View.PreparationPhase != PreparationPhase.Ready && guard < 1000)
            {
                driver.Advance(1);
                guard++;
            }

            TestHarness.Equal(PreparationPhase.Ready, driver.View.PreparationPhase, "readiness");
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                driver.Apply(sequence => new DepartExpeditionCommand(sequence));
            }

            return driver;
        }

        private static LastBearingReadModel CompleteCooperateLoop(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            const string transactionId = "tx:cooperate:001";
            const string fingerprint = "fp:cooperate:001";
            CoreTestDriver driver = CompleteReturnLoop(
                composition,
                residentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                transactionId,
                fingerprint,
                worldSeed);
            driver.Apply(sequence => new InstallTurbineRepairCommand(sequence));
            driver.Advance(8);

            byte[] encoded = LastBearingCanonicalCodec.Encode(driver.State);
            LastBearingDecodeResult decoded = LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(decoded.Succeeded && decoded.State != null, "loop save decode failed");
            TestHarness.True(
                encoded.SequenceEqual(LastBearingCanonicalCodec.Encode(decoded.State!)),
                "loop save bytes changed after decode");
            return LastBearingReadModel.FromState(decoded.State!);
        }

        private static CoreTestDriver CompleteReturnLoop(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            string transactionId,
            string fingerprint,
            int worldSeed)
        {
            CoreTestDriver driver = ReadyForRoad(
                composition,
                residentId,
                choice,
                module,
                transactionId,
                fingerprint,
                worldSeed);
            DriveUntilDepotRecoveryAvailable(driver, 400);
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.Cooperate));
            if (module == VehicleModule.SealedRangeTank)
            {
                LiquidCargoKind liquid = choice == PreparationChoice.CivicBuffer
                    ? LiquidCargoKind.Fuel
                    : LiquidCargoKind.Water;
                driver.Apply(sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    liquid));
            }

            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                driver.Apply(sequence => new ReturnHomeCommand(sequence));
            }

            DriveUntil(driver, ExpeditionPhase.Returned, 400);
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new FinalizeExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            return driver;
        }

        private static void DriveUntil(
            CoreTestDriver driver,
            ExpeditionPhase expected,
            int maximumTicks)
        {
            int ticks = 0;
            while (driver.View.ExpeditionPhase != expected && ticks < maximumTicks)
            {
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
                ticks++;
            }

            TestHarness.Equal(expected, driver.View.ExpeditionPhase, "route phase");
        }

        private static void DriveUntilDepotRecoveryAvailable(
            CoreTestDriver driver,
            int maximumTicks)
        {
            int ticks = 0;
            while (!driver.View.IsDepotApproachRecoveryAvailable
                   && ticks < maximumTicks)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                ticks++;
            }

            TestHarness.True(
                driver.View.IsDepotApproachRecoveryAvailable,
                "depot recovery gate was not reached");
        }

        private static string MechanicalOutcome(LastBearingReadModel view)
        {
            return string.Join(
                "|",
                view.WaterMilli,
                view.WaterTrendMilliPerSettlementTick,
                view.PartsUnits,
                view.FuelUnits,
                view.TurbineCondition,
                view.VehicleModule,
                view.VehicleLateralMilli,
                view.VehicleConditionMilli,
                view.TransactionPhase,
                view.RepairCargoKind,
                view.RepairCargoCustody,
                view.FactionClaimState,
                view.DepotControl,
                view.FactionAccessPolicy,
                view.FactionAidPolicy,
                view.FactionTrust,
                view.FactionGrievance,
                view.MaintenanceRecipe,
                view.MaintenanceObligationActive,
                view.MaintenanceDue,
                view.NextCityDecision,
                view.IsWaterRecovering,
                view.NextObjective);
        }

        private static void AssertRoster(
            LastBearingReadModel view,
            params string[] expectedResidentIds)
        {
            TestHarness.Equal(expectedResidentIds.Length, view.Residents.Count, "resident count after reload");
            TestHarness.True(
                expectedResidentIds.SequenceEqual(
                    view.Residents.Select(resident => resident.StableId)),
                "resident set changed after reload");
        }

        private static void AssertClocksAndForecastsFrozen(
            LastBearingReadModel before,
            LastBearingReadModel after,
            string phase)
        {
            TestHarness.Equal(before.SettlementTick, after.SettlementTick, phase + " settlement clock");
            TestHarness.Equal(before.FactionTick, after.FactionTick, phase + " faction clock");
            TestHarness.Equal(before.CrisisTick, after.CrisisTick, phase + " crisis clock");
            TestHarness.Equal(before.RoadTick, after.RoadTick, phase + " road clock");
            TestHarness.Equal(before.RouteProgressTicks, after.RouteProgressTicks, phase + " route progress");
            TestHarness.Equal(
                before.WaterZeroSettlementTicks,
                after.WaterZeroSettlementTicks,
                phase + " water forecast");
            TestHarness.Equal(
                before.ClaimContestedFactionTicks,
                after.ClaimContestedFactionTicks,
                phase + " contested-claim forecast");
            TestHarness.Equal(
                before.ClaimedFactionTicks,
                after.ClaimedFactionTicks,
                phase + " claimed forecast");
            TestHarness.Equal(
                before.DustFrontCrisisTicks,
                after.DustFrontCrisisTicks,
                phase + " dust-front forecast");
            TestHarness.Equal(
                before.RouteArrivalGlobalTicks,
                after.RouteArrivalGlobalTicks,
                phase + " arrival forecast");
            TestHarness.Equal(
                before.RouteReturnGlobalTicks,
                after.RouteReturnGlobalTicks,
                phase + " return forecast");
        }

        private static void AssertForecastsAdvanced(
            LastBearingReadModel before,
            LastBearingReadModel after,
            bool includeFactionClaim,
            bool includeArrival,
            string phase)
        {
            long settlementDelta = after.SettlementTick - before.SettlementTick;
            long crisisDelta = after.CrisisTick - before.CrisisTick;
            TestHarness.Equal(
                RequiredForecast(before.WaterZeroSettlementTicks, phase + " starting water forecast")
                    - settlementDelta,
                RequiredForecast(after.WaterZeroSettlementTicks, phase + " ending water forecast"),
                phase + " water forecast follows settlement clock");
            TestHarness.Equal(
                before.DustFrontCrisisTicks - crisisDelta,
                after.DustFrontCrisisTicks,
                phase + " dust-front forecast follows crisis clock");

            if (includeFactionClaim)
            {
                long factionDelta = after.FactionTick - before.FactionTick;
                TestHarness.Equal(
                    before.ClaimContestedFactionTicks - factionDelta,
                    after.ClaimContestedFactionTicks,
                    phase + " contested-claim forecast follows faction clock");
                TestHarness.Equal(
                    before.ClaimedFactionTicks - factionDelta,
                    after.ClaimedFactionTicks,
                    phase + " claimed forecast follows faction clock");
            }

            if (includeArrival)
            {
                long progressDelta = after.RouteProgressTicks - before.RouteProgressTicks;
                TestHarness.Equal(
                    RequiredForecast(before.RouteArrivalGlobalTicks, phase + " starting arrival forecast")
                        - progressDelta,
                    RequiredForecast(after.RouteArrivalGlobalTicks, phase + " ending arrival forecast"),
                    phase + " arrival forecast follows route progress");
            }
        }

        private static long RequiredForecast(long? value, string name)
        {
            if (!value.HasValue)
            {
                throw new InvalidOperationException(name + " is unavailable");
            }

            return value.Value;
        }

        private static long ExpectedSlowedTicks(long roadTicks)
        {
            return checked(
                roadTicks * LastBearingBalanceV1.ExpeditionHomeClockScaleMilli
                / LastBearingBalanceV1.FullClockScaleMilli);
        }
    }
}
