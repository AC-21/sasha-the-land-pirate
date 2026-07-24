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
            "RECOMMENDED FIRST RUN · Choose CIVIC BUFFER + WINCH. " +
            "Nothing is auto-selected; this path opens the complete " +
            "manufacturing-and-permit continuation.";
        private const string RecommendedDepotCue =
            "FIRST RUN · TAKE THE CERAMIC BEARING opens One Good Batch; " +
            "COOPERATE closes on the maintenance promise.";
        private const string ReplayCue =
            "REPLAY CUE · Choose CIVIC BUFFER + WINCH, then TAKE THE " +
            "CERAMIC BEARING to reach manufacturing and barter.";
        private const string PlaceRecyclerObjective = "place-city-recycler";
        private const string PlaceMachineShopObjective =
            "place-city-machine-shop";
        private const string PlaceEmergencyStorageObjective =
            "place-city-emergency-storage";
        private const string ConnectServiceLinkObjective =
            "connect-city-service-link";
        private const string StaffServiceCellObjective =
            "staff-city-service-cell";
        private const string AdvanceServiceSledObjective =
            "advance-city-service-sled";

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
                    "Click a colony composition, or click LOAD LAST-BEARING-DEV-V1 " +
                    "to continue the fixed profile.");
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
                    "Click Assign Default Expedition Lead; the resident enters " +
                    "the manifest without changing colony mechanics.");
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
                    "Click Inspect Failing Water System; the stopped turbine " +
                    "becomes the active city need.");
            }

            LastBearingPermitJobPresentation? serviceCell =
                PresentServiceCellObjective(model);
            if (serviceCell != null)
            {
                return serviceCell;
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
                    "Use the trial buttons to place, connect, deliver, and record " +
                    "one path; then click BRING THE SAME RECYCLER + MACHINE SHOP ONLINE.");
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
                    "Click CIVIC BUFFER or WORKSHOP PUSH, then click one rig " +
                    "module in the garage; that pair starts the preparation clock.",
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
                    model.PreparationRemainingTicks +
                    " remaining · keep the settlement unpaused to advance",
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
                    "Click COMMIT MANIFEST + DEPART; the exact manifest is " +
                    "debited and chase driving begins.",
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
                            ? "Press E / gamepad south; the winch moves the pump " +
                              "rotor onto Sasha's scout and reopens the route."
                            : "Press E / gamepad south; the sealed scout crosses " +
                              "the dust exposure without the pump rotor.",
                        model.RouteProgressTicks,
                        model.RouteTargetTicks);
                }

                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    return Create(
                        LastBearingPermitJobChapter.Outbound,
                        3,
                        "CHAPTER III · THE WRECK LINE",
                        "Strip the rails while the road is stopped",
                        "The Patchwork Skid Plate lets Sasha belly under the wreck for one fixed frame-rail bundle. It uses one ordinary cargo slot and becomes four reclaimed parts only after home check-in.",
                        "E — Recover frame rails · +4 reclaimed parts at home",
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
                        "Press E / gamepad south; seat the recovery bridle and " +
                        "open the depot decision.",
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
                    "Hold W / right trigger to advance; steer with A/D / left " +
                    "stick · " + FormatRouteProgress(model),
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
                        "Click COOPERATE for the field sleeve and obligation, or " +
                        "click TAKE for the ceramic bearing and grievance.",
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
                        "Load the repair cargo with E or gamepad south. This " +
                        "moves it into Sasha's vehicle custody.");
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
                        "Click LOAD WATER or LOAD FUEL; that liquid becomes part " +
                        "of the return payload.");
                }

                return Create(
                    LastBearingPermitJobChapter.Depot,
                    4,
                    "CHAPTER IV · THE RETURN LOAD",
                    "Seal the consequences into the manifest",
                    cargoDetail +
                    " Freeze the exact payload before turning toward home.",
                    "Click FREEZE PAYLOAD + RETURN; lock the exact cargo and " +
                    "consequences, then begin the road home.");
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
                    "Hold W / right trigger to drive home; steer with A/D / " +
                    "left stick · " + FormatRouteProgress(model),
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
                    "Press E / gamepad south at the fixed return apron; credit " +
                    "the cargo to Last Bearing and open the repair route.");
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
                    "Click OPEN PUMP HALL REPAIR LINE, then press E / gamepad " +
                    "south; install the carried repair and reverse the water loss.");
            }

            if (model.IsCityImprovementInstallationAvailable &&
                model.NextCityDecision ==
                    NextCityDecision.RefurbishAuxiliaryPump)
            {
                return Create(
                    LastBearingPermitJobChapter.Homecoming,
                    6,
                    "ALTERNATE HOMEWORK · THE PUMP HALL",
                    "Seat the recovered rotor",
                    "Workshop Push + Winch returned the physical pump rotor. " +
                    "Install it at the fixed auxiliary-pump socket to close this branch.",
                    "Open the pump hall, release the controls, then press E / " +
                    "gamepad south; the returned rotor becomes permanent civic infrastructure.");
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
                    "Click OPEN MACHINE SHOP, then press E / gamepad south; " +
                    "commit two parts and start the batch clock.",
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
                    model.SpareBearingRequiredTicks + " settlement ticks · " +
                    "keep the settlement unpaused; completion moves one " +
                    "physical lot to workshop output",
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
                    "Click OPEN CLAIMS WICKET, then press E / gamepad south; " +
                    "exchange the physical lot for the route permit.",
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
                    "Open the pump hall, release the route input, then operate " +
                    "the physical field-sleeve control with E or gamepad south.");
            }

            return PresentAlternateConclusion(model);
        }

        private static LastBearingPermitJobPresentation PresentAlternateConclusion(
            LastBearingReadModel model)
        {
            if (model.TurbineCondition == TurbineCondition.SleeveRepaired
                && model.InstalledCityImprovement
                    == CityImprovementKind.ExpandedEmergencyCistern)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · SADDLE TANKS AND A PROMISE",
                    "The cistern grew. The promise remains.",
                    "The returned range tank now expands Emergency Storage to " +
                    "210.000 water capacity, while the cooperative field sleeve " +
                    "keeps the turbine moving and carries its maintenance promise.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

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

            if (model.InstalledCityImprovement
                == CityImprovementKind.ExpandedEmergencyCistern)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE CONCLUSION · EMERGENCY STORAGE EXPANDED",
                    "The returned range tank joined the city",
                    "Workshop Push converted the sealed Water return into visible " +
                    "saddle tanks and a permanent 210.000 water ceiling.",
                    "This branch is complete for the current V0.",
                    isAlternateConclusion: true,
                    recommendedFirstRunCue: ReplayCue);
            }

            if (model.NextCityDecision == NextCityDecision.ExpandEmergencyCistern)
            {
                return Create(
                    LastBearingPermitJobChapter.AlternateConclusion,
                    JobStepCount,
                    "ALTERNATE WORK ORDER · RANGE-TANK RETURN",
                    "Expand Emergency Storage",
                    "Workshop Push + Range Tank returned one sealed Water load. " +
                    "Route to the physical expansion handwheel and commit two parts.",
                    "Open Emergency Storage, release the control, then use E, " +
                    "gamepad south, or the exact pointer target.",
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

        private static LastBearingPermitJobPresentation?
            PresentServiceCellObjective(LastBearingReadModel model)
        {
            switch (model.NextObjective)
            {
                case PlaceRecyclerObjective:
                    return Create(
                        LastBearingPermitJobChapter.CityCrisis,
                        1,
                        "CHAPTER I · THE WORKING CELL",
                        "Place the recycler",
                        "Preview the recycler across five authored pads. Moving, " +
                        "quarter-turning, and canceling the preview are free; " +
                        "acceptance spends 2 reclaimed parts while the water " +
                        "reserve keeps falling.",
                        "In the city Field Desk, click SELECT RECYCLER · 2 PARTS; " +
                        "choose its pad and rotation, then click PLACE RECYCLER · 2 PARTS.");
                case PlaceMachineShopObjective:
                    return Create(
                        LastBearingPermitJobChapter.CityCrisis,
                        1,
                        "CHAPTER I · THE WORKING CELL",
                        "Place the machine shop",
                        "The shop receives the service-cell delivery. Preview, " +
                        "move, rotate, or cancel for free before accepting its " +
                        "3 reclaimed-part cost.",
                        "Click SELECT MACHINE SHOP · 3 PARTS; choose its pad and " +
                        "rotation, then click PLACE MACHINE SHOP · 3 PARTS.");
                case PlaceEmergencyStorageObjective:
                    return Create(
                        LastBearingPermitJobChapter.CityCrisis,
                        1,
                        "CHAPTER I · THE WORKING CELL",
                        "Place emergency storage",
                        "Storage completes the three-building cell. Preview, " +
                        "move, rotate, or cancel for free before accepting its " +
                        "1 reclaimed-part cost.",
                        "Click SELECT EMERGENCY STORAGE · 1 PART; choose its pad " +
                        "and rotation, then click PLACE EMERGENCY STORAGE · 1 PART.");
                case ConnectServiceLinkObjective:
                    return Create(
                        LastBearingPermitJobChapter.CityCrisis,
                        1,
                        "CHAPTER I · THE WORKING CELL",
                        "Lock the service link",
                        "All three buildings may still move for free. Locking " +
                        "the link spends 1 reclaimed part and permanently fixes " +
                        "every pad and orientation; V0 has no demolition or refund.",
                        "Reposition any building now, or click LOCK SERVICE LINK · " +
                        "1 PART to make the layout permanent.");
                case StaffServiceCellObjective:
                    return Create(
                        LastBearingPermitJobChapter.CityCrisis,
                        1,
                        "CHAPTER I · THE WORKING CELL",
                        "Staff the machine-shop slot",
                        "Assign one eligible resident already in this colony. " +
                        "Human and utility-robot operators are mechanically " +
                        "neutral here; neither receives a V0 bonus.",
                        FormatStaffingControl(model.Composition));
                case AdvanceServiceSledObjective:
                    return model.CityDeliveryStage == CityDeliveryStage.AtRecycler
                        ? Create(
                            LastBearingPermitJobChapter.CityCrisis,
                            1,
                            "CHAPTER I · THE WORKING CELL",
                            "Send the calibration sled",
                            "The linked cell has an operator. The first of two " +
                            "explicit advances moves the sled into transit and " +
                            "returns no parts yet.",
                            "Click ADVANCE PARTS SLED; the sled moves from the " +
                            "recycler onto the permanent service link.")
                        : Create(
                            LastBearingPermitJobChapter.CityCrisis,
                            1,
                            "CHAPTER I · THE WORKING CELL",
                            "Complete the commissioning delivery",
                            "The second advance completes commissioning and " +
                            "returns exactly 2 reclaimed parts once.",
                            "Click COMMISSIONING DELIVERY · ONCE; the completed working " +
                            "cell then hands control to expedition preparation.");
                default:
                    return null;
            }
        }

        private static string FormatStaffingControl(
            ColonyComposition composition)
        {
            switch (composition)
            {
                case ColonyComposition.HumanOnly:
                    return "Click STAFF HUMAN · NEUTRAL; fill the one operator " +
                           "slot without a composition bonus.";
                case ColonyComposition.RobotOnly:
                    return "Click STAFF UTILITY ROBOT · NEUTRAL; fill the one " +
                           "operator slot without a composition bonus.";
                default:
                    return "Click STAFF HUMAN · NEUTRAL or STAFF UTILITY ROBOT · " +
                           "NEUTRAL; either fills the same one operator slot.";
            }
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
