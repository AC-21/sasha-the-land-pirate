#nullable enable

using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum LastBearingPermitJobChapter
    {
        Prologue = 0,
        CityCrisis = 1,
        Preparation = 2,
        Outbound = 3,
        Depot = 4,
        Returning = 5,
        Homecoming = 6,
        Manufacturing = 7,
        Barter = 8,
        Finale = 9,
        AlternateConclusion = 10,
    }

    /// <summary>
    /// Immutable, presentation-only copy for the bounded Last Bearing job.
    /// It contains no command, Unity object, save seam, or canonical state.
    /// </summary>
    public sealed class LastBearingPermitJobPresentation
    {
        internal LastBearingPermitJobPresentation(
            LastBearingPermitJobChapter chapter,
            int stepIndex,
            int stepCount,
            string chapterLabel,
            string headline,
            string detail,
            string progressLabel,
            long phaseProgressCurrent,
            long phaseProgressTarget,
            bool phaseProgressIndeterminate,
            bool isFinale,
            bool isAlternateConclusion,
            string recommendedFirstRunCue)
        {
            Chapter = chapter;
            StepIndex = stepIndex;
            StepCount = stepCount;
            ChapterLabel = chapterLabel;
            Headline = headline;
            Detail = detail;
            ProgressLabel = progressLabel;
            PhaseProgressCurrent = phaseProgressCurrent;
            PhaseProgressTarget = phaseProgressTarget;
            PhaseProgressIndeterminate = phaseProgressIndeterminate;
            IsFinale = isFinale;
            IsAlternateConclusion = isAlternateConclusion;
            RecommendedFirstRunCue = recommendedFirstRunCue;
        }

        public LastBearingPermitJobChapter Chapter { get; }

        public int StepIndex { get; }

        public int StepCount { get; }

        public string ChapterLabel { get; }

        public string Headline { get; }

        public string Detail { get; }

        public string ProgressLabel { get; }

        public long PhaseProgressCurrent { get; }

        public long PhaseProgressTarget { get; }

        public bool PhaseProgressIndeterminate { get; }

        public bool HasMeasuredPhaseProgress => PhaseProgressTarget > 0;

        public bool IsFinale { get; }

        public bool IsAlternateConclusion { get; }

        public string RecommendedFirstRunCue { get; }

        public bool ShowRecommendedFirstRunCue =>
            RecommendedFirstRunCue.Length > 0;
    }

    /// <summary>
    /// Pure projection from the canonical read model plus the controller's
    /// local inspection flag into legible player guidance. The presenter never
    /// mutates the model and never exposes raw objective identifiers.
    /// </summary>
    public static class LastBearingPermitJobPresenter
    {
        private const int JobStepCount = 9;
        private const string NoCue = "";
        private const string RecommendedPlanCue =
            "FIRST RUN · CIVIC BUFFER + WINCH opens the complete " +
            "manufacturing-and-permit continuation.";
        private const string RecommendedDepotCue =
            "FIRST RUN · TAKE THE CERAMIC BEARING opens One Good Batch; " +
            "COOPERATE closes on the maintenance promise.";
        private const string ReplayCue =
            "REPLAY CUE · Choose CIVIC BUFFER + WINCH, then TAKE THE " +
            "CERAMIC BEARING to reach manufacturing and barter.";

        public static LastBearingPermitJobPresentation Present(
            LastBearingReadModel? model,
            bool cityNeedInspected)
        {
            if (model == null)
            {
                return Create(
                    LastBearingPermitJobChapter.Prologue,
                    0,
                    "PROLOGUE · LAST BEARING",
                    "Choose who calls this place home",
                    "Human, utility-robot, and mixed colonies share the same " +
                    "Permit Job mechanics in this build.",
                    "Choose a colony composition or load the fixed profile.");
            }

            if (IsPermitJobFinale(model))
            {
                string water = model.IsWaterRecovering
                    ? "Water is recovering. "
                    : "Water remains under pressure. ";
                return Create(
                    LastBearingPermitJobChapter.Finale,
                    JobStepCount,
                    "FINALE · THE ROAD REMEMBERS",
                    "Permit won. Debt retained.",
                    water +
                    "The physical spare-bearing lot is at the claims wicket. " +
                    "Last Bearing holds the corridor permit, but the depot " +
                    "remains aggrieved and the next passage still costs " +
                    model.FutureRouteTollFuelUnits + " fuel.",
                    "Permit Job complete.",
                    phaseProgressCurrent: 1,
                    phaseProgressTarget: 1,
                    isFinale: true);
            }

            if (model.AssignedResidentId == null)
            {
                return Create(
                    LastBearingPermitJobChapter.CityCrisis,
                    1,
                    "CHAPTER I · THE FALLING RESERVE",
                    "Name an expedition lead",
                    "The roster is valid, but the road manifest needs one " +
                    "resident in the lead slot.",
                    "Assign the default expedition lead.");
            }

            if (!cityNeedInspected)
            {
                return Create(
                    LastBearingPermitJobChapter.CityCrisis,
                    1,
                    "CHAPTER I · THE FALLING RESERVE",
                    "Read the waterworks before spending",
                    "Inspect the stopped turbine and falling reserve before " +
                    "committing labor, parts, or fuel.",
                    "Inspect the failing water system.");
            }

            if (model.NextObjective == "activate-slice-infrastructure")
            {
                return Create(
                    LastBearingPermitJobChapter.CityCrisis,
                    1,
                    "CHAPTER I · THE FALLING RESERVE",
                    "Lay out one service-cell trial",
                    "Stage the recycler and machine shop, move one empty " +
                    "calibration sled, and record whether its path reads " +
                    "clearly. Either reversible trial unlocks the same " +
                    "canonical infrastructure fact without selecting D-0030.",
                    "Complete one city trial, then bring the service cell online.");
            }

            if (model.PreparationPhase == PreparationPhase.Unselected)
            {
                return Create(
                    LastBearingPermitJobChapter.Preparation,
                    2,
                    "CHAPTER II · WHAT HOME CAN SPARE",
                    "Choose the bargain before the road",
                    "Preparation sets the city's water risk; the fitted module " +
                    "sets the route and the cargo Sasha can bring home.",
                    "Choose one preparation and one vehicle module.",
                    recommendedFirstRunCue: RecommendedPlanCue);
            }

            if (model.PreparationPhase == PreparationPhase.Preparing)
            {
                return Create(
                    LastBearingPermitJobChapter.Preparation,
                    2,
                    "CHAPTER II · WHAT HOME CAN SPARE",
                    "The service bay is working",
                    FormatPlan(model) +
                    " is being prepared on the settlement clock. Inspect the " +
                    "city, pump hall, workshop, or garage while the crew works.",
                    model.PreparationElapsedTicks + " / " +
                    model.PreparationRequiredTicks + " settlement ticks · " +
                    model.PreparationRemainingTicks + " remaining",
                    model.PreparationElapsedTicks,
                    model.PreparationRequiredTicks);
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome
                && model.TurbineCondition == TurbineCondition.Failing
                && model.RepairCargoKind == RepairCargoKind.None
                && (model.PreparationPhase == PreparationPhase.Ready
                    || model.PreparationPhase == PreparationPhase.Committed))
            {
                return Create(
                    LastBearingPermitJobChapter.Preparation,
                    2,
                    "CHAPTER II · WHAT HOME CAN SPARE",
                    "The manifest is ready",
                    FormatPlan(model) +
                    " is fitted. Commit the bounded fuel and cargo manifest " +
                    "before Sasha takes responsibility for the road.",
                    "Commit the manifest and depart.",
                    phaseProgressCurrent: 1,
                    phaseProgressTarget: 1);
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Outbound)
            {
                if (model.IsWreckLineModulePointAvailable)
                {
                    bool winch = model.RouteActionKind == RouteActionKind.DeployWinch;
                    return Create(
                        LastBearingPermitJobChapter.Outbound,
                        3,
                        "CHAPTER III · THE WRECK LINE",
                        winch
                            ? "Put the winch on the old rotor"
                            : "Seal up for the dust exposure",
                        winch
                            ? "The one existing pump rotor is within reach. " +
                              "Operate the fitted winch before the route can continue."
                            : "The rotor stays behind on this route. Use the sealed " +
                              "range tank to cross the exposure.",
                        winch
                            ? "Operate the Wreck Line winch."
                            : "Cross the sealed dust exposure.",
                        model.RouteProgressTicks,
                        model.RouteTargetTicks);
                }

                if (model.IsDepotApproachRecoveryAvailable)
                {
                    return Create(
                        LastBearingPermitJobChapter.Outbound,
                        3,
                        "CHAPTER III · THE DEPOT APPROACH",
                        "Seat the recovery bridle",
                        "The route is complete, but the encounter remains locked " +
                        "until Sasha secures the authored recovery point.",
                        "Operate the depot recovery point.",
                        model.RouteProgressTicks,
                        model.RouteTargetTicks);
                }

                return Create(
                    LastBearingPermitJobChapter.Outbound,
                    3,
                    "CHAPTER III · THE ROAD OUT",
                    "Drive the corridor",
                    "Throttle advances the canonical route. Steering outside " +
                    "the safe road half-width costs vehicle condition.",
                    FormatRouteProgress(model),
                    model.RouteProgressTicks,
                    model.RouteTargetTicks);
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                if (model.RepairCargoKind == RepairCargoKind.None)
                {
                    return Create(
                        LastBearingPermitJobChapter.Depot,
                        4,
                        "CHAPTER IV · THE LAST BEARING",
                        "Choose what the depot will remember",
                        "Cooperate for a field sleeve and maintenance promise, " +
                        "or take the ceramic bearing without agreement and carry the grievance home.",
                        "Resolve the depot encounter.",
                        recommendedFirstRunCue: DepotChoiceCue(model));
                }

                bool fieldSleeve =
                    model.RepairCargoKind == RepairCargoKind.FieldSleeve;
                bool ceramicWasFactionHeld =
                    model.RepairCargoKind == RepairCargoKind.CeramicBearing &&
                    model.FactionClaimProgressMilli ==
                        LastBearingBalanceV1.FactionClaimThresholdMilli;
                string cargoDetail = fieldSleeve
                    ? "The field sleeve comes with an ongoing maintenance promise."
                    : ceramicWasFactionHeld
                        ? "The faction-held ceramic bearing carries an aggrieved faction memory."
                        : "Taking the unclaimed ceramic bearing creates an aggrieved faction memory.";
                if (model.IsRepairCargoLoadAvailable)
                {
                    return Create(
                        LastBearingPermitJobChapter.Depot,
                        4,
                        "CHAPTER IV · WORK THE DEPOT",
                        fieldSleeve
                            ? "Load the faction field sleeve"
                            : ceramicWasFactionHeld
                                ? "Load the faction-held ceramic bearing"
                                : "Load the unclaimed ceramic bearing",
                        cargoDetail +
                        (model.RepairCargoCustody == RepairCargoCustody.Faction
                            ? " It remains at the faction service stand until Sasha loads the scout cargo socket."
                            : " It remains at the depot cradle until Sasha loads the scout cargo socket."),
                        "Load the repair cargo with E or gamepad south.");
                }

                if (model.VehicleModule == VehicleModule.SealedRangeTank
                    && model.LiquidCargoKind == LiquidCargoKind.None)
                {
                    return Create(
                        LastBearingPermitJobChapter.Depot,
                        4,
                        "CHAPTER IV · THE RETURN LOAD",
                        "Choose what fills the range tank",
                        cargoDetail +
                        " The return payload cannot freeze until the tank carries " +
                        "either emergency water or fuel.",
                        "Load water or fuel.");
                }

                return Create(
                    LastBearingPermitJobChapter.Depot,
                    4,
                    "CHAPTER IV · THE RETURN LOAD",
                    "Seal the consequences into the manifest",
                    cargoDetail +
                    " Freeze the exact payload before turning toward home.",
                    "Freeze the payload and begin the return.");
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Returning)
            {
                return Create(
                    LastBearingPermitJobChapter.Returning,
                    5,
                    "CHAPTER V · THE ROAD HOME",
                    "Bring the consequence back intact",
                    "The frozen payload, vehicle condition, faction memory, " +
                    "and recovered cargo all travel together.",
                    FormatRouteProgress(model),
                    model.RouteProgressTicks,
                    model.RouteTargetTicks);
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Returned
                || model.TransactionPhase != TransactionPhase.Finalized)
            {
                return Create(
                    LastBearingPermitJobChapter.Homecoming,
                    6,
                    "CHAPTER VI · HOMECOMING",
                    "Credit the road back to Last Bearing",
                    "Move the frozen return payload into settlement custody and " +
                    "finalize the one expedition transaction.",
                    "Credit and finalize the return.");
            }

            if (model.TurbineCondition == TurbineCondition.Failing
                && model.RepairCargoKind != RepairCargoKind.None)
            {
                return Create(
                    LastBearingPermitJobChapter.Homecoming,
                    6,
                    "CHAPTER VI · HOMECOMING",
                    "Make the waterworks turn again",
                    model.RepairCargoKind == RepairCargoKind.CeramicBearing
                        ? "Install the taken ceramic bearing. The repair does not " +
                          "erase the depot's grievance."
                        : "Install the cooperative field sleeve. Its maintenance " +
                          "obligation remains part of the settlement.",
                    "Install the turbine repair.");
            }

            if (model.IsCityImprovementInstallationAvailable)
            {
                return Create(
                    LastBearingPermitJobChapter.Homecoming,
                    6,
                    "ALTERNATE HOMEWORK · THE PUMP HALL",
                    "Seat the recovered rotor",
                    "Workshop Push + Winch returned the physical pump rotor. " +
                    "Install it at the fixed auxiliary-pump socket to close this branch.",
                    "Install the refurbished auxiliary pump.");
            }

            if (model.IsSpareBearingBatchStartAvailable)
            {
                return Create(
                    LastBearingPermitJobChapter.Manufacturing,
                    7,
                    "CHAPTER VII · ONE GOOD BATCH",
                    "Commit exactly two parts",
                    "The machine shop can make one physical spare-bearing lot " +
                    "while retaining the two-part civic reserve.",
                    "Start the one approved spare-bearing batch.",
                    phaseProgressCurrent: 0,
                    phaseProgressTarget:
                        LastBearingBalanceV1.SpareBearingBatchRequiredSettlementTicks);
            }

            if (model.SpareBearingBatchPhase == SpareBearingBatchPhase.InProgress)
            {
                return Create(
                    LastBearingPermitJobChapter.Manufacturing,
                    7,
                    "CHAPTER VII · ONE GOOD BATCH",
                    "Keep the machine turning",
                    "The committed inputs are on the machine. Paused settlement " +
                    "time does not advance this fixed batch.",
                    model.SpareBearingElapsedTicks + " / " +
                    model.SpareBearingRequiredTicks + " settlement ticks",
                    model.SpareBearingElapsedTicks,
                    model.SpareBearingRequiredTicks);
            }

            if (model.IsSpareBearingBarterAvailable)
            {
                return Create(
                    LastBearingPermitJobChapter.Barter,
                    8,
                    "CHAPTER VIII · THE CLAIMS WICKET",
                    "Trade the thing, not a number",
                    "One tagged spare-bearing lot sits in workshop output custody. " +
                    "Barter it once for the fixed depot-corridor permit.",
                    "Barter the lot for the route permit.",
                    phaseProgressCurrent: 0,
                    phaseProgressTarget: 1);
            }

            if (model.MaintenanceDue)
            {
                return Create(
                    LastBearingPermitJobChapter.Homecoming,
                    JobStepCount,
                    "ALTERNATE CONSEQUENCE · THE PROMISE COMES DUE",
                    "Service the field sleeve",
                    "The cooperative repair kept the water moving, and its " +
                    "two-part maintenance promise is now due.",
                    "Service the field sleeve.");
            }

            return PresentAlternateConclusion(model);
        }

        private static LastBearingPermitJobPresentation PresentAlternateConclusion(
            LastBearingReadModel model)
        {
            if (model.TurbineCondition == TurbineCondition.SleeveRepaired
                && model.InstalledCityImprovement
                    == CityImprovementKind.RefurbishedAuxiliaryPump)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · TWO CIVIC REPAIRS",
                    "The pump turns. The promise remains.",
                    "The recovered rotor now drives the auxiliary pump, while " +
                    "the cooperative field sleeve keeps the turbine moving. " +
                    "Its maintenance promise remains part of Last Bearing.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.TurbineCondition == TurbineCondition.SleeveRepaired
                && model.NextCityDecision
                    == NextCityDecision.ExpandEmergencyCistern)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · CISTERN PROMISE",
                    "The cistern waits. The promise remains.",
                    "The range-tank return leaves emergency-cistern expansion " +
                    "open, while the cooperative field sleeve carries its " +
                    "maintenance promise forward.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.TurbineCondition == TurbineCondition.SleeveRepaired
                && model.NextCityDecision
                    == NextCityDecision.RestoreDepotAccess)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · ACCESS PROMISE",
                    "Depot access waits. The promise remains.",
                    "The range-tank return leaves restored depot access open, " +
                    "while the cooperative field sleeve carries its maintenance " +
                    "promise forward.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.TurbineCondition == TurbineCondition.SleeveRepaired)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · A SHARED REPAIR",
                    "A promise came home, not a permit",
                    "The field sleeve restored water and preserved cooperation. " +
                    "Its maintenance obligation is the lasting cost of this run.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.InstalledCityImprovement
                == CityImprovementKind.RefurbishedAuxiliaryPump)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · TWO PUMPS TURNING",
                    "The recovered rotor joined the city",
                    "Workshop Push converted the Wreck Line recovery into a " +
                    "permanent auxiliary-pump improvement.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.NextCityDecision == NextCityDecision.ExpandEmergencyCistern)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · RANGE-TANK RETURN",
                    "The cistern question remains open",
                    "Workshop Push + Range Tank returned a different resource " +
                    "story. Emergency-cistern expansion is outside this V0.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.NextCityDecision == NextCityDecision.RestoreDepotAccess)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · RANGE-TANK RETURN",
                    "Depot access needs another answer",
                    "Civic Buffer + Range Tank preserved a different return " +
                    "capacity. Restoring depot access is outside this V0.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            return Create(
                LastBearingPermitJobChapter.AlternateConclusion,
                JobStepCount,
                "ALTERNATE CONCLUSION · WATER RECOVERING",
                "Last Bearing survived this route",
                "The selected plan closed without the One Good Batch permit " +
                "continuation in the current V0.",
                "This branch is complete for the current V0.",
                isAlternateConclusion: true,
                recommendedFirstRunCue: ReplayCue);
        }

        private static bool IsPermitJobFinale(LastBearingReadModel model)
        {
            return model.RoutePermitGranted
                && model.SpareBearingBatchPhase == SpareBearingBatchPhase.Settled
                && model.SpareBearingLotQuantity
                    == LastBearingBalanceV1.SpareBearingBatchOutputQuantity
                && model.SpareBearingLotCustody
                    == SpareBearingLotCustody.LastBearingClaimsCounter;
        }

        private static string DepotChoiceCue(LastBearingReadModel model)
        {
            if (model.PreparationChoice == PreparationChoice.CivicBuffer
                && model.VehicleModule == VehicleModule.WinchAssembly)
            {
                return RecommendedDepotCue;
            }

            if (model.PreparationChoice == PreparationChoice.WorkshopPush
                && model.VehicleModule == VehicleModule.WinchAssembly)
            {
                return "THIS RUN · The recovered rotor can become an auxiliary " +
                       "pump. TAKE carries a grievance; COOPERATE adds the " +
                       "field-sleeve maintenance promise.";
            }

            if (model.PreparationChoice == PreparationChoice.WorkshopPush)
            {
                return "THIS RUN · The range-tank return closes on the emergency-" +
                       "cistern question. TAKE carries a grievance; COOPERATE " +
                       "adds the field-sleeve maintenance promise.";
            }

            return "THIS RUN · The range-tank return closes on restoring depot " +
                   "access. TAKE carries a grievance; COOPERATE adds the " +
                   "field-sleeve maintenance promise.";
        }

        private static string FormatPlan(LastBearingReadModel model)
        {
            return FormatPreparation(model.PreparationChoice) + " + " +
                   FormatModule(model.PlannedModule);
        }

        private static string FormatPreparation(PreparationChoice choice)
        {
            return choice == PreparationChoice.CivicBuffer
                ? "Civic Buffer"
                : "Workshop Push";
        }

        private static string FormatModule(VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly
                ? "Winch"
                : "Range Tank";
        }

        private static string FormatRouteProgress(LastBearingReadModel model)
        {
            return model.RouteProgressTicks + " / " +
                   model.RouteTargetTicks + " route ticks";
        }

        private static LastBearingPermitJobPresentation Create(
            LastBearingPermitJobChapter chapter,
            int stepIndex,
            string chapterLabel,
            string headline,
            string detail,
            string progressLabel,
            long phaseProgressCurrent = 0,
            long phaseProgressTarget = 0,
            bool phaseProgressIndeterminate = false,
            bool isFinale = false,
            bool isAlternateConclusion = false,
            string recommendedFirstRunCue = NoCue)
        {
            return new LastBearingPermitJobPresentation(
                chapter,
                stepIndex,
                JobStepCount,
                chapterLabel,
                headline,
                detail,
                progressLabel,
                phaseProgressCurrent,
                phaseProgressTarget,
                phaseProgressIndeterminate,
                isFinale,
                isAlternateConclusion,
                recommendedFirstRunCue);
        }
    }
}
