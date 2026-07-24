#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class ServiceScoutTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "scout service spends one hot shift output and keeps reserve",
                ServiceCostAndReserveAreExact);
            harness.Run(
                "scout service changes only parts and vehicle condition",
                ExactTransitionAndEvents);
            harness.Run(
                "scout service waits for every urgent return task",
                UrgentReturnWorkHasPriority);
            harness.Run(
                "scout service rejects forged delivered aid lineage",
                ForgedDeliveredAidCannotUnlockService);
            harness.Run(
                "scout service is preparation module outcome and composition neutral",
                FullChoiceMatrixSharesMechanics);
            harness.Run(
                "scout service ready and accepted states round trip in schema 9",
                ReadyAndAcceptedStatesRoundTrip);
            harness.Run(
                "scout service rejects invalid and stale intents atomically",
                InvalidAndStaleIntentsFailAtomically);
            harness.Run(
                "scout service replay cannot spend parts twice",
                DuplicateServiceIsIdempotent);
            harness.Run(
                "scout service preserves expedition and faction history",
                UnrelatedReturnStateIsPreserved);
        }

        private static void ServiceCostAndReserveAreExact()
        {
            CoreTestDriver driver = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3301);
            driver = WithParts(
                driver,
                checked(
                    LastBearingBalanceV1.HotShiftOutputPartsUnits
                    + LastBearingBalanceV1.MinimumPostReturnPartsUnits));
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftOutputPartsUnits,
                driver.View.VehicleServicePartsCostUnits,
                "service cost read model");
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                driver.View.VehicleServiceReservePartsUnits,
                "service reserve read model");
            TestHarness.True(
                driver.View.IsVehicleServiceNeeded,
                "damaged returned scout did not need service");
            TestHarness.True(
                driver.View.IsVehicleServiceAvailable,
                "exact cost plus reserve was unavailable");
            TestHarness.Equal(
                "service-scout-in-garage",
                driver.View.NextObjective,
                "service objective");

            Pause(driver);
            long before = driver.State.PartsUnits;
            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            TestHarness.Equal(
                before - LastBearingBalanceV1.HotShiftOutputPartsUnits,
                driver.State.PartsUnits,
                "service parts debit");
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                driver.State.PartsUnits,
                "retained parts reserve");
            TestHarness.Equal(
                LastBearingBalanceV1.StartingVehicleConditionMilli,
                driver.State.VehicleConditionMilli,
                "restored condition");
            TestHarness.True(
                !driver.View.IsVehicleServiceNeeded,
                "serviced scout still needed service");
            TestHarness.True(
                !driver.View.IsVehicleServiceAvailable,
                "serviced scout remained available");
            TestHarness.True(
                driver.View.NextObjective != "service-scout-in-garage",
                "accepted service retained service objective");

            CoreTestDriver insufficient = WithParts(
                ReachServiceReady(
                    ColonyComposition.Mixed,
                    ResidentRoster.HumanResidentId,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly,
                    EncounterChoice.TakeBearing,
                    3302),
                checked(
                    LastBearingBalanceV1.HotShiftOutputPartsUnits
                    + LastBearingBalanceV1.MinimumPostReturnPartsUnits
                    - 1));
            TestHarness.True(
                insufficient.View.IsVehicleServiceNeeded,
                "insufficient state hid service need");
            TestHarness.True(
                !insufficient.View.IsVehicleServiceAvailable,
                "service consumed the retained reserve");
            AssertRejected(
                insufficient.State,
                insufficient.State.NextCommandSequence,
                "LAST_BEARING_VEHICLE_SERVICE_PARTS_INSUFFICIENT",
                "insufficient parts");
        }

        private static void ExactTransitionAndEvents()
        {
            CoreTestDriver driver = ReachServiceReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3303);
            Pause(driver);
            LastBearingState ready = driver.State;
            long expectedParts = checked(
                ready.PartsUnits
                - LastBearingBalanceV1.HotShiftOutputPartsUnits);

            LastBearingTickResult result = driver.Apply(sequence =>
                new ServiceScoutCommand(sequence));
            LastBearingState ordinaryTick =
                new LastBearingKernel().Step(
                    ready,
                    Array.Empty<LastBearingCommand>()).State;
            LastBearingState expected =
                new LastBearingStateBuilder(ordinaryTick)
                {
                    NextCommandSequence = checked(
                        ready.NextCommandSequence + 1),
                    PartsUnits = expectedParts,
                    VehicleConditionMilli =
                        LastBearingBalanceV1
                            .StartingVehicleConditionMilli,
                }.Build();
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(expected).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(result.State)),
                "service changed state outside exact transition");
            TestHarness.Equal(2, result.DomainEvents.Count, "event count");

            LastBearingDomainEvent resources = result.DomainEvents[0];
            TestHarness.Equal(
                LastBearingEventKind.CityResourcesCommitted,
                resources.Kind,
                "first event kind");
            TestHarness.Equal(
                LastBearingEventCause.PlayerCommand,
                resources.Cause,
                "first event cause");
            TestHarness.Equal(
                "settlement:last-bearing:parts",
                resources.SubjectId,
                "first event subject");
            TestHarness.Equal(
                ready.PartsUnits,
                resources.BeforeValue,
                "first event before");
            TestHarness.Equal(
                expectedParts,
                resources.AfterValue,
                "first event after");

            LastBearingDomainEvent condition = result.DomainEvents[1];
            TestHarness.Equal(
                LastBearingEventKind.VehicleConditionChanged,
                condition.Kind,
                "second event kind");
            TestHarness.Equal(
                LastBearingEventCause.PlayerCommand,
                condition.Cause,
                "second event cause");
            TestHarness.Equal(
                "vehicle:sasha:service-cell",
                condition.SubjectId,
                "second event subject");
            TestHarness.Equal(
                ready.VehicleConditionMilli,
                condition.BeforeValue,
                "second event before");
            TestHarness.Equal(
                LastBearingBalanceV1.StartingVehicleConditionMilli,
                condition.AfterValue,
                "second event after");
        }

        private static void UrgentReturnWorkHasPriority()
        {
            CoreTestDriver pendingImprovement = ReachRepairedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3304);
            AssertBlocked(
                pendingImprovement,
                "pending city improvement");

            CoreTestDriver queuedAid = ReachRepairedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                EncounterChoice.Cooperate,
                3305);
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterQueued,
                queuedAid.State.FactionAidPolicy,
                "queued aid fixture");
            AssertBlocked(queuedAid, "queued emergency aid");

            CoreTestDriver batch = ReachRepairedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3306);
            batch.Apply(sequence =>
                new StartSpareBearingBatchCommand(sequence));
            AssertBlocked(batch, "active one good batch");
            AdvanceUntil(
                batch,
                model => model.SpareBearingBatchPhase
                    == SpareBearingBatchPhase.Complete,
                drive: false,
                "one good batch completion");
            AssertBlocked(batch, "unbartered one good batch");
            batch.Apply(sequence =>
                new BarterSpareBearingLotCommand(sequence));
            TestHarness.True(
                batch.View.IsVehicleServiceAvailable,
                "barter resolution did not reveal service");

            CoreTestDriver fuelBond = ReachRepairedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                EncounterChoice.TakeBearing,
                3307);
            TestHarness.True(
                fuelBond.View.IsDepotAccessRestorationAvailable,
                "fuel bond fixture");
            AssertBlocked(fuelBond, "unposted fuel bond");
            fuelBond.Apply(sequence =>
                new RestoreDepotAccessCommand(sequence));
            TestHarness.True(
                fuelBond.View.IsVehicleServiceAvailable,
                "fuel bond resolution did not reveal service");

            CoreTestDriver hotShift = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3308);
            hotShift.Apply(sequence => new RunHotShiftCommand(
                sequence,
                hotShift.State.HotShiftCompletedCount));
            AssertBlocked(hotShift, "active hot shift");

            CoreTestDriver maintenance = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.Cooperate,
                3309);
            AdvanceUntil(
                maintenance,
                model => model.MaintenanceDue,
                drive: false,
                "field sleeve maintenance");
            AssertBlocked(maintenance, "due field sleeve maintenance");

            CoreTestDriver dust = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3310);
            dust = new CoreTestDriver(
                new LastBearingStateBuilder(dust.State)
                {
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks,
                    DustFrontOutcome = DustFrontOutcome.Held,
                    IsDustFrontAcknowledgementRequired = true,
                    PauseCause = PauseCause.DustFrontAlert,
                }.Build());
            AssertBlocked(dust, "unacknowledged dust front");
        }

        private static void ForgedDeliveredAidCannotUnlockService()
        {
            LastBearingState delivered = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.Cooperate,
                3311).State;
            LastBearingReadModel natural =
                LastBearingReadModel.FromState(delivered);
            TestHarness.True(
                natural.IsEmergencyAidReceptionComplete,
                "natural delivered aid lineage");
            TestHarness.True(
                natural.IsVehicleServiceAvailable,
                "natural delivered aid blocked service");

            (string Label, LastBearingState State)[] forgeries =
            {
                (
                    "bearing disposition",
                    new LastBearingStateBuilder(delivered)
                    {
                        DepotBearingDisposition =
                            DepotBearingDisposition.AtDepot,
                    }.Build()),
                (
                    "pending outcome",
                    new LastBearingStateBuilder(delivered)
                    {
                        PendingFactionOutcome =
                            FactionOutcomeKind.Adverse,
                    }.Build()),
                (
                    "outcome maturation",
                    new LastBearingStateBuilder(delivered)
                    {
                        FactionOutcomeElapsedTicks =
                            LastBearingBalanceV1
                                .FactionOutcomeMaturationTicks - 1,
                    }.Build()),
                (
                    "depot fee",
                    new LastBearingStateBuilder(delivered)
                    {
                        DepotAccessFeePartsUnits = 1,
                    }.Build()),
                (
                    "maintenance parts",
                    new LastBearingStateBuilder(delivered)
                    {
                        MaintenancePartsUnits =
                            LastBearingBalanceV1
                                .SleeveMaintenancePartsUnits + 1,
                    }.Build()),
                (
                    "faction memory",
                    new LastBearingStateBuilder(delivered)
                    {
                        FactionMemory = new FactionMemoryRecord(
                            "memory:last-bearing:cooperate:forged",
                            "CooperateAtBearingDepot",
                            LastBearingState.LastBearingFactionId,
                            LastBearingBalanceV1.CooperateTrustDelta,
                            "shared-maintenance",
                            delivered.GlobalTick,
                            "FIELD_SLEEVE_SERVICE"),
                    }.Build()),
            };

            foreach (var forgery in forgeries)
            {
                LastBearingReadModel forged =
                    LastBearingReadModel.FromState(forgery.State);
                TestHarness.True(
                    !forged.IsEmergencyAidReceptionComplete,
                    forgery.Label +
                    " retained the exact delivered-aid witness");
                TestHarness.True(
                    !forged.IsVehicleServiceAvailable,
                    forgery.Label + " exposed scout service");
                TestHarness.True(
                    forged.NextObjective != "service-scout-in-garage",
                    forgery.Label + " exposed the service objective");
                AssertRejected(
                    forgery.State,
                    forgery.State.NextCommandSequence,
                    "LAST_BEARING_VEHICLE_SERVICE_NOT_READY",
                    forgery.Label);
            }
        }

        private static void FullChoiceMatrixSharesMechanics()
        {
            var compositions = new[]
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };
            var preparations = new[]
            {
                PreparationChoice.WorkshopPush,
                PreparationChoice.CivicBuffer,
            };
            var modules = new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            };
            var outcomes = new[]
            {
                EncounterChoice.Cooperate,
                EncounterChoice.TakeBearing,
            };
            int seed = 3320;

            foreach (PreparationChoice preparation in preparations)
            {
                foreach (VehicleModule module in modules)
                {
                    foreach (EncounterChoice outcome in outcomes)
                    {
                        string? expectedHash = null;
                        int sharedSeed = seed++;
                        foreach (ColonyComposition composition in compositions)
                        {
                            string resident =
                                composition == ColonyComposition.RobotOnly
                                    ? ResidentRoster.RobotResidentId
                                    : ResidentRoster.HumanResidentId;
                            CoreTestDriver driver = ReachServiceReady(
                                composition,
                                resident,
                                preparation,
                                module,
                                outcome,
                                sharedSeed);
                            TestHarness.True(
                                driver.View.IsVehicleServiceNeeded,
                                composition + " " + preparation + " "
                                    + module + " " + outcome + " need");
                            TestHarness.True(
                                driver.View.IsVehicleServiceAvailable,
                                composition + " " + preparation + " "
                                    + module + " " + outcome
                                    + " availability");
                            Pause(driver);
                            long before = driver.State.PartsUnits;
                            driver.Apply(sequence =>
                                new ServiceScoutCommand(sequence));
                            TestHarness.Equal(
                                before
                                    - LastBearingBalanceV1
                                        .HotShiftOutputPartsUnits,
                                driver.State.PartsUnits,
                                composition + " " + preparation + " "
                                    + module + " " + outcome + " cost");
                            TestHarness.Equal(
                                LastBearingBalanceV1
                                    .StartingVehicleConditionMilli,
                                driver.State.VehicleConditionMilli,
                                composition + " " + preparation + " "
                                    + module + " " + outcome + " condition");

                            string hash =
                                LastBearingCanonicalCodec
                                    .ComputeMechanicalSha256(driver.State);
                            if (expectedHash == null)
                            {
                                expectedHash = hash;
                            }
                            else
                            {
                                TestHarness.Equal(
                                    expectedHash,
                                    hash,
                                    preparation + " " + module + " "
                                        + outcome
                                        + " composition mechanics");
                            }
                        }
                    }
                }
            }
        }

        private static void ReadyAndAcceptedStatesRoundTrip()
        {
            LastBearingState ready = ReachServiceReady(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                EncounterChoice.TakeBearing,
                3340).State;
            LastBearingState restoredReady = RoundTrip(ready, "ready");
            LastBearingReadModel readyView =
                LastBearingReadModel.FromState(restoredReady);
            TestHarness.True(
                readyView.IsVehicleServiceNeeded,
                "restored ready need");
            TestHarness.True(
                readyView.IsVehicleServiceAvailable,
                "restored ready availability");
            TestHarness.Equal(
                "service-scout-in-garage",
                readyView.NextObjective,
                "restored ready objective");

            CoreTestDriver accepted = new CoreTestDriver(restoredReady);
            Pause(accepted);
            accepted.Apply(sequence =>
                new ServiceScoutCommand(sequence));
            LastBearingState restoredAccepted =
                RoundTrip(accepted.State, "accepted");
            LastBearingReadModel acceptedView =
                LastBearingReadModel.FromState(restoredAccepted);
            TestHarness.True(
                !acceptedView.IsVehicleServiceNeeded,
                "restored accepted need");
            TestHarness.True(
                !acceptedView.IsVehicleServiceAvailable,
                "restored accepted availability");
            TestHarness.Equal(
                LastBearingBalanceV1.StartingVehicleConditionMilli,
                acceptedView.VehicleConditionMilli,
                "restored accepted condition");
            TestHarness.Equal(
                9,
                restoredAccepted.SchemaVersion,
                "service changed save schema");
        }

        private static void InvalidAndStaleIntentsFailAtomically()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    3341);
            AssertRejected(
                initial,
                initial.NextCommandSequence,
                "LAST_BEARING_VEHICLE_SERVICE_NOT_READY",
                "untouched full-condition scout");

            CoreTestDriver ready = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                EncounterChoice.TakeBearing,
                3342);
            AssertRejected(
                ready.State,
                checked(ready.State.NextCommandSequence + 1),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "future command sequence");

            LastBearingState away =
                new LastBearingStateBuilder(ready.State)
                {
                    ExpeditionPhase = ExpeditionPhase.Returned,
                }.BuildUnchecked();
            AssertInvariantRejected(
                away,
                "LAST_BEARING_RETURN_OWNERSHIP_INVALID",
                "forged returned phase");
        }

        private static void DuplicateServiceIsIdempotent()
        {
            CoreTestDriver driver = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3343);
            Pause(driver);
            driver.Apply(sequence =>
                new ServiceScoutCommand(sequence));
            LastBearingState beforeReplay = driver.State;
            LastBearingTickResult replay = driver.Apply(sequence =>
                new ServiceScoutCommand(sequence));
            LastBearingState ordinaryTick =
                new LastBearingKernel().Step(
                    beforeReplay,
                    Array.Empty<LastBearingCommand>()).State;
            LastBearingState expected =
                new LastBearingStateBuilder(ordinaryTick)
                {
                    NextCommandSequence = checked(
                        beforeReplay.NextCommandSequence + 1),
                }.Build();
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(expected).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(replay.State)),
                "service replay changed authoritative state");
            TestHarness.Equal(
                1,
                replay.DomainEvents.Count,
                "service replay event count");
            TestHarness.Equal(
                LastBearingEventKind.IdempotentReplayAccepted,
                replay.DomainEvents[0].Kind,
                "service replay event");
        }

        private static void UnrelatedReturnStateIsPreserved()
        {
            CoreTestDriver driver = ReachServiceReady(
                ColonyComposition.Mixed,
                ResidentRoster.RobotResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                EncounterChoice.TakeBearing,
                3344);
            Pause(driver);
            LastBearingState before = driver.State;
            driver.Apply(sequence =>
                new ServiceScoutCommand(sequence));

            TestHarness.Equal(
                before.TransactionId,
                driver.State.TransactionId,
                "transaction id");
            TestHarness.Equal(
                before.TransactionFingerprint,
                driver.State.TransactionFingerprint,
                "transaction fingerprint");
            TestHarness.Equal(
                before.DepotResolution,
                driver.State.DepotResolution,
                "depot outcome");
            TestHarness.Equal(
                before.FactionMemory,
                driver.State.FactionMemory,
                "faction memory");
            TestHarness.Equal(
                before.FactionTrust,
                driver.State.FactionTrust,
                "faction trust");
            TestHarness.Equal(
                before.FactionGrievance,
                driver.State.FactionGrievance,
                "faction grievance");
            TestHarness.Equal(
                before.FutureRouteTollFuelUnits,
                driver.State.FutureRouteTollFuelUnits,
                "future toll");
            TestHarness.Equal(
                before.RoutePermitGranted,
                driver.State.RoutePermitGranted,
                "route permit");
            TestHarness.Equal(
                before.LiquidCargoKind,
                driver.State.LiquidCargoKind,
                "liquid kind");
            TestHarness.Equal(
                before.LiquidCargoQuantityMilli,
                driver.State.LiquidCargoQuantityMilli,
                "liquid quantity");
            TestHarness.Equal(
                before.LiquidCargoCustody,
                driver.State.LiquidCargoCustody,
                "liquid custody");
            TestHarness.Equal(
                before.InstalledCityImprovement,
                driver.State.InstalledCityImprovement,
                "city improvement");
        }

        private static CoreTestDriver ReachServiceReady(
            ColonyComposition composition,
            string residentId,
            PreparationChoice preparation,
            VehicleModule module,
            EncounterChoice outcome,
            int worldSeed)
        {
            CoreTestDriver driver = ReachRepairedReturn(
                composition,
                residentId,
                preparation,
                module,
                outcome,
                worldSeed);

            if (driver.State.FactionAidPolicy
                == FactionAidPolicy.EmergencyWaterQueued)
            {
                driver.Apply(sequence =>
                    new ReceiveEmergencyAidCommand(sequence));
            }

            switch (driver.State.NextCityDecision)
            {
                case NextCityDecision.RefurbishAuxiliaryPump:
                    driver.Apply(sequence =>
                        new InstallCityImprovementCommand(
                            sequence,
                            NextCityDecision.RefurbishAuxiliaryPump,
                            LastBearingState.AuxiliaryPumpSocketId,
                            LastBearingState
                                .AuxiliaryPumpOrientationQuarterTurns));
                    break;
                case NextCityDecision.ExpandEmergencyCistern:
                    driver.Apply(sequence =>
                        new InstallCityImprovementCommand(
                            sequence,
                            NextCityDecision.ExpandEmergencyCistern,
                            LastBearingState
                                .EmergencyStorageExpansionSocketId,
                            LastBearingState
                                .EmergencyStorageExpansionOrientationQuarterTurns));
                    break;
                case NextCityDecision.MachineSpareBearing:
                    driver.Apply(sequence =>
                        new StartSpareBearingBatchCommand(sequence));
                    AdvanceUntil(
                        driver,
                        model => model.SpareBearingBatchPhase
                            == SpareBearingBatchPhase.Complete,
                        drive: false,
                        "one good batch");
                    driver.Apply(sequence =>
                        new BarterSpareBearingLotCommand(sequence));
                    break;
                case NextCityDecision.RestoreDepotAccess:
                    driver.Apply(sequence =>
                        new RestoreDepotAccessCommand(sequence));
                    break;
                case NextCityDecision.None:
                    break;
                default:
                    throw new InvalidOperationException(
                        "unexpected service prerequisite decision");
            }

            TestHarness.True(
                driver.View.IsVehicleServiceNeeded,
                "service-ready fixture condition");
            TestHarness.True(
                driver.View.IsVehicleServiceAvailable,
                "service-ready fixture availability");
            TestHarness.Equal(
                "service-scout-in-garage",
                driver.View.NextObjective,
                "service-ready fixture objective");
            return driver;
        }

        private static CoreTestDriver ReachRepairedReturn(
            ColonyComposition composition,
            string residentId,
            PreparationChoice preparation,
            VehicleModule module,
            EncounterChoice outcome,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(residentId, preparation, module);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "preparation");

            string transactionId = "tx:service-scout:" + worldSeed;
            string fingerprint = "fp:service-scout:" + worldSeed;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(sequence, outcome));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                driver.Apply(sequence =>
                    new ChooseLiquidReturnCommand(
                        sequence,
                        preparation == PreparationChoice.WorkshopPush
                            ? LiquidCargoKind.Water
                            : LiquidCargoKind.Fuel));
            }

            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "home return");
            driver.Apply(sequence =>
                new CreditCityReturnCommand(
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

        private static CoreTestDriver WithParts(
            CoreTestDriver driver,
            long parts)
        {
            return new CoreTestDriver(
                new LastBearingStateBuilder(driver.State)
                {
                    PartsUnits = parts,
                }.Build());
        }

        private static void Pause(CoreTestDriver driver)
        {
            if (driver.State.PauseCause == PauseCause.None)
            {
                driver.Apply(sequence =>
                    new SetPauseCommand(sequence, true));
            }
        }

        private static void AssertBlocked(
            CoreTestDriver driver,
            string label)
        {
            TestHarness.True(
                driver.View.IsVehicleServiceNeeded,
                label + " hid service need");
            TestHarness.True(
                !driver.View.IsVehicleServiceAvailable,
                label + " exposed service");
            TestHarness.True(
                driver.View.NextObjective != "service-scout-in-garage",
                label + " exposed service objective");
            AssertRejected(
                driver.State,
                driver.State.NextCommandSequence,
                "LAST_BEARING_VEHICLE_SERVICE_NOT_READY",
                label);
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

        private static void AssertRejected(
            LastBearingState state,
            long sequence,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new LastBearingCommand[]
                        {
                            new ServiceScoutCommand(sequence),
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
