#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingPermitJobPresenterTests
    {
        private const string TransactionId =
            "transaction:last-bearing:permit-job-presenter-test";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:permit-job-presenter-test";

        [Test]
        public void PrologueAndCityCrisisLeadToRecommendedPlanWithoutMutation()
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                3101);
            string initialHash = LastBearingCanonicalCodec.ComputeSha256(state);

            LastBearingPermitJobPresentation prologue =
                LastBearingPermitJobPresenter.Present(null, false);
            Assert.That(
                prologue.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Prologue));
            Assert.That(prologue.Headline, Does.Contain("Choose"));
            Assert.That(prologue.IsFinale, Is.False);

            LastBearingReadModel initial = LastBearingReadModel.FromState(state);
            LastBearingPermitJobPresentation lead =
                LastBearingPermitJobPresenter.Present(initial, false);
            Assert.That(
                lead.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.CityCrisis));
            Assert.That(lead.ProgressLabel, Does.Contain("Assign"));
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(initialHash),
                "early presenter calls mutated canonical state");

            state = ApplyOne(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));
            LastBearingReadModel assigned = LastBearingReadModel.FromState(state);
            LastBearingPermitJobPresentation inspection =
                LastBearingPermitJobPresenter.Present(assigned, false);
            Assert.That(inspection.ProgressLabel, Does.Contain("Inspect"));

            LastBearingPermitJobPresentation infrastructure =
                LastBearingPermitJobPresenter.Present(assigned, true);
            Assert.That(infrastructure.Headline, Does.Contain("Place the recycler"));

            state = CompleteWorkingServiceCell(
                state,
                ResidentRoster.RobotResidentId);
            LastBearingPermitJobPresentation plan =
                Present(state, cityNeedInspected: true);
            Assert.That(
                plan.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Preparation));
            Assert.That(plan.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(plan.RecommendedFirstRunCue, Does.Contain("CIVIC BUFFER + WINCH"));
            Assert.That(plan.RecommendedFirstRunCue, Does.Contain("RECOMMENDED FIRST RUN"));
            Assert.That(plan.RecommendedFirstRunCue, Does.Contain("Nothing is auto-selected"));
            Assert.That(plan.IsFinale, Is.False);
            AssertLegible(plan);
        }

        [Test]
        public void WorkingServiceCellObjectivesExposeExactCostsControlsAndConsequences()
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                3106);
            state = ApplyOne(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));

            AssertWorkingServiceCellGuidance(
                state,
                "place-city-recycler",
                "Place the recycler",
                "SELECT RECYCLER · 2 PARTS",
                "2 reclaimed parts");
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.Recycler,
                    0,
                    1));

            AssertWorkingServiceCellGuidance(
                state,
                "place-city-machine-shop",
                "Place the machine shop",
                "SELECT MACHINE SHOP · 3 PARTS",
                "3 reclaimed-part");
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.MachineShop,
                    1,
                    2));

            AssertWorkingServiceCellGuidance(
                state,
                "place-city-emergency-storage",
                "Place emergency storage",
                "SELECT EMERGENCY STORAGE · 1 PART",
                "1 reclaimed-part");
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.EmergencyStorage,
                    2,
                    3));

            AssertWorkingServiceCellGuidance(
                state,
                "connect-city-service-link",
                "Lock the service link",
                "LOCK SERVICE LINK · 1 PART",
                "permanently");
            state = ApplyOne(
                state,
                sequence => new ConnectCityServiceLinkCommand(sequence));

            AssertWorkingServiceCellGuidance(
                state,
                "staff-city-service-cell",
                "Staff the machine-shop slot",
                "STAFF HUMAN · NEUTRAL",
                "mechanically neutral",
                "STAFF UTILITY ROBOT · NEUTRAL");
            state = ApplyOne(
                state,
                sequence => new AssignCityServiceResidentCommand(
                    sequence,
                    ResidentRoster.RobotResidentId));

            AssertWorkingServiceCellGuidance(
                state,
                "advance-city-service-sled",
                "Send the calibration sled",
                "ADVANCE PARTS SLED",
                "returns no parts yet");
            state = ApplyOne(
                state,
                sequence => new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));

            AssertWorkingServiceCellGuidance(
                state,
                "advance-city-service-sled",
                "Complete the commissioning delivery",
                "COMMISSIONING DELIVERY · ONCE",
                "returns exactly 2 reclaimed parts once");
            state = ApplyOne(
                state,
                sequence => new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));

            LastBearingPermitJobPresentation preparation =
                Present(state, cityNeedInspected: true);
            Assert.That(
                preparation.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Preparation));
            Assert.That(preparation.Headline, Does.Contain("Choose the bargain"));
        }

        [Test]
        public void PreparationRoadDepotAndReturnExposeMeasuredGuidance()
        {
            LastBearingState state = CreatePreparation(
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                worldSeed: 3102);
            LastBearingPermitJobPresentation preparing = Present(state, true);
            Assert.That(
                preparing.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Preparation));
            Assert.That(preparing.HasMeasuredPhaseProgress, Is.True);
            Assert.That(preparing.PhaseProgressIndeterminate, Is.False);
            Assert.That(
                preparing.PhaseProgressTarget,
                Is.EqualTo(
                    LastBearingBalanceV1.CivicBufferBasePreparationTicks +
                    LastBearingBalanceV1.WinchFabricationTicks));
            Assert.That(preparing.PhaseProgressCurrent, Is.GreaterThan(0));
            Assert.That(preparing.ProgressLabel, Does.Contain("remaining"));
            Assert.That(
                preparing.ProgressLabel,
                Does.Contain("keep the settlement unpaused"));

            state = AdvanceUntil(
                state,
                model => model.PreparationPhase == PreparationPhase.Ready);
            LastBearingPermitJobPresentation manifest = Present(state, true);
            Assert.That(manifest.Headline, Does.Contain("manifest is ready"));
            Assert.That(manifest.PhaseProgressCurrent, Is.EqualTo(1));
            Assert.That(manifest.PhaseProgressTarget, Is.EqualTo(1));
            Assert.That(manifest.ProgressLabel, Does.StartWith("Click COMMIT MANIFEST"));
            Assert.That(manifest.ProgressLabel, Does.Contain("chase driving begins"));

            state = Depart(state);
            LastBearingPermitJobPresentation outbound = Present(state, true);
            Assert.That(
                outbound.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Outbound));
            Assert.That(outbound.Headline, Does.Contain("Drive"));
            Assert.That(outbound.ProgressLabel, Does.StartWith("Hold W / right trigger"));
            Assert.That(outbound.Detail, Does.Contain("costs vehicle condition"));

            state = AdvanceUntil(
                state,
                model => model.IsWreckLineModulePointAvailable,
                drive: true);
            LastBearingPermitJobPresentation wreckLine = Present(state, true);
            Assert.That(wreckLine.Headline, Does.Contain("winch"));
            Assert.That(wreckLine.HasMeasuredPhaseProgress, Is.True);
            Assert.That(wreckLine.ProgressLabel, Does.StartWith("Press E / gamepad south"));
            Assert.That(wreckLine.ProgressLabel, Does.Contain("pump rotor"));
            state = ApplyOne(
                state,
                sequence => new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.DeployWinch));

            state = AdvanceUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true);
            LastBearingPermitJobPresentation recovery = Present(state, true);
            Assert.That(recovery.Headline, Does.Contain("recovery bridle"));
            Assert.That(recovery.ProgressLabel, Does.StartWith("Press E / gamepad south"));
            Assert.That(recovery.ProgressLabel, Does.Contain("open the depot decision"));
            state = ApplyOne(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));

            LastBearingPermitJobPresentation depot = Present(state, true);
            Assert.That(
                depot.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Depot));
            Assert.That(depot.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("TAKE THE CERAMIC BEARING"));
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("One Good Batch"));
            Assert.That(depot.ProgressLabel, Does.StartWith("Click COOPERATE"));
            Assert.That(depot.ProgressLabel, Does.Contain("click TAKE"));

            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            LastBearingPermitJobPresentation loading = Present(state, true);
            Assert.That(
                loading.Headline,
                state.RepairCargoCustody == RepairCargoCustody.Faction
                    ? Does.Contain("Load the faction-held ceramic bearing")
                    : Does.Contain("Load the unclaimed ceramic bearing"));
            Assert.That(loading.ProgressLabel, Does.Contain("E or gamepad south"));
            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            LastBearingPermitJobPresentation payload = Present(state, true);
            Assert.That(payload.Headline, Does.Contain("Seal the consequences"));
            Assert.That(payload.ProgressLabel, Does.StartWith("Click FREEZE PAYLOAD"));
            state = ApplyOne(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));

            LastBearingPermitJobPresentation returning = Present(state, true);
            Assert.That(
                returning.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Returning));
            Assert.That(returning.HasMeasuredPhaseProgress, Is.True);
            Assert.That(returning.ProgressLabel, Does.Contain("route ticks"));
            Assert.That(returning.ProgressLabel, Does.StartWith("Hold W / right trigger"));
            AssertLegible(returning);
        }

        [Test]
        public void NonCityHudCopyShowsExactControlsAndConsequencesWithoutMutation()
        {
            LastBearingState state = CreatePreparation(
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                worldSeed: 3105,
                installPatchworkSkidPlate: true);
            string before = LastBearingCanonicalCodec.ComputeSha256(state);
            string recommendedModule = InvokeHudString(
                "BuildGarageModuleLabel",
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);
            string alternateModule = InvokeHudString(
                "BuildGarageModuleLabel",
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            Assert.That(recommendedModule, Does.StartWith("RECOMMENDED FIRST RUN"));
            Assert.That(recommendedModule, Does.Contain("PUMP ROTOR"));
            Assert.That(alternateModule, Does.Not.Contain("RECOMMENDED FIRST RUN"));
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(before),
                "module-label projection mutated canonical state");

            state = AdvanceUntil(
                state,
                model => model.PreparationPhase == PreparationPhase.Ready);
            state = Depart(state);
            string outboundHash = LastBearingCanonicalCodec.ComputeSha256(state);
            LastBearingReadModel outbound = LastBearingReadModel.FromState(state);
            string drivingControls = InvokeHudString(
                "BuildControlsText",
                outbound,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);
            string outboundLedger = InvokeHudString(
                "BuildJourneyLedgerText",
                outbound);
            Assert.That(drivingControls, Does.Contain("Hold W / right trigger"));
            Assert.That(drivingControls, Does.Contain("A/D / left stick"));
            Assert.That(drivingControls, Does.Contain("costs rig condition"));
            Assert.That(drivingControls, Does.Contain("R / gamepad north"));
            Assert.That(outboundLedger, Does.StartWith("ROUTE  outbound"));
            Assert.That(outboundLedger, Does.Contain("RIG  winch fitted"));
            Assert.That(outboundLedger, Does.Contain("patchwork skid plate"));
            Assert.That(
                outboundLedger,
                Does.Contain(
                    "+" +
                    LastBearingBalanceV1.PatchworkSkidPlateProtectionMilli +
                    " protection"));
            Assert.That(outboundLedger, Does.Contain("CARGO  pump rotor waiting at the Wreck Line"));
            Assert.That(
                outboundLedger,
                Does.Contain("frame rails waiting at the Wreck Line"));
            Assert.That(outboundLedger, Does.Contain("CONSEQUENCE  turbine failing"));
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(outboundHash),
                "driving copy projection mutated canonical state");

            state = AdvanceUntil(
                state,
                model => model.IsWreckLineModulePointAvailable,
                drive: true);
            state = ApplyOne(
                state,
                sequence => new OperateWreckLineModuleCommand(
                    sequence,
                    RouteActionKind.DeployWinch));
            LastBearingReadModel railsAvailable =
                LastBearingReadModel.FromState(state);
            Assert.That(
                railsAvailable.NextObjective,
                Is.EqualTo("recover-wreck-line-frame-rails"));
            LastBearingPermitJobPresentation railsCue = Present(state, true);
            Assert.That(railsCue.Headline, Does.Contain("Strip the rails"));
            Assert.That(
                railsCue.ProgressLabel,
                Is.EqualTo(
                    "E — Recover frame rails · +4 reclaimed parts at home"));
            string railControls = InvokeHudString(
                "BuildControlsText",
                railsAvailable,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);
            Assert.That(railControls, Does.Contain("+4 reclaimed parts"));
            state = ApplyOne(
                state,
                sequence => new RecoverWreckLineFrameRailsCommand(sequence));
            string recoveredLedger = InvokeHudString(
                "BuildJourneyLedgerText",
                LastBearingReadModel.FromState(state));
            Assert.That(
                recoveredLedger,
                Does.Contain("frame rails strapped to Sasha's scout"));
            state = AdvanceUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true);
            state = ApplyOne(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            LastBearingReadModel awaitingLoad = LastBearingReadModel.FromState(state);
            string loadControls = InvokeHudString(
                "BuildControlsText",
                awaitingLoad,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);
            Assert.That(loadControls, Does.StartWith("Press E / gamepad south"));
            Assert.That(loadControls, Does.Contain("ceramic bearing"));

            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            string loadedHash = LastBearingCanonicalCodec.ComputeSha256(state);
            LastBearingReadModel loaded = LastBearingReadModel.FromState(state);
            string loadedLedger = InvokeHudString(
                "BuildJourneyLedgerText",
                loaded);
            Assert.That(loadedLedger, Does.Contain("pump rotor on Sasha's scout"));
            Assert.That(loadedLedger, Does.Contain("ceramic bearing on Sasha's scout"));
            Assert.That(loadedLedger, Does.Contain("grievance 40"));
            Assert.That(loadedLedger, Does.Contain("future access consequence pending"));
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(loadedHash),
                "loaded-cargo projection mutated canonical state");
        }

        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.WinchAssembly,
            "auxiliary pump")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.SealedRangeTank,
            "emergency-cistern")]
        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.SealedRangeTank,
            "restoring depot access")]
        public void DepotCueMatchesAlternatePlanWithoutPromisingOneGoodBatch(
            PreparationChoice preparation,
            VehicleModule module,
            string expectedConsequence)
        {
            LastBearingState state = ReachUnresolvedDepot(
                preparation,
                module,
                worldSeed: 3300 + (int)preparation * 10 + (int)module);
            LastBearingPermitJobPresentation depot = Present(state, true);

            Assert.That(
                depot.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Depot));
            Assert.That(depot.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(
                depot.RecommendedFirstRunCue,
                Does.Contain(expectedConsequence).IgnoreCase);
            Assert.That(
                depot.RecommendedFirstRunCue,
                Does.Not.Contain("One Good Batch"));
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("TAKE"));
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("COOPERATE"));
        }

        [Test]
        public void RangeTankDepotGuidesCargoSelectionBeforePayloadFreeze()
        {
            LastBearingState state = ReachUnresolvedDepot(
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                worldSeed: 3304);
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));

            LastBearingPermitJobPresentation loading = Present(state, true);
            Assert.That(
                loading.Headline,
                state.RepairCargoCustody == RepairCargoCustody.Faction
                    ? Does.Contain("Load the faction-held ceramic bearing")
                    : Does.Contain("Load the unclaimed ceramic bearing"));
            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));

            LastBearingPermitJobPresentation selection = Present(state, true);
            Assert.That(selection.Headline, Does.Contain("fills the range tank"));
            Assert.That(selection.Detail, Does.Contain("water or fuel"));
            Assert.That(selection.ProgressLabel, Does.Contain("LOAD WATER"));
            Assert.That(selection.ProgressLabel, Does.Contain("LOAD FUEL"));
            Assert.That(selection.ProgressLabel, Does.Contain("return payload"));
            Assert.That(selection.IsFinale, Is.False);

            state = ApplyOne(
                state,
                sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Fuel));
            LastBearingPermitJobPresentation payload = Present(state, true);
            Assert.That(payload.Headline, Does.Contain("Seal the consequences"));
            Assert.That(payload.ProgressLabel, Does.Contain("FREEZE PAYLOAD"));
            AssertLegible(payload);
        }

        [TestCase(false, "unclaimed", "faction-held")]
        [TestCase(true, "faction-held", "unclaimed")]
        public void LoadedBearingGuidancePreservesCanonicalSourceLineage(
            bool waitForFactionClaim,
            string expectedSource,
            string rejectedSource)
        {
            LastBearingState state = ReachUnresolvedDepot(
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                worldSeed: waitForFactionClaim ? 3311 : 3310,
                waitForFactionClaim: waitForFactionClaim);
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            Assert.That(
                state.RepairCargoCustody,
                Is.EqualTo(
                    waitForFactionClaim
                        ? RepairCargoCustody.Faction
                        : RepairCargoCustody.Depot));

            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            Assert.That(
                state.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));

            LastBearingPermitJobPresentation selection = Present(state, true);
            Assert.That(selection.Detail, Does.Contain(expectedSource));
            Assert.That(selection.Detail, Does.Not.Contain(rejectedSource));

            state = ApplyOne(
                state,
                sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Water));
            LastBearingPermitJobPresentation payload = Present(state, true);
            Assert.That(payload.Detail, Does.Contain(expectedSource));
            Assert.That(payload.Detail, Does.Not.Contain(rejectedSource));
        }

        [Test]
        public void HomecomingBatchBarterAndFinaleAreOneLegibleArc()
        {
            LastBearingState state = CompleteExpedition(
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                worldSeed: 3103);
            LastBearingPermitJobPresentation homecoming = Present(state, true);
            Assert.That(
                homecoming.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Homecoming));
            Assert.That(homecoming.Headline, Does.Contain("waterworks"));

            state = ApplyOne(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));
            LastBearingPermitJobPresentation start = Present(state, true);
            Assert.That(
                start.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Manufacturing));
            Assert.That(start.Headline, Does.Contain("two parts"));
            Assert.That(
                start.PhaseProgressTarget,
                Is.EqualTo(
                    LastBearingBalanceV1.SpareBearingBatchRequiredSettlementTicks));

            state = ApplyOne(
                state,
                sequence => new StartSpareBearingBatchCommand(sequence));
            state = Advance(state, 60);
            LastBearingPermitJobPresentation midpoint = Present(state, true);
            Assert.That(midpoint.PhaseProgressCurrent, Is.EqualTo(60));
            Assert.That(midpoint.PhaseProgressTarget, Is.EqualTo(120));
            Assert.That(midpoint.ProgressLabel, Does.StartWith("60 / 120 settlement ticks"));
            Assert.That(midpoint.ProgressLabel, Does.Contain("keep the settlement unpaused"));
            Assert.That(midpoint.ProgressLabel, Does.Contain("workshop output"));

            state = Advance(state, 60);
            LastBearingPermitJobPresentation barter = Present(state, true);
            Assert.That(
                barter.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Barter));
            Assert.That(barter.Headline, Does.Contain("thing, not a number"));
            Assert.That(barter.IsFinale, Is.False);

            state = ApplyOne(
                state,
                sequence => new BarterSpareBearingLotCommand(sequence));
            string settledHash = LastBearingCanonicalCodec.ComputeSha256(state);
            LastBearingPermitJobPresentation finale = Present(state, true);
            Assert.That(
                finale.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Finale));
            Assert.That(finale.StepIndex, Is.EqualTo(finale.StepCount));
            Assert.That(finale.IsFinale, Is.True);
            Assert.That(finale.IsAlternateConclusion, Is.False);
            Assert.That(finale.Headline, Does.Contain("Permit won"));
            Assert.That(finale.Detail, Does.Contain("2 fuel"));
            Assert.That(finale.ShowRecommendedFirstRunCue, Is.False);
            AssertLegible(finale);
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(settledHash),
                "presenter mutated the settled canonical state");
        }

        [Test]
        public void ExpandedCisternConclusionNamesCompletedCapacity()
        {
            LastBearingState state = CompleteExpedition(
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                EncounterChoice.TakeBearing,
                worldSeed: 3420);
            state = ApplyOne(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));

            LastBearingPermitJobPresentation ready = Present(state, true);
            Assert.That(
                ready.Headline,
                Is.EqualTo(
                    "ALTERNATE WORK ORDER · RANGE-TANK RETURN"));
            Assert.That(ready.Detail, Does.Contain("sealed Water load"));
            Assert.That(ready.Detail, Does.Not.Contain("outside this V0"));

            state = ApplyOne(
                state,
                sequence => new InstallCityImprovementCommand(
                    sequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns));
            LastBearingPermitJobPresentation installed = Present(state, true);

            Assert.That(
                installed.Headline,
                Is.EqualTo(
                    "ALTERNATE CONCLUSION · EMERGENCY STORAGE EXPANDED"));
            Assert.That(installed.Detail, Does.Contain("saddle tanks"));
            Assert.That(installed.Detail, Does.Contain("210.000"));
            Assert.That(installed.IsAlternateConclusion, Is.True);
            Assert.That(
                LastBearingReadModel.FromState(state).WaterCapacityMilli,
                Is.EqualTo(210000));
        }

        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.WinchAssembly,
            EncounterChoice.Cooperate,
            "depot gate stayed open")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.WinchAssembly,
            EncounterChoice.Cooperate,
            "pump turns")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.WinchAssembly,
            EncounterChoice.TakeBearing,
            "recovered rotor joined")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.SealedRangeTank,
            EncounterChoice.Cooperate,
            "cistern grew")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.SealedRangeTank,
            EncounterChoice.TakeBearing,
            "returned range tank joined")]
        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.SealedRangeTank,
            EncounterChoice.Cooperate,
            "depot gate stayed open")]
        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.SealedRangeTank,
            EncounterChoice.TakeBearing,
            "claims ledger is not ready")]
        public void AlternateBranchesCloseHonestlyWithoutFalsePermitFinale(
            PreparationChoice preparation,
            VehicleModule module,
            EncounterChoice encounter,
            string expectedHeadline)
        {
            LastBearingState state = CompleteExpedition(
                preparation,
                module,
                encounter,
                worldSeed: 3200 + (int)preparation * 10 + (int)module);
            state = ApplyOne(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));
            LastBearingReadModel model = LastBearingReadModel.FromState(state);
            if (model.IsCityImprovementInstallationAvailable)
            {
                bool expand =
                    model.NextCityDecision ==
                    NextCityDecision.ExpandEmergencyCistern;
                state = ApplyOne(
                    state,
                    sequence => new InstallCityImprovementCommand(
                        sequence,
                        expand
                            ? NextCityDecision.ExpandEmergencyCistern
                            : NextCityDecision.RefurbishAuxiliaryPump,
                        expand
                            ? LastBearingState
                                .EmergencyStorageExpansionSocketId
                            : LastBearingState.AuxiliaryPumpSocketId,
                        expand
                            ? LastBearingState
                                .EmergencyStorageExpansionOrientationQuarterTurns
                            : LastBearingState
                                .AuxiliaryPumpOrientationQuarterTurns));
            }

            string before = LastBearingCanonicalCodec.ComputeSha256(state);
            LastBearingPermitJobPresentation presentation = Present(state, true);
            Assert.That(
                presentation.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.AlternateConclusion));
            Assert.That(presentation.IsFinale, Is.False);
            Assert.That(presentation.IsAlternateConclusion, Is.True);
            Assert.That(presentation.Headline, Does.Contain(expectedHeadline).IgnoreCase);
            Assert.That(presentation.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(presentation.RecommendedFirstRunCue, Does.Contain("CIVIC BUFFER + WINCH"));
            Assert.That(presentation.RecommendedFirstRunCue, Does.Contain("TAKE THE CERAMIC BEARING"));
            if (preparation == PreparationChoice.WorkshopPush
                && module == VehicleModule.WinchAssembly
                && encounter == EncounterChoice.Cooperate)
            {
                Assert.That(presentation.Detail, Does.Contain("auxiliary pump"));
                Assert.That(presentation.Detail, Does.Contain("field sleeve"));
                Assert.That(presentation.Detail, Does.Contain("maintenance promise"));
            }
            else if (preparation == PreparationChoice.WorkshopPush
                     && module == VehicleModule.SealedRangeTank
                     && encounter == EncounterChoice.Cooperate)
            {
                Assert.That(
                    presentation.Detail,
                    Does.Contain("range tank").IgnoreCase);
                Assert.That(presentation.Detail, Does.Contain("field sleeve"));
                Assert.That(presentation.Detail, Does.Contain("maintenance promise"));
            }
            else if (preparation == PreparationChoice.CivicBuffer
                     && encounter == EncounterChoice.Cooperate)
            {
                Assert.That(
                    presentation.Detail,
                    Does.Contain("shared depot service"));
                Assert.That(
                    presentation.Detail,
                    Does.Contain("No paid fuel bond is pending"));
            }

            AssertLegible(presentation);
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(before),
                "alternate presentation mutated canonical state");
        }

        [Test]
        public void DueMaintenanceIsActionableAndConclusionReturnsAfterService()
        {
            LastBearingState state = CompleteExpedition(
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.Cooperate,
                worldSeed: 3401);
            state = ApplyOne(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));
            state = AdvanceUntil(state, model => model.MaintenanceDue);

            LastBearingPermitJobPresentation due = Present(state, true);
            Assert.That(
                due.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Homecoming));
            Assert.That(due.Headline, Does.Contain("Service"));
            Assert.That(due.IsFinale, Is.False);
            Assert.That(due.IsAlternateConclusion, Is.False);
            Assert.That(due.ShowRecommendedFirstRunCue, Is.False);

            state = ApplyOne(
                state,
                sequence => new ServiceFieldSleeveCommand(sequence));
            LastBearingPermitJobPresentation serviced = Present(state, true);
            Assert.That(
                serviced.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.AlternateConclusion));
            Assert.That(serviced.IsAlternateConclusion, Is.True);
            Assert.That(
                serviced.Headline,
                Does.Contain("depot gate stayed open").IgnoreCase);
            AssertLegible(serviced);
        }

        private static void AssertWorkingServiceCellGuidance(
            LastBearingState state,
            string expectedObjective,
            string expectedHeadline,
            string expectedControl,
            string expectedConsequence,
            params string[] additionalControls)
        {
            string before = LastBearingCanonicalCodec.ComputeSha256(state);
            LastBearingReadModel model = LastBearingReadModel.FromState(state);
            Assert.That(model.NextObjective, Is.EqualTo(expectedObjective));

            LastBearingPermitJobPresentation presentation =
                LastBearingPermitJobPresenter.Present(
                    model,
                    cityNeedInspected: true);
            string hudControls = InvokeHudString(
                "BuildControlsText",
                model,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.That(
                presentation.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.CityCrisis));
            Assert.That(presentation.Headline, Does.Contain(expectedHeadline));
            Assert.That(
                presentation.ProgressLabel,
                Does.Contain(expectedControl));
            Assert.That(
                presentation.Detail,
                Does.Contain(expectedConsequence));
            Assert.That(hudControls, Does.Contain(expectedControl));
            foreach (string additionalControl in additionalControls)
            {
                Assert.That(
                    presentation.ProgressLabel,
                    Does.Contain(additionalControl));
                Assert.That(hudControls, Does.Contain(additionalControl));
            }

            AssertLegible(presentation);
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(state),
                Is.EqualTo(before),
                expectedObjective + " guidance mutated canonical state");
        }

        private static LastBearingState CompleteWorkingServiceCell(
            LastBearingState state,
            string operatorStableId)
        {
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.Recycler,
                    0,
                    0));
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.MachineShop,
                    1,
                    0));
            state = ApplyOne(
                state,
                sequence => new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.EmergencyStorage,
                    2,
                    0));
            state = ApplyOne(
                state,
                sequence => new ConnectCityServiceLinkCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new AssignCityServiceResidentCommand(
                    sequence,
                    operatorStableId));
            state = ApplyOne(
                state,
                sequence => new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            return ApplyOne(
                state,
                sequence => new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
        }

        private static LastBearingState CreatePreparation(
            PreparationChoice preparation,
            VehicleModule module,
            int worldSeed,
            bool installPatchworkSkidPlate = false)
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                worldSeed);
            state = ApplyMany(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId),
                sequence => new ActivateSliceInfrastructureCommand(sequence));
            if (installPatchworkSkidPlate)
            {
                state = ApplyOne(
                    state,
                    sequence => new InstallRigUpgradeCommand(
                        sequence,
                        RigUpgrade.PatchworkSkidPlate));
            }

            return ApplyMany(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    preparation,
                    module),
                sequence => new InstallVehicleModuleCommand(sequence, module));
        }

        private static LastBearingState CompleteExpedition(
            PreparationChoice preparation,
            VehicleModule module,
            EncounterChoice encounter,
            int worldSeed)
        {
            LastBearingState state = ReachUnresolvedDepot(
                preparation,
                module,
                worldSeed);
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(sequence, encounter));
            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                state = ApplyOne(
                    state,
                    sequence => new ChooseLiquidReturnCommand(
                        sequence,
                        LiquidCargoKind.Water));
            }

            state = ApplyOne(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = AdvanceUntil(
                state,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true);
            return ApplyMany(
                state,
                sequence => new CreditCityReturnCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
        }

        private static LastBearingState ReachUnresolvedDepot(
            PreparationChoice preparation,
            VehicleModule module,
            int worldSeed,
            bool waitForFactionClaim = false)
        {
            LastBearingState state = CreatePreparation(
                preparation,
                module,
                worldSeed);
            if (waitForFactionClaim)
            {
                state = Advance(state, 9000);
                Assert.That(
                    state.FactionClaimState,
                    Is.EqualTo(FactionClaimState.Claimed));
                Assert.That(
                    state.DepotBearingDisposition,
                    Is.EqualTo(DepotBearingDisposition.FactionHeld));
            }

            state = AdvanceUntil(
                state,
                model => model.PreparationPhase == PreparationPhase.Ready);
            state = Depart(state);
            state = AdvanceUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                operateWreckLine: true);
            return ApplyOne(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));
        }

        private static LastBearingState Depart(LastBearingState state)
        {
            return ApplyMany(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DepartExpeditionCommand(sequence));
        }

        private static LastBearingState AdvanceUntil(
            LastBearingState state,
            Func<LastBearingReadModel, bool> completed,
            bool drive = false,
            bool operateWreckLine = false)
        {
            for (var tick = 0; tick < 2000; tick++)
            {
                LastBearingReadModel model = LastBearingReadModel.FromState(state);
                if (completed(model))
                {
                    return state;
                }

                if (operateWreckLine && model.IsWreckLineModulePointAvailable)
                {
                    state = ApplyOne(
                        state,
                        sequence => new OperateWreckLineModuleCommand(
                            sequence,
                            model.RouteActionKind));
                }
                else if (operateWreckLine &&
                         model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = ApplyOne(
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(sequence));
                }
                else if (drive)
                {
                    state = ApplyOne(
                        state,
                        sequence => new DriveVehicleCommand(
                            sequence,
                            1000,
                            0));
                }
                else
                {
                    state = Step(state);
                }
            }

            Assert.Fail("Presenter fixture did not reach the requested state.");
            return state;
        }

        private static LastBearingState Advance(LastBearingState state, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                state = Step(state);
            }

            return state;
        }

        private static LastBearingState Step(LastBearingState state)
        {
            return new LastBearingKernel().Step(
                state,
                Array.Empty<LastBearingCommand>()).State;
        }

        private static LastBearingState ApplyOne(
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return ApplyMany(state, create);
        }

        private static LastBearingState ApplyMany(
            LastBearingState state,
            params Func<long, LastBearingCommand>[] factories)
        {
            var commands = new List<LastBearingCommand>(factories.Length);
            for (var index = 0; index < factories.Length; index++)
            {
                commands.Add(factories[index](checked(
                    state.NextCommandSequence + index)));
            }

            return new LastBearingKernel().Step(state, commands).State;
        }

        private static LastBearingPermitJobPresentation Present(
            LastBearingState state,
            bool cityNeedInspected)
        {
            return LastBearingPermitJobPresenter.Present(
                LastBearingReadModel.FromState(state),
                cityNeedInspected);
        }

        private static string InvokeHudString(
            string methodName,
            params object[] arguments)
        {
            MethodInfo? method = typeof(LastBearingHud).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName + " test seam is missing");
            object? result = method!.Invoke(null, arguments);
            Assert.That(result, Is.TypeOf<string>());
            return (string)result!;
        }

        private static void AssertLegible(
            LastBearingPermitJobPresentation presentation)
        {
            string[] rawObjectives =
            {
                "activate-slice-infrastructure",
                "place-city-recycler",
                "place-city-machine-shop",
                "place-city-emergency-storage",
                "connect-city-service-link",
                "staff-city-service-cell",
                "advance-city-service-sled",
                "complete-preparation",
                "drive-to-depot",
                "resolve-depot",
                "drive-home",
                "machine-one-good-batch",
                "route-permit-recorded",
                "await-next-city-decision-authority",
            };
            string visible = presentation.ChapterLabel + "\n" +
                             presentation.Headline + "\n" +
                             presentation.Detail + "\n" +
                             presentation.ProgressLabel + "\n" +
                             presentation.RecommendedFirstRunCue;
            foreach (string rawObjective in rawObjectives)
            {
                Assert.That(visible, Does.Not.Contain(rawObjective));
            }
        }
    }
}
