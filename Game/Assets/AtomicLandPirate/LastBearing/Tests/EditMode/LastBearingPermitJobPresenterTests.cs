#nullable enable

using System;
using System.Collections.Generic;
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
            Assert.That(infrastructure.Headline, Does.Contain("service-cell trial"));

            state = ApplyOne(
                state,
                sequence => new ActivateSliceInfrastructureCommand(sequence));
            LastBearingPermitJobPresentation plan =
                Present(state, cityNeedInspected: true);
            Assert.That(
                plan.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Preparation));
            Assert.That(plan.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(plan.RecommendedFirstRunCue, Does.Contain("CIVIC BUFFER + WINCH"));
            Assert.That(plan.IsFinale, Is.False);
            AssertLegible(plan);
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

            state = AdvanceUntil(
                state,
                model => model.PreparationPhase == PreparationPhase.Ready);
            LastBearingPermitJobPresentation manifest = Present(state, true);
            Assert.That(manifest.Headline, Does.Contain("manifest is ready"));
            Assert.That(manifest.PhaseProgressCurrent, Is.EqualTo(1));
            Assert.That(manifest.PhaseProgressTarget, Is.EqualTo(1));

            state = Depart(state);
            LastBearingPermitJobPresentation outbound = Present(state, true);
            Assert.That(
                outbound.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Outbound));
            Assert.That(outbound.Headline, Does.Contain("Drive"));

            state = AdvanceUntil(
                state,
                model => model.IsWreckLineModulePointAvailable,
                drive: true);
            LastBearingPermitJobPresentation wreckLine = Present(state, true);
            Assert.That(wreckLine.Headline, Does.Contain("winch"));
            Assert.That(wreckLine.HasMeasuredPhaseProgress, Is.True);
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
            state = ApplyOne(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));

            LastBearingPermitJobPresentation depot = Present(state, true);
            Assert.That(
                depot.Chapter,
                Is.EqualTo(LastBearingPermitJobChapter.Depot));
            Assert.That(depot.ShowRecommendedFirstRunCue, Is.True);
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("TAKE THE CLAIMED BEARING"));
            Assert.That(depot.RecommendedFirstRunCue, Does.Contain("One Good Batch"));

            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            LastBearingPermitJobPresentation payload = Present(state, true);
            Assert.That(payload.Headline, Does.Contain("Seal the consequences"));
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
            AssertLegible(returning);
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

            LastBearingPermitJobPresentation selection = Present(state, true);
            Assert.That(selection.Headline, Does.Contain("fills the range tank"));
            Assert.That(selection.Detail, Does.Contain("water or fuel"));
            Assert.That(selection.ProgressLabel, Does.Contain("Load water or fuel"));
            Assert.That(selection.IsFinale, Is.False);

            state = ApplyOne(
                state,
                sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Fuel));
            LastBearingPermitJobPresentation payload = Present(state, true);
            Assert.That(payload.Headline, Does.Contain("Seal the consequences"));
            Assert.That(payload.ProgressLabel, Does.Contain("Freeze"));
            AssertLegible(payload);
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
            Assert.That(midpoint.ProgressLabel, Is.EqualTo("60 / 120 settlement ticks"));

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

        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.WinchAssembly,
            EncounterChoice.Cooperate,
            "promise came home")]
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
            "cistern waits")]
        [TestCase(
            PreparationChoice.WorkshopPush,
            VehicleModule.SealedRangeTank,
            EncounterChoice.TakeBearing,
            "cistern question remains")]
        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.SealedRangeTank,
            EncounterChoice.Cooperate,
            "Depot access waits")]
        [TestCase(
            PreparationChoice.CivicBuffer,
            VehicleModule.SealedRangeTank,
            EncounterChoice.TakeBearing,
            "Depot access needs")]
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
                state = ApplyOne(
                    state,
                    sequence => new InstallCityImprovementCommand(
                        sequence,
                        NextCityDecision.RefurbishAuxiliaryPump,
                        LastBearingState.AuxiliaryPumpSocketId,
                        LastBearingState.AuxiliaryPumpOrientationQuarterTurns));
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
            Assert.That(presentation.RecommendedFirstRunCue, Does.Contain("TAKE THE CLAIMED BEARING"));
            if (preparation == PreparationChoice.WorkshopPush
                && module == VehicleModule.WinchAssembly
                && encounter == EncounterChoice.Cooperate)
            {
                Assert.That(presentation.Detail, Does.Contain("auxiliary pump"));
                Assert.That(presentation.Detail, Does.Contain("field sleeve"));
                Assert.That(presentation.Detail, Does.Contain("maintenance promise"));
            }
            else if (module == VehicleModule.SealedRangeTank
                     && encounter == EncounterChoice.Cooperate)
            {
                Assert.That(presentation.Detail, Does.Contain("range-tank return"));
                Assert.That(presentation.Detail, Does.Contain("field sleeve"));
                Assert.That(presentation.Detail, Does.Contain("maintenance promise"));
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
            Assert.That(serviced.Headline, Does.Contain("promise came home"));
            AssertLegible(serviced);
        }

        private static LastBearingState CreatePreparation(
            PreparationChoice preparation,
            VehicleModule module,
            int worldSeed)
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
            int worldSeed)
        {
            LastBearingState state = CreatePreparation(
                preparation,
                module,
                worldSeed);
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

        private static void AssertLegible(
            LastBearingPermitJobPresentation presentation)
        {
            string[] rawObjectives =
            {
                "activate-slice-infrastructure",
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
