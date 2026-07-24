#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum LastBearingFieldDeskIntent
    {
        None = 0,
        AssignDefaultLead = 1,
        InspectCityNeed = 2,
        SelectRecycler = 3,
        SelectMachineShop = 4,
        SelectEmergencyStorage = 5,
        RotateCityBuilding = 6,
        PreviousCityPad = 7,
        NextCityPad = 8,
        PlaceCityBuilding = 9,
        ConnectCityServiceLink = 10,
        StaffCityServiceHuman = 11,
        StaffCityServiceRobot = 12,
        AdvanceCityServiceSled = 13,
        CancelCityBuildingPreview = 14,
        // Legacy compatibility value. The working service-cell UI never emits it.
        ActivateInfrastructure = 15,
        BeginWorkshopPush = 16,
        BeginCivicBuffer = 17,
        OpenGarage = 18,
        CommitExpedition = 19,
        OpenPumpHallRepair = 20,
        OpenOneGoodBatchWorkshop = 21,
        OpenPumpHallImprovement = 22,
        ServiceFieldSleeve = 23,
        TogglePause = 24,
        Save = 25,
        Load = 26,
        ReturnToTitle = 27,
        RunHotShift = 28,
        AcknowledgeDustFront = 29,
        OpenEmergencyCisternPump = 30,
        OpenDustFrontRelay = 31,
    }

    public enum LastBearingFieldDeskActionTone
    {
        None = 0,
        Primary = 1,
        Signal = 2,
        Hazard = 3,
        Quiet = 4,
    }

    public readonly struct LastBearingFieldDeskActionProjection
    {
        internal LastBearingFieldDeskActionProjection(
            LastBearingFieldDeskIntent intent,
            string label,
            string detail,
            bool visible,
            bool enabled,
            LastBearingFieldDeskActionTone tone)
        {
            Intent = intent;
            Label = label;
            Detail = detail;
            IsVisible = visible;
            IsEnabled = enabled;
            Tone = tone;
        }

        public LastBearingFieldDeskIntent Intent { get; }

        public string Label { get; }

        public string Detail { get; }

        public bool IsVisible { get; }

        public bool IsEnabled { get; }

        public LastBearingFieldDeskActionTone Tone { get; }
    }

    public sealed class LastBearingFieldDeskSurveyProjection
    {
        internal LastBearingFieldDeskSurveyProjection(
            bool visible,
            string hypothesisLabel,
            string evidence,
            LastBearingFieldDeskActionProjection selectRecycler,
            LastBearingFieldDeskActionProjection selectMachineShop,
            LastBearingFieldDeskActionProjection selectEmergencyStorage,
            LastBearingFieldDeskActionProjection rotate,
            LastBearingFieldDeskActionProjection previousPad,
            LastBearingFieldDeskActionProjection nextPad,
            LastBearingFieldDeskActionProjection place,
            LastBearingFieldDeskActionProjection connectLink,
            LastBearingFieldDeskActionProjection staffHuman,
            LastBearingFieldDeskActionProjection staffRobot,
            LastBearingFieldDeskActionProjection advanceSled,
            LastBearingFieldDeskActionProjection cancelPreview)
        {
            IsVisible = visible;
            HypothesisLabel = hypothesisLabel;
            Evidence = evidence;
            SelectRecycler = selectRecycler;
            SelectMachineShop = selectMachineShop;
            SelectEmergencyStorage = selectEmergencyStorage;
            Rotate = rotate;
            PreviousPad = previousPad;
            NextPad = nextPad;
            Place = place;
            ConnectLink = connectLink;
            StaffHuman = staffHuman;
            StaffRobot = staffRobot;
            AdvanceSled = advanceSled;
            CancelPreview = cancelPreview;
        }

        public bool IsVisible { get; }

        public string HypothesisLabel { get; }

        public string Evidence { get; }

        public LastBearingFieldDeskActionProjection SelectRecycler { get; }

        public LastBearingFieldDeskActionProjection SelectMachineShop { get; }

        public LastBearingFieldDeskActionProjection SelectEmergencyStorage { get; }

        public LastBearingFieldDeskActionProjection Rotate { get; }

        public LastBearingFieldDeskActionProjection PreviousPad { get; }

        public LastBearingFieldDeskActionProjection NextPad { get; }

        public LastBearingFieldDeskActionProjection Place { get; }

        public LastBearingFieldDeskActionProjection ConnectLink { get; }

        public LastBearingFieldDeskActionProjection StaffHuman { get; }

        public LastBearingFieldDeskActionProjection StaffRobot { get; }

        public LastBearingFieldDeskActionProjection AdvanceSled { get; }

        public LastBearingFieldDeskActionProjection CancelPreview { get; }
    }

    public readonly struct LastBearingDryLineProjection
    {
        internal LastBearingDryLineProjection(
            long frontTicks,
            long dryLineMilli,
            long projectedWaterMilli,
            DustFrontOutcome projectedOutcome,
            bool approaching,
            string forecast,
            string telltale)
        {
            FrontTicks = frontTicks;
            DryLineMilli = dryLineMilli;
            ProjectedWaterMilli = projectedWaterMilli;
            ProjectedOutcome = projectedOutcome;
            IsApproaching = approaching;
            Forecast = forecast;
            Telltale = telltale;
        }

        public long FrontTicks { get; }

        public long DryLineMilli { get; }

        public long ProjectedWaterMilli { get; }

        public DustFrontOutcome ProjectedOutcome { get; }

        public bool IsApproaching { get; }

        public string Forecast { get; }

        public string Telltale { get; }
    }

    public sealed class LastBearingFieldDeskProjection
    {
        internal LastBearingFieldDeskProjection(
            string composition,
            string pauseState,
            string waterAmount,
            string waterTrend,
            string parts,
            string fuel,
            string turbine,
            string pressure,
            LastBearingDryLineProjection dryLine,
            LastBearingPermitJobPresentation permitJob,
            LastBearingFieldDeskActionProjection primaryAction,
            LastBearingFieldDeskActionProjection secondaryAction,
            LastBearingFieldDeskSurveyProjection survey,
            LastBearingFieldDeskActionProjection pauseAction,
            LastBearingFieldDeskActionProjection saveAction,
            LastBearingFieldDeskActionProjection loadAction,
            LastBearingFieldDeskActionProjection titleAction,
            string controllerStatus,
            string saveStatus)
        {
            Composition = composition;
            PauseState = pauseState;
            WaterAmount = waterAmount;
            WaterTrend = waterTrend;
            Parts = parts;
            Fuel = fuel;
            Turbine = turbine;
            Pressure = pressure;
            DryLine = dryLine;
            PermitJob = permitJob;
            PrimaryAction = primaryAction;
            SecondaryAction = secondaryAction;
            Survey = survey;
            PauseAction = pauseAction;
            SaveAction = saveAction;
            LoadAction = loadAction;
            TitleAction = titleAction;
            ControllerStatus = controllerStatus;
            SaveStatus = saveStatus;
        }

        public string Composition { get; }

        public string PauseState { get; }

        public string WaterAmount { get; }

        public string WaterTrend { get; }

        public string Parts { get; }

        public string Fuel { get; }

        public string Turbine { get; }

        public string Pressure { get; }

        public LastBearingDryLineProjection DryLine { get; }

        public LastBearingPermitJobPresentation PermitJob { get; }

        public LastBearingFieldDeskActionProjection PrimaryAction { get; }

        public LastBearingFieldDeskActionProjection SecondaryAction { get; }

        public LastBearingFieldDeskSurveyProjection Survey { get; }

        public LastBearingFieldDeskActionProjection PauseAction { get; }

        public LastBearingFieldDeskActionProjection SaveAction { get; }

        public LastBearingFieldDeskActionProjection LoadAction { get; }

        public LastBearingFieldDeskActionProjection TitleAction { get; }

        public string ControllerStatus { get; }

        public string SaveStatus { get; }
    }

    internal readonly struct LastBearingFieldDeskStamp
    {
        internal LastBearingFieldDeskStamp(ulong value)
        {
            Value = value;
        }

        internal ulong Value { get; }
    }

    /// <summary>
    /// Presentation-only projection for the retained city desk. It reads the
    /// controller and read model, but owns no game rule and performs no action.
    /// </summary>
    public static class LastBearingFieldDeskPresenter
    {
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
        private const string SelectPreparationObjective =
            "select-preparation-and-module";
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static LastBearingFieldDeskProjection Present(
            LastBearingGameController controller)
        {
            LastBearingReadModel? model = controller.RuntimeReadModel;
            LastBearingPermitJobPresentation permitJob =
                LastBearingPermitJobPresenter.Present(
                    model,
                    controller.CityNeedInspected);

            if (model == null)
            {
                LastBearingFieldDeskActionProjection unavailable = Hidden();
                return new LastBearingFieldDeskProjection(
                    "NO ACTIVE ROSTER",
                    "OFFLINE",
                    "--",
                    "--",
                    "--",
                    "--",
                    "--",
                    "CITY DESK STOWED",
                    new LastBearingDryLineProjection(
                        0,
                        LastBearingBalanceV1.MinimumRecoverableWaterMilli,
                        0,
                        DustFrontOutcome.Unresolved,
                        false,
                        "CURRENT-DRAW FORECAST OFFLINE",
                        "FRONT OFFLINE"),
                    permitJob,
                    unavailable,
                    unavailable,
                    CreateSurvey(controller, null, false, false, false),
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    controller.Status,
                    controller.SaveStatus);
            }

            bool canDispatch = controller.IsExactFieldDeskCityOverview &&
                               !controller.HasPendingPlayerCommands;
            DeriveCurrentOrder(
                controller,
                model,
                canDispatch,
                out LastBearingFieldDeskActionProjection primary,
                out LastBearingFieldDeskActionProjection secondary);

            return new LastBearingFieldDeskProjection(
                FormatComposition(model.Composition),
                FormatPause(model.PauseCause),
                FormatWater(model.WaterMilli),
                FormatTrend(model.WaterTrendMilliPerSettlementTick),
                model.PartsUnits + " UNITS",
                model.FuelUnits + " UNITS",
                FormatTurbine(model.TurbineCondition),
                FormatPressure(model),
                ProjectDryLine(model),
                permitJob,
                primary,
                secondary,
                CreateSurvey(
                    controller,
                    model,
                    !model.IsDustFrontAcknowledgementRequired &&
                    controller.CityNeedInspected &&
                    IsServiceCellObjective(model.NextObjective),
                    !model.IsDustFrontAcknowledgementRequired &&
                    primary.Intent != LastBearingFieldDeskIntent.RunHotShift &&
                    secondary.Intent != LastBearingFieldDeskIntent.RunHotShift,
                    !model.IsDustFrontAcknowledgementRequired &&
                    controller.CityNeedInspected),
                Action(
                    LastBearingFieldDeskIntent.TogglePause,
                    model.PauseCause == PauseCause.None ? "PAUSE" : "RESUME",
                    model.PauseCause == PauseCause.AutoAlert
                        ? "The depot alert must be resolved in place."
                        : model.PauseCause == PauseCause.DustFrontAlert
                            ? "Acknowledge the Dust Front verdict before resuming."
                            : "Hold or resume the settlement clocks.",
                    true,
                    canDispatch &&
                    model.PauseCause != PauseCause.AutoAlert &&
                    model.PauseCause != PauseCause.DustFrontAlert,
                    LastBearingFieldDeskActionTone.Quiet),
                Action(
                    LastBearingFieldDeskIntent.Save,
                    "SAVE",
                    "Write the fixed local development profile.",
                    true,
                    canDispatch && model.AssignedResidentId != null,
                    LastBearingFieldDeskActionTone.Quiet),
                Action(
                    LastBearingFieldDeskIntent.Load,
                    "LOAD",
                    "Restore the fixed local development profile.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Quiet),
                Action(
                    LastBearingFieldDeskIntent.ReturnToTitle,
                    "TITLE",
                    "Leave this run and return to composition choice.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Quiet),
                controller.Status,
                controller.SaveStatus);
        }

        public static bool IsIntentAvailable(
            LastBearingGameController controller,
            LastBearingFieldDeskIntent intent)
        {
            if (intent == LastBearingFieldDeskIntent.None ||
                !controller.IsExactFieldDeskCityOverview ||
                controller.HasPendingPlayerCommands)
            {
                return false;
            }

            LastBearingFieldDeskProjection projection = Present(controller);
            return Matches(projection.PrimaryAction, intent) ||
                   Matches(projection.SecondaryAction, intent) ||
                   Matches(projection.PauseAction, intent) ||
                   Matches(projection.SaveAction, intent) ||
                   Matches(projection.LoadAction, intent) ||
                   Matches(projection.TitleAction, intent) ||
                   Matches(projection.Survey.SelectRecycler, intent) ||
                   Matches(projection.Survey.SelectMachineShop, intent) ||
                   Matches(projection.Survey.SelectEmergencyStorage, intent) ||
                   Matches(projection.Survey.Rotate, intent) ||
                   Matches(projection.Survey.PreviousPad, intent) ||
                   Matches(projection.Survey.NextPad, intent) ||
                   Matches(projection.Survey.Place, intent) ||
                   Matches(projection.Survey.ConnectLink, intent) ||
                   Matches(projection.Survey.StaffHuman, intent) ||
                   Matches(projection.Survey.StaffRobot, intent) ||
                   Matches(projection.Survey.AdvanceSled, intent) ||
                   Matches(projection.Survey.CancelPreview, intent);
        }

        internal static LastBearingFieldDeskStamp CaptureStamp(
            LastBearingGameController controller)
        {
            ulong hash = OffsetBasis;
            Mix(ref hash, controller.IsExactFieldDeskCityOverview);
            Mix(ref hash, controller.HasPendingPlayerCommands);
            Mix(ref hash, controller.CityNeedInspected);
            Mix(ref hash, controller.IsTurbineRepairReady);
            Mix(ref hash, controller.GaragePreparationIntent.GetHashCode());
            Mix(ref hash, controller.CityPreviewBuilding.GetHashCode());
            Mix(ref hash, controller.CityPreviewPadIndex);
            Mix(ref hash, controller.CityPreviewQuarterTurns);
            Mix(ref hash, controller.CityGrammarHypothesis.GetHashCode());
            Mix(ref hash, controller.CityGrammarTrialReady);
            Mix(ref hash, controller.HasCompletedCityGrammarObservation);
            Mix(ref hash, controller.ActiveCityGrammarPiece.GetHashCode());
            Mix(ref hash, controller.CityGrammarDeliveryStage.GetHashCode());
            Mix(ref hash, controller.CityGrammarPathRead.GetHashCode());
            Mix(ref hash, controller.CityGrammarLogisticsConnected);
            Mix(ref hash, controller.CityGrammarInteractionCount);
            Mix(ref hash, controller.Status);
            Mix(ref hash, controller.SaveStatus);

            LastBearingReadModel? model = controller.RuntimeReadModel;
            if (model == null)
            {
                Mix(ref hash, -1);
                return new LastBearingFieldDeskStamp(hash);
            }

            Mix(ref hash, model.Composition.GetHashCode());
            Mix(ref hash, model.AssignedResidentId);
            Mix(ref hash, model.RecyclerPadIndex);
            Mix(ref hash, model.RecyclerQuarterTurns);
            Mix(ref hash, model.MachineShopPadIndex);
            Mix(ref hash, model.MachineShopQuarterTurns);
            Mix(ref hash, model.EmergencyStoragePadIndex);
            Mix(ref hash, model.EmergencyStorageQuarterTurns);
            Mix(ref hash, model.CityServiceLinkConnected);
            Mix(ref hash, model.CityServiceResidentId);
            Mix(ref hash, model.CityDeliveryStage.GetHashCode());
            Mix(ref hash, model.CityDeliveryCount);
            Mix(ref hash, model.SliceInfrastructureActive);
            Mix(ref hash, model.HotShiftPhase.GetHashCode());
            Mix(ref hash, model.HotShiftElapsedTicks);
            Mix(ref hash, model.HotShiftRequiredTicks);
            Mix(ref hash, model.HotShiftCompletedCount);
            Mix(ref hash, model.IsHotShiftRunAvailable);
            Mix(ref hash, model.IsHotShiftStalledByWorkshopPush);
            Mix(ref hash, model.IsHotShiftStalledByDustFront);
            Mix(ref hash, model.IsHotShiftActivelyWorking);
            Mix(ref hash, model.EmergencyCisternCharged);
            Mix(ref hash, model.IsEmergencyCisternPumpAvailable);
            Mix(ref hash, model.WaterMilli);
            Mix(ref hash, model.WaterTrendMilliPerSettlementTick);
            Mix(ref hash, model.PartsUnits);
            Mix(ref hash, model.FuelUnits);
            Mix(ref hash, model.TurbineCondition.GetHashCode());
            Mix(ref hash, model.PreparationChoice.GetHashCode());
            Mix(ref hash, model.PreparationPhase.GetHashCode());
            Mix(ref hash, model.PreparationElapsedTicks);
            Mix(ref hash, model.PreparationRequiredTicks);
            Mix(ref hash, model.PlannedModule.GetHashCode());
            Mix(ref hash, model.VehicleModule.GetHashCode());
            Mix(ref hash, model.ExpeditionPhase.GetHashCode());
            Mix(ref hash, model.TransactionPhase.GetHashCode());
            Mix(ref hash, model.RepairCargoKind.GetHashCode());
            Mix(ref hash, model.RepairCargoCustody.GetHashCode());
            Mix(ref hash, model.MaintenanceDue);
            Mix(ref hash, model.NextCityDecision.GetHashCode());
            Mix(ref hash, model.InstalledCityImprovement.GetHashCode());
            Mix(ref hash, model.IsCityImprovementInstallationAvailable);
            Mix(ref hash, model.SpareBearingBatchPhase.GetHashCode());
            Mix(ref hash, model.SpareBearingElapsedTicks);
            Mix(ref hash, model.SpareBearingRequiredTicks);
            Mix(ref hash, model.SpareBearingLotQuantity);
            Mix(ref hash, model.SpareBearingLotCustody.GetHashCode());
            Mix(ref hash, model.RoutePermitGranted);
            Mix(ref hash, model.FutureRouteTollFuelUnits);
            Mix(ref hash, model.IsSpareBearingBatchStartAvailable);
            Mix(ref hash, model.IsSpareBearingBarterAvailable);
            Mix(ref hash, model.PauseCause.GetHashCode());
            Mix(ref hash, model.DustFrontOutcome.GetHashCode());
            Mix(ref hash, model.IsDustFrontAcknowledgementRequired);
            Mix(ref hash, model.DustFrontCrisisTicks);
            Mix(ref hash, model.NextObjective);
            return new LastBearingFieldDeskStamp(hash);
        }

        public static LastBearingDryLineProjection ProjectDryLine(
            LastBearingReadModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            const long dryLine =
                LastBearingBalanceV1.MinimumRecoverableWaterMilli;
            if (model.DustFrontOutcome != DustFrontOutcome.Unresolved)
            {
                return new LastBearingDryLineProjection(
                    0,
                    dryLine,
                    ClampWater(model.WaterMilli),
                    model.DustFrontOutcome,
                    false,
                    FormatPressure(model),
                    model.DustFrontOutcome == DustFrontOutcome.Held
                        ? "FRONT HELD"
                        : "FRONT BREACHED");
            }

            long frontTicks = Math.Max(0, model.DustFrontCrisisTicks);
            long projectedWater = ProjectWaterAtConstantDraw(
                model.WaterMilli,
                model.WaterTrendMilliPerSettlementTick,
                frontTicks);
            DustFrontOutcome projectedOutcome =
                model.TurbineCondition != TurbineCondition.Failing ||
                projectedWater > dryLine
                    ? DustFrontOutcome.Held
                    : DustFrontOutcome.Breached;
            string verdict = projectedOutcome == DustFrontOutcome.Held
                ? "HELD"
                : "BREACHED";
            return new LastBearingDryLineProjection(
                frontTicks,
                dryLine,
                projectedWater,
                projectedOutcome,
                true,
                "FRONT IN " + frontTicks +
                " TICKS · DRY LINE " + FormatExactMilli(dryLine) +
                " · PROJECTED " + verdict +
                " IF CURRENT DRAW CONTINUES",
                "FRONT " + frontTicks +
                "\nDRY " + FormatExactMilli(dryLine) +
                " · " + verdict);
        }

        private static void DeriveCurrentOrder(
            LastBearingGameController controller,
            LastBearingReadModel model,
            bool canDispatch,
            out LastBearingFieldDeskActionProjection primary,
            out LastBearingFieldDeskActionProjection secondary)
        {
            secondary = Hidden();
            if (model.IsDustFrontAcknowledgementRequired)
            {
                bool useFallback =
                    controller.CanAcknowledgeDustFrontFallback;
                primary = Action(
                    useFallback
                        ? LastBearingFieldDeskIntent.AcknowledgeDustFront
                        : LastBearingFieldDeskIntent.OpenDustFrontRelay,
                    useFallback
                        ? "ACKNOWLEDGE FRONT · FALLBACK"
                        : "OPEN EMERGENCY STORAGE · FACE DUST FRONT",
                    (model.DustFrontOutcome == DustFrontOutcome.Held
                        ? "HELD · Last Bearing kept the reserve above the recoverable line. Face the physical relay to resume settlement clocks."
                        : "BREACHED · The failing turbine could not hold the dry line. Face the physical relay; Hot Shift stays stalled until turbine repair.") +
                    (useFallback
                        ? " Physical relay unavailable; use the bounded fallback."
                        : string.Empty),
                    true,
                    canDispatch &&
                    (useFallback
                        ? controller.CanAcknowledgeDustFrontFallback
                        : controller.CanOpenDustFrontRelay),
                    LastBearingFieldDeskActionTone.Hazard);
                return;
            }

            if (model.AssignedResidentId == null)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.AssignDefaultLead,
                    "ASSIGN THE ROAD LEAD",
                    "Seat the default resident in the expedition manifest.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            if (!controller.CityNeedInspected)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.InspectCityNeed,
                    "READ THE WATERWORKS",
                    "Inspect the stopped turbine before committing city work.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Hazard);
                return;
            }

            if (IsServiceCellObjective(model.NextObjective))
            {
                DeriveServiceCellOrder(
                    controller,
                    model,
                    canDispatch,
                    out primary,
                    out secondary);
                return;
            }

            if (controller.IsGaragePlanIntentActive &&
                model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.PreparationChoice == PreparationChoice.Unselected)
            {
                string preparationLabel = controller.GaragePreparationIntent ==
                                          PreparationChoice.CivicBuffer
                    ? "Civic Buffer"
                    : "Workshop Push";
                primary = Action(
                    LastBearingFieldDeskIntent.OpenGarage,
                    "RETURN TO SASHA'S GARAGE",
                    preparationLabel +
                    " remains an uncommitted city note until a rig module is chosen.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            if (model.PreparationPhase == PreparationPhase.Unselected &&
                model.NextObjective == SelectPreparationObjective)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.BeginCivicBuffer,
                    "PENCIL CIVIC BUFFER",
                    "Protect the city reserve, then choose a rig module in the garage.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = Action(
                    LastBearingFieldDeskIntent.BeginWorkshopPush,
                    "PENCIL WORKSHOP PUSH",
                    "Spend city slack on faster preparation, then enter the garage.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Signal);
                return;
            }

            if (model.PreparationPhase == PreparationPhase.Preparing)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.OpenGarage,
                    "INSPECT SASHA'S RIG",
                    "Preparation continues on the settlement clock.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = CreateHotShiftAction(
                    controller,
                    model,
                    canDispatch);
                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.TurbineCondition == TurbineCondition.Failing &&
                model.RepairCargoKind == RepairCargoKind.None &&
                (model.PreparationPhase == PreparationPhase.Ready ||
                 model.PreparationPhase == PreparationPhase.Committed))
            {
                primary = Action(
                    LastBearingFieldDeskIntent.OpenGarage,
                    "OPEN GARAGE · PULL LAUNCH DOG",
                    "Return to Sasha's fixed garage and throw the physical departure control.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = Hidden();
                return;
            }

            if (controller.IsTurbineRepairReady)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.OpenPumpHallRepair,
                    "OPEN THE PUMP HALL",
                    "Route the loaded repair to its physical service line.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Hazard);
                return;
            }

            if (model.IsCityImprovementInstallationAvailable)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.OpenPumpHallImprovement,
                    "OPEN PUMP HALL · SEAT AUXILIARY PUMP",
                    "Frame the returned rotor at its physical civic socket, then release and press E or gamepad south.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            if (IsWorkshopRelevant(model))
            {
                primary = Action(
                    LastBearingFieldDeskIntent.OpenOneGoodBatchWorkshop,
                    "OPEN ONE GOOD BATCH",
                    "Handle the active batch or physical lot at its workplace.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Signal);
                return;
            }

            if (model.MaintenanceDue)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.ServiceFieldSleeve,
                    "OPEN PUMP HALL · KEEP THE PROMISE",
                    "Route to the physical field-sleeve control, release the route input, then press E or gamepad south.",
                    true,
                    canDispatch &&
                    controller.CanOpenFieldSleeveService,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            primary = Action(
                LastBearingFieldDeskIntent.OpenGarage,
                "INSPECT SASHA'S RIG",
                "The city desk is waiting on work at a physical station.",
                model.ExpeditionPhase == ExpeditionPhase.AtHome,
                canDispatch && model.ExpeditionPhase == ExpeditionPhase.AtHome,
                LastBearingFieldDeskActionTone.Quiet);
            secondary = CreateHotShiftAction(
                controller,
                model,
                canDispatch);
        }

        private static void DeriveServiceCellOrder(
            LastBearingGameController controller,
            LastBearingReadModel model,
            bool canDispatch,
            out LastBearingFieldDeskActionProjection primary,
            out LastBearingFieldDeskActionProjection secondary)
        {
            secondary = Hidden();
            if (controller.HasCityBuildingPreview)
            {
                primary = CreatePlaceAction(controller, model, canDispatch, true);
                secondary = CreateCancelPreviewAction(
                    controller,
                    canDispatch,
                    true);
                return;
            }

            switch (model.NextObjective)
            {
                case PlaceRecyclerObjective:
                    primary = CreateSelectBuildingAction(
                        model,
                        CityBuildingKind.Recycler,
                        canDispatch,
                        true);
                    return;
                case PlaceMachineShopObjective:
                    primary = CreateSelectBuildingAction(
                        model,
                        CityBuildingKind.MachineShop,
                        canDispatch,
                        true);
                    return;
                case PlaceEmergencyStorageObjective:
                    primary = CreateSelectBuildingAction(
                        model,
                        CityBuildingKind.EmergencyStorage,
                        canDispatch,
                        true);
                    return;
                case ConnectServiceLinkObjective:
                    primary = CreateConnectLinkAction(
                        controller,
                        model,
                        canDispatch,
                        true);
                    return;
                case StaffServiceCellObjective:
                    if (model.Composition == ColonyComposition.RobotOnly)
                    {
                        primary = CreateStaffRobotAction(
                            controller,
                            model,
                            canDispatch,
                            true);
                        return;
                    }

                    primary = CreateStaffHumanAction(
                        controller,
                        model,
                        canDispatch,
                        true);
                    if (model.Composition == ColonyComposition.Mixed)
                    {
                        secondary = CreateStaffRobotAction(
                            controller,
                            model,
                            canDispatch,
                            true);
                    }

                    return;
                case AdvanceServiceSledObjective:
                    primary = CreateAdvanceSledAction(
                        controller,
                        model,
                        canDispatch,
                        true);
                    return;
                default:
                    primary = Hidden();
                    return;
            }
        }

        private static LastBearingFieldDeskSurveyProjection CreateSurvey(
            LastBearingGameController controller,
            LastBearingReadModel? model,
            bool serviceControlsVisible,
            bool allowSupplementalHotShift,
            bool allowEmergencyCistern)
        {
            if (model == null)
            {
                LastBearingFieldDeskActionProjection unavailable = Hidden();
                return new LastBearingFieldDeskSurveyProjection(
                    false,
                    "NO CANONICAL SERVICE CELL",
                    "Construction controls are stowed without an active run.",
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable,
                    unavailable);
            }

            bool canDispatch =
                controller.IsExactFieldDeskCityOverview &&
                !controller.HasPendingPlayerCommands;
            LastBearingFieldDeskActionProjection supplementalCistern =
                allowEmergencyCistern && !serviceControlsVisible
                    ? CreateEmergencyCisternAction(
                        controller,
                        model,
                        canDispatch)
                    : Hidden();
            bool cisternOwnsSharedSlot =
                supplementalCistern.IsVisible &&
                supplementalCistern.IsEnabled;
            LastBearingFieldDeskActionProjection supplementalHotShift =
                allowSupplementalHotShift && !serviceControlsVisible
                && !cisternOwnsSharedSlot
                    ? CreateHotShiftAction(controller, model, canDispatch)
                    : Hidden();
            LastBearingFieldDeskActionProjection supplementalCityWork =
                cisternOwnsSharedSlot
                    ? supplementalCistern
                    : supplementalHotShift.IsVisible
                        ? supplementalHotShift
                        : supplementalCistern;
            bool showSupplementalCityWork =
                supplementalCityWork.IsVisible;
            bool surveyVisible =
                serviceControlsVisible || showSupplementalCityWork;
            bool canUseService = serviceControlsVisible && canDispatch;
            bool hasPreview = controller.HasCityBuildingPreview;
            return new LastBearingFieldDeskSurveyProjection(
                surveyVisible,
                showSupplementalCityWork
                    ? supplementalCityWork.Intent ==
                        LastBearingFieldDeskIntent.OpenEmergencyCisternPump
                        ? "EMERGENCY CISTERN · CITY WORK ORDER"
                        : "HOT SHIFT · CITY WORK ORDER"
                    : FormatServiceCellState(model, controller),
                showSupplementalCityWork
                    ? supplementalCityWork.Detail
                    : "COSTS: RECYCLER 2 · SHOP 3 · STORAGE 1 PART · " +
                      "MOVES FREE BEFORE LINK · " +
                      "LINK LOCKS PERMANENTLY FOR 1 PART · OPERATOR IS NEUTRAL · " +
                      "COMMISSIONING DELIVERY · ONCE · PAYOFF +2 PARTS · ONCE",
                CreateSelectBuildingAction(
                    model,
                    CityBuildingKind.Recycler,
                    canUseService,
                    serviceControlsVisible),
                CreateSelectBuildingAction(
                    model,
                    CityBuildingKind.MachineShop,
                    canUseService,
                    serviceControlsVisible),
                CreateSelectBuildingAction(
                    model,
                    CityBuildingKind.EmergencyStorage,
                    canUseService,
                    serviceControlsVisible),
                Action(
                    LastBearingFieldDeskIntent.RotateCityBuilding,
                    "ROTATE 90°",
                    "Quarter-turn the preview. Rotation is free before link lock.",
                    serviceControlsVisible && hasPreview,
                    canUseService && hasPreview &&
                    !model.CityServiceLinkConnected,
                    LastBearingFieldDeskActionTone.Quiet),
                CreatePadAction(
                    LastBearingFieldDeskIntent.PreviousCityPad,
                    "PREVIOUS PAD",
                    canUseService,
                    serviceControlsVisible && hasPreview,
                    model.CityServiceLinkConnected),
                CreatePadAction(
                    LastBearingFieldDeskIntent.NextCityPad,
                    "NEXT PAD",
                    canUseService,
                    serviceControlsVisible && hasPreview,
                    model.CityServiceLinkConnected),
                CreatePlaceAction(
                    controller,
                    model,
                    canUseService,
                    serviceControlsVisible && hasPreview),
                CreateConnectLinkAction(
                    controller,
                    model,
                    canUseService,
                    serviceControlsVisible),
                CreateStaffHumanAction(
                    controller,
                    model,
                    canUseService,
                    serviceControlsVisible &&
                    model.Composition != ColonyComposition.RobotOnly),
                CreateStaffRobotAction(
                    controller,
                    model,
                    canUseService,
                    serviceControlsVisible &&
                    model.Composition != ColonyComposition.HumanOnly),
                showSupplementalCityWork
                    ? supplementalCityWork
                    : CreateAdvanceSledAction(
                        controller,
                        model,
                        canUseService,
                        serviceControlsVisible),
                CreateCancelPreviewAction(
                    controller,
                    canUseService,
                    serviceControlsVisible && hasPreview));
        }

        private static LastBearingFieldDeskActionProjection
            CreateSelectBuildingAction(
                LastBearingReadModel model,
                CityBuildingKind building,
                bool canUse,
                bool visible)
        {
            int pad = CityBuildingPad(model, building);
            long cost = CityBuildingCost(building);
            bool alreadyPlaced = pad >= 0;
            bool locked = model.CityServiceLinkConnected;
            return Action(
                SelectBuildingIntent(building),
                locked
                    ? CityBuildingLabel(building) + " · LOCKED"
                    : (alreadyPlaced ? "MOVE " : "SELECT ") +
                      CityBuildingLabel(building) +
                      (alreadyPlaced
                          ? " · FREE"
                          : " · " + FormatPartsCost(cost)),
                locked
                    ? "The permanent 1-part service link has locked this pad " +
                      "and orientation; V0 has no demolition or refund."
                    : alreadyPlaced
                    ? CityBuildingLabel(building) + " is on pad " +
                      (pad + 1) +
                      "; click its world selector to reposition free before lock."
                    : "Click its world selector or use this accessibility " +
                      "fallback; preview across five pads before committing " +
                      FormatReclaimedParts(cost) + ".",
                visible,
                canUse && !locked,
                LastBearingFieldDeskActionTone.Signal);
        }

        private static LastBearingFieldDeskActionProjection CreatePadAction(
            LastBearingFieldDeskIntent intent,
            string label,
            bool canUse,
            bool visible,
            bool linkConnected)
        {
            return Action(
                intent,
                label,
                "Keyboard/accessibility fallback: cycle the world ghost across " +
                "five authored pads for free.",
                visible,
                canUse && !linkConnected,
                LastBearingFieldDeskActionTone.Quiet);
        }

        private static LastBearingFieldDeskActionProjection CreatePlaceAction(
            LastBearingGameController controller,
            LastBearingReadModel model,
            bool canUse,
            bool visible)
        {
            CityBuildingKind building = controller.CityPreviewBuilding;
            bool moving = CityBuildingPad(model, building) >= 0;
            long cost = CityBuildingCost(building);
            bool occupied = IsPadOccupiedByOther(
                model,
                building,
                controller.CityPreviewPadIndex);
            bool insufficient = !moving && model.PartsUnits < cost;
            string detail = occupied
                ? "Pad " + (controller.CityPreviewPadIndex + 1) +
                  " is occupied; choose another authored pad."
                : insufficient
                    ? "Needs " + FormatReclaimedParts(cost) + "; only " +
                      FormatReclaimedParts(model.PartsUnits) + " remain."
                    : "Pad " + (controller.CityPreviewPadIndex + 1) +
                      " · " + (controller.CityPreviewQuarterTurns * 90) +
                      "°. " + (moving
                          ? "This move is free until the permanent link lock."
                          : "Acceptance spends " +
                            FormatReclaimedParts(cost) + ".");
            return Action(
                LastBearingFieldDeskIntent.PlaceCityBuilding,
                (moving ? "MOVE " : "PLACE ") +
                CityBuildingLabel(building) +
                (moving ? " · FREE" : " · " + FormatPartsCost(cost)),
                detail,
                visible,
                canUse && controller.CanPlaceCityBuildingPreview,
                LastBearingFieldDeskActionTone.Primary);
        }

        private static LastBearingFieldDeskActionProjection
            CreateConnectLinkAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canUse,
                bool visible)
        {
            bool allPlaced = model.RecyclerPadIndex >= 0 &&
                             model.MachineShopPadIndex >= 0 &&
                             model.EmergencyStoragePadIndex >= 0;
            string detail = !allPlaced
                ? "Place recycler, machine shop, and emergency storage first; " +
                  "then 1 part permanently locks every pad and orientation."
                : model.CityServiceLinkConnected
                    ? "The 1-part service link is permanently locked; layout " +
                      "moves are closed."
                    : model.PartsUnits <
                      LastBearingBalanceV1.CityServiceLinkPartsUnits
                    ? "The permanent link needs 1 reclaimed part; none remain."
                    : "World: click Recycler output, then Machine Shop intake. " +
                      "Permanent: locks all three pads and orientations for 1 " +
                      "part; V0 has no demolition or refund.";
            return Action(
                LastBearingFieldDeskIntent.ConnectCityServiceLink,
                model.CityServiceLinkConnected
                    ? "SERVICE LINK · LOCKED"
                    : "LOCK SERVICE LINK · 1 PART",
                detail,
                visible,
                canUse && controller.CanConnectCityServiceLink,
                LastBearingFieldDeskActionTone.Hazard);
        }

        private static LastBearingFieldDeskActionProjection
            CreateStaffHumanAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canUse,
                bool visible)
        {
            bool assigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.HumanResidentId,
                StringComparison.Ordinal);
            return Action(
                LastBearingFieldDeskIntent.StaffCityServiceHuman,
                assigned
                    ? "HUMAN ASSIGNED · NEUTRAL"
                    : "STAFF HUMAN · NEUTRAL",
                !model.CityServiceLinkConnected
                    ? "Lock the permanent service link before staffing."
                    : assigned
                        ? "The human cohort is the current neutral operator."
                        : "World: click the human token, then the Machine Shop " +
                          "socket. Assign the human cohort to the one operator slot; " +
                          "no V0 bonus.",
                visible,
                canUse && controller.CanAssignCityServiceHuman && !assigned,
                LastBearingFieldDeskActionTone.Signal);
        }

        private static LastBearingFieldDeskActionProjection
            CreateStaffRobotAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canUse,
                bool visible)
        {
            bool assigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.RobotResidentId,
                StringComparison.Ordinal);
            return Action(
                LastBearingFieldDeskIntent.StaffCityServiceRobot,
                assigned
                    ? "UTILITY ROBOT ASSIGNED · NEUTRAL"
                    : "STAFF UTILITY ROBOT · NEUTRAL",
                !model.CityServiceLinkConnected
                    ? "Lock the permanent service link before staffing."
                    : assigned
                        ? "The utility robot is the current neutral operator."
                        : "World: click the utility-robot token, then the Machine " +
                          "Shop socket. Assign it to the one operator slot; " +
                          "no V0 bonus.",
                visible,
                canUse && controller.CanAssignCityServiceRobot && !assigned,
                LastBearingFieldDeskActionTone.Signal);
        }

        private static LastBearingFieldDeskActionProjection
            CreateAdvanceSledAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canUse,
                bool visible)
        {
            string label = model.CityDeliveryStage switch
            {
                CityDeliveryStage.AtRecycler => "ADVANCE PARTS SLED",
                CityDeliveryStage.InTransit =>
                    "COMMISSIONING DELIVERY · ONCE",
                _ => "COMMISSIONING COMPLETE · ONCE"
            };
            return Action(
                LastBearingFieldDeskIntent.AdvanceCityServiceSled,
                label,
                !model.CityServiceLinkConnected
                    ? "Permanently lock the service link before moving the sled."
                    : model.CityServiceResidentId == null
                        ? "Assign one neutral human or utility-robot operator first."
                        : "World: click the sled, then its Machine Shop destination. " +
                          "Two advances complete the route. Commissioning pays " +
                          "+2 PARTS · ONCE.",
                visible,
                canUse && controller.CanAdvanceCityServiceSled,
                LastBearingFieldDeskActionTone.Primary);
        }

        private static LastBearingFieldDeskActionProjection
            CreateCancelPreviewAction(
                LastBearingGameController controller,
                bool canUse,
                bool visible)
        {
            return Action(
                LastBearingFieldDeskIntent.CancelCityBuildingPreview,
                "CANCEL PREVIEW",
                "Accessibility fallback: discard only the world ghost; no parts are spent.",
                visible,
                canUse && controller.HasCityBuildingPreview,
                LastBearingFieldDeskActionTone.Quiet);
        }

        private static LastBearingFieldDeskActionProjection Action(
            LastBearingFieldDeskIntent intent,
            string label,
            string detail,
            bool visible,
            bool enabled,
            LastBearingFieldDeskActionTone tone)
        {
            return new LastBearingFieldDeskActionProjection(
                intent,
                label,
                detail,
                visible,
                enabled,
                tone);
        }

        private static LastBearingFieldDeskActionProjection Hidden()
        {
            return Action(
                LastBearingFieldDeskIntent.None,
                string.Empty,
                string.Empty,
                false,
                false,
                LastBearingFieldDeskActionTone.None);
        }

        private static bool Matches(
            LastBearingFieldDeskActionProjection action,
            LastBearingFieldDeskIntent intent)
        {
            return action.IsVisible && action.IsEnabled && action.Intent == intent;
        }

        private static bool IsWorkshopRelevant(LastBearingReadModel model)
        {
            return model.IsSpareBearingBatchStartAvailable ||
                   model.SpareBearingBatchPhase == SpareBearingBatchPhase.InProgress ||
                   model.IsSpareBearingBarterAvailable ||
                   model.SpareBearingBatchPhase == SpareBearingBatchPhase.Settled;
        }

        private static LastBearingFieldDeskActionProjection
            CreateEmergencyCisternAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canDispatch)
        {
            bool configuredAtHome =
                model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.SliceInfrastructureActive &&
                model.EmergencyStoragePadIndex >= 0 &&
                model.PreparationChoice != PreparationChoice.Unselected &&
                model.PlannedModule != VehicleModule.None &&
                model.DustFrontOutcome == DustFrontOutcome.Unresolved &&
                model.HotShiftPhase == HotShiftPhase.Idle &&
                !model.EmergencyCisternCharged;
            if (!configuredAtHome)
            {
                return Hidden();
            }

            string detail;
            if (model.PreparationChoice == PreparationChoice.WorkshopPush &&
                model.PreparationPhase == PreparationPhase.Preparing)
            {
                detail =
                    "Workshop Push has borrowed the operator. Finish preparation before pumping.";
            }
            else if (model.WaterMilli >
                LastBearingBalanceV1.WaterCapacityMilli -
                model.EmergencyCisternWaterMilli)
            {
                detail =
                    "The full 10.000-water fill will not fit. The cistern never spills or accepts a partial fill.";
            }
            else
            {
                detail =
                    "Commit one fuel only after Sasha's planned route reserve; the commissioned operator adds one full 10.000-water emergency fill.";
            }

            return Action(
                LastBearingFieldDeskIntent.OpenEmergencyCisternPump,
                "OPEN EMERGENCY STORAGE · WORK CISTERN PUMP",
                detail + " Open the physical pump station, then release and " +
                "press E or gamepad south.",
                true,
                canDispatch && controller.CanOpenEmergencyCisternPump,
                LastBearingFieldDeskActionTone.Hazard);
        }

        private static LastBearingFieldDeskActionProjection
            CreateHotShiftAction(
                LastBearingGameController controller,
                LastBearingReadModel model,
                bool canDispatch)
        {
            bool configuredAtHome =
                model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.SliceInfrastructureActive &&
                model.PreparationChoice != PreparationChoice.Unselected &&
                model.PlannedModule != VehicleModule.None;
            if (!configuredAtHome)
            {
                return Hidden();
            }

            string label;
            if (model.HotShiftPhase == HotShiftPhase.InProgress)
            {
                label = model.IsHotShiftStalledByDustFront
                    ? "HOT SHIFT · FRONT-STALLED · " +
                      model.HotShiftElapsedTicks + " / " +
                      model.HotShiftRequiredTicks
                    : model.IsHotShiftStalledByWorkshopPush
                        ? "HOT SHIFT · STALLED · " +
                          model.HotShiftElapsedTicks + " / " +
                          model.HotShiftRequiredTicks
                        : "HOT SHIFT · " + model.HotShiftElapsedTicks + " / " +
                          model.HotShiftRequiredTicks;
            }
            else
            {
                string verb = model.HotShiftCompletedCount > 0
                    ? "RUN ANOTHER HOT SHIFT"
                    : "RUN HOT SHIFT";
                label = verb + " · " + model.HotShiftFuelCostUnits +
                    " FUEL · " +
                    LastBearingBalanceV1.HotShiftRequiredSettlementTicks +
                    " TICKS · +" + model.HotShiftOutputPartsUnits + " PARTS";
            }

            string detail;
            if (model.IsHotShiftStalledByDustFront)
            {
                detail =
                    "The breached Dust Front stopped the failing waterworks. Progress and Hot Shift water draw stay held until turbine repair.";
            }
            else if (model.IsHotShiftStalledByWorkshopPush)
            {
                detail =
                    "Workshop Push borrowed the machine-shop operator. Progress is held and the stalled Hot Shift adds no water penalty.";
            }
            else if (model.HotShiftPhase == HotShiftPhase.InProgress)
            {
                detail =
                    "The operator is working. Hot Shift adds -0.010 water per settlement tick until completion.";
            }
            else if (model.PreparationChoice == PreparationChoice.CivicBuffer)
            {
                detail =
                    "Civic Buffer leaves the operator available. Working adds -0.010 water per settlement tick.";
            }
            else
            {
                detail =
                    "Workshop Push borrows the operator while preparation runs. The shift stalls with no water penalty until that operator returns.";
            }

            return Action(
                LastBearingFieldDeskIntent.RunHotShift,
                label,
                detail,
                true,
                canDispatch && controller.CanStartHotShift,
                LastBearingFieldDeskActionTone.Signal);
        }

        private static string FormatComposition(ColonyComposition composition)
        {
            return composition switch
            {
                ColonyComposition.HumanOnly => "HUMAN SETTLEMENT",
                ColonyComposition.RobotOnly => "UTILITY-ROBOT SETTLEMENT",
                ColonyComposition.Mixed => "HUMAN + UTILITY-ROBOT SETTLEMENT",
                _ => "SETTLEMENT ROSTER",
            };
        }

        private static string FormatPause(PauseCause pauseCause)
        {
            return pauseCause switch
            {
                PauseCause.None => "CLOCKS RUNNING",
                PauseCause.Explicit => "CLOCKS HELD BY PLAYER",
                PauseCause.AutoAlert => "CLOCKS HELD BY DEPOT ALERT",
                PauseCause.DustFrontAlert =>
                    "CLOCKS HELD BY DUST FRONT VERDICT",
                _ => "CLOCK STATE UNKNOWN",
            };
        }

        private static string FormatWater(long milli)
        {
            long whole = milli / 1000;
            long tenths = (milli < 0 ? -milli : milli) % 1000 / 100;
            return whole + "." + tenths + " WATER";
        }

        private static string FormatExactMilli(long milli)
        {
            long whole = milli / 1000;
            long fraction = Math.Abs(milli % 1000);
            return whole + "." + fraction.ToString("D3");
        }

        private static long ProjectWaterAtConstantDraw(
            long waterMilli,
            long trendMilliPerTick,
            long frontTicks)
        {
            long water = ClampWater(waterMilli);
            long ticks = Math.Max(0, frontTicks);
            if (trendMilliPerTick == 0 || ticks == 0)
            {
                return water;
            }

            if (trendMilliPerTick > 0)
            {
                long headroom =
                    LastBearingBalanceV1.WaterCapacityMilli - water;
                return ticks > headroom / trendMilliPerTick
                    ? LastBearingBalanceV1.WaterCapacityMilli
                    : water + (trendMilliPerTick * ticks);
            }

            if (trendMilliPerTick == long.MinValue)
            {
                return 0;
            }

            long drawMilliPerTick = -trendMilliPerTick;
            long ticksUntilEmpty = water == 0
                ? 0
                : 1 + ((water - 1) / drawMilliPerTick);
            return ticks >= ticksUntilEmpty
                ? 0
                : water - (drawMilliPerTick * ticks);
        }

        private static long ClampWater(long waterMilli)
        {
            return Math.Max(
                0,
                Math.Min(
                    LastBearingBalanceV1.WaterCapacityMilli,
                    waterMilli));
        }

        private static string FormatTrend(long trendMilli)
        {
            string sign = trendMilli > 0 ? "+" : trendMilli < 0 ? "-" : string.Empty;
            long magnitude = Math.Abs(trendMilli);
            return sign +
                   magnitude / 1000 +
                   "." +
                   (magnitude % 1000).ToString("D3") +
                   " WATER / SETTLEMENT TICK";
        }

        private static string FormatTurbine(TurbineCondition condition)
        {
            return condition switch
            {
                TurbineCondition.Failing => "TURBINE FAILING",
                TurbineCondition.BearingRepaired => "BEARING REPAIRED",
                TurbineCondition.SleeveRepaired => "FIELD SLEEVE FITTED",
                _ => "TURBINE STATE UNKNOWN",
            };
        }

        private static string FormatPressure(LastBearingReadModel model)
        {
            if (model.DustFrontOutcome == DustFrontOutcome.Held)
            {
                return "DUST FRONT HELD · RESERVE ENDURED";
            }

            if (model.DustFrontOutcome == DustFrontOutcome.Breached)
            {
                return model.IsHotShiftStalledByDustFront
                    ? "DUST FRONT BREACHED · HOT SHIFT STALLED"
                    : "DUST FRONT BREACHED · TURBINE RECOVERED";
            }

            if (model.WaterTrendMilliPerSettlementTick > 0)
            {
                return "RECOVERING · RESERVE CLIMBING";
            }

            if (model.WaterTrendMilliPerSettlementTick == 0)
            {
                return "HOLDING · RESERVE STEADY";
            }

            return "PRESSURE · RESERVE FALLING";
        }

        private static bool IsServiceCellObjective(string objective)
        {
            return objective == PlaceRecyclerObjective ||
                   objective == PlaceMachineShopObjective ||
                   objective == PlaceEmergencyStorageObjective ||
                   objective == ConnectServiceLinkObjective ||
                   objective == StaffServiceCellObjective ||
                   objective == AdvanceServiceSledObjective;
        }

        private static LastBearingFieldDeskIntent SelectBuildingIntent(
            CityBuildingKind building)
        {
            return building switch
            {
                CityBuildingKind.Recycler =>
                    LastBearingFieldDeskIntent.SelectRecycler,
                CityBuildingKind.MachineShop =>
                    LastBearingFieldDeskIntent.SelectMachineShop,
                CityBuildingKind.EmergencyStorage =>
                    LastBearingFieldDeskIntent.SelectEmergencyStorage,
                _ => LastBearingFieldDeskIntent.None
            };
        }

        private static string CityBuildingLabel(CityBuildingKind building)
        {
            return building switch
            {
                CityBuildingKind.Recycler => "RECYCLER",
                CityBuildingKind.MachineShop => "MACHINE SHOP",
                CityBuildingKind.EmergencyStorage => "EMERGENCY STORAGE",
                _ => "CITY BUILDING"
            };
        }

        private static long CityBuildingCost(CityBuildingKind building)
        {
            return building switch
            {
                CityBuildingKind.Recycler =>
                    LastBearingBalanceV1.RecyclerPlacementPartsUnits,
                CityBuildingKind.MachineShop =>
                    LastBearingBalanceV1.MachineShopPlacementPartsUnits,
                CityBuildingKind.EmergencyStorage =>
                    LastBearingBalanceV1.EmergencyStoragePlacementPartsUnits,
                _ => 0L
            };
        }

        private static string FormatPartsCost(long cost)
        {
            return cost + (cost == 1 ? " PART" : " PARTS");
        }

        private static string FormatReclaimedParts(long parts)
        {
            return parts +
                   (parts == 1 ? " reclaimed part" : " reclaimed parts");
        }

        private static int CityBuildingPad(
            LastBearingReadModel model,
            CityBuildingKind building)
        {
            return building switch
            {
                CityBuildingKind.Recycler => model.RecyclerPadIndex,
                CityBuildingKind.MachineShop => model.MachineShopPadIndex,
                CityBuildingKind.EmergencyStorage =>
                    model.EmergencyStoragePadIndex,
                _ => -1
            };
        }

        private static bool IsPadOccupiedByOther(
            LastBearingReadModel model,
            CityBuildingKind building,
            int padIndex)
        {
            return (building != CityBuildingKind.Recycler &&
                    model.RecyclerPadIndex == padIndex) ||
                   (building != CityBuildingKind.MachineShop &&
                    model.MachineShopPadIndex == padIndex) ||
                   (building != CityBuildingKind.EmergencyStorage &&
                    model.EmergencyStoragePadIndex == padIndex);
        }

        private static string FormatServiceCellState(
            LastBearingReadModel model,
            LastBearingGameController controller)
        {
            string preview = controller.HasCityBuildingPreview
                ? "PREVIEW " +
                  CityBuildingLabel(controller.CityPreviewBuilding) +
                  " @ PAD " + (controller.CityPreviewPadIndex + 1) +
                  " / " + (controller.CityPreviewQuarterTurns * 90) + "°"
                : "NO PREVIEW";
            return "RECYCLER " + FormatPad(model.RecyclerPadIndex) +
                   " · SHOP " + FormatPad(model.MachineShopPadIndex) +
                   " · STORAGE " + FormatPad(model.EmergencyStoragePadIndex) +
                   " · LINK " +
                   (model.CityServiceLinkConnected ? "LOCKED" : "OPEN") +
                   " · OPERATOR " +
                   FormatOperator(model.CityServiceResidentId) +
                   " · " + FormatCityDelivery(model.CityDeliveryStage) +
                   " · " + preview;
        }

        private static string FormatPad(int padIndex)
        {
            return padIndex < 0 ? "UNPLACED" : "PAD " + (padIndex + 1);
        }

        private static string FormatOperator(string? stableId)
        {
            if (string.Equals(
                    stableId,
                    ResidentRoster.HumanResidentId,
                    StringComparison.Ordinal))
            {
                return "HUMAN · NEUTRAL";
            }

            if (string.Equals(
                    stableId,
                    ResidentRoster.RobotResidentId,
                    StringComparison.Ordinal))
            {
                return "UTILITY ROBOT · NEUTRAL";
            }

            return stableId == null ? "UNSTAFFED" : stableId + " · NEUTRAL";
        }

        private static string FormatCityDelivery(CityDeliveryStage stage)
        {
            return stage switch
            {
                CityDeliveryStage.AtRecycler => "SLED AT RECYCLER",
                CityDeliveryStage.InTransit => "SLED IN TRANSIT",
                CityDeliveryStage.DeliveredToWorkshop =>
                    "COMMISSIONING DELIVERY · ONCE",
                _ => "SLED STATE UNKNOWN"
            };
        }

        private static void Mix(ref ulong hash, bool value)
        {
            Mix(ref hash, value ? 1L : 0L);
        }

        private static void Mix(ref ulong hash, int value)
        {
            Mix(ref hash, (long)value);
        }

        private static void Mix(ref ulong hash, long value)
        {
            unchecked
            {
                ulong data = (ulong)value;
                for (var shift = 0; shift < 64; shift += 8)
                {
                    hash ^= (byte)(data >> shift);
                    hash *= Prime;
                }
            }
        }

        private static void Mix(ref ulong hash, string? value)
        {
            unchecked
            {
                if (value == null)
                {
                    Mix(ref hash, -1L);
                    return;
                }

                Mix(ref hash, value.Length);
                for (var index = 0; index < value.Length; index++)
                {
                    char character = value[index];
                    hash ^= (byte)character;
                    hash *= Prime;
                    hash ^= (byte)(character >> 8);
                    hash *= Prime;
                }
            }
        }
    }
}
