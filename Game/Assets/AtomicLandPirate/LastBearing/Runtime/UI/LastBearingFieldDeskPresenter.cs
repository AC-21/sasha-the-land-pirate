#nullable enable

using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum LastBearingFieldDeskIntent
    {
        None = 0,
        AssignDefaultLead = 1,
        InspectCityNeed = 2,
        SelectTrialA = 3,
        SelectTrialB = 4,
        ManipulateTrial = 5,
        RotateTrial = 6,
        ToggleTrialPiece = 7,
        ConnectTrial = 8,
        AdvanceTrialDelivery = 9,
        RecordPathClear = 10,
        RecordPathUnclear = 11,
        ResetActiveTrial = 12,
        LeaveTrial = 13,
        ClearTrials = 14,
        ActivateInfrastructure = 15,
        BeginWorkshopPush = 16,
        BeginCivicBuffer = 17,
        OpenGarage = 18,
        CommitExpedition = 19,
        OpenPumpHallRepair = 20,
        OpenOneGoodBatchWorkshop = 21,
        InstallCityImprovement = 22,
        ServiceFieldSleeve = 23,
        TogglePause = 24,
        Save = 25,
        Load = 26,
        ReturnToTitle = 27,
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
            LastBearingFieldDeskActionProjection selectA,
            LastBearingFieldDeskActionProjection selectB,
            LastBearingFieldDeskActionProjection manipulate,
            LastBearingFieldDeskActionProjection rotate,
            LastBearingFieldDeskActionProjection togglePiece,
            LastBearingFieldDeskActionProjection connect,
            LastBearingFieldDeskActionProjection advance,
            LastBearingFieldDeskActionProjection recordClear,
            LastBearingFieldDeskActionProjection recordUnclear,
            LastBearingFieldDeskActionProjection reset,
            LastBearingFieldDeskActionProjection leave,
            LastBearingFieldDeskActionProjection clear)
        {
            IsVisible = visible;
            HypothesisLabel = hypothesisLabel;
            Evidence = evidence;
            SelectA = selectA;
            SelectB = selectB;
            Manipulate = manipulate;
            Rotate = rotate;
            TogglePiece = togglePiece;
            Connect = connect;
            Advance = advance;
            RecordClear = recordClear;
            RecordUnclear = recordUnclear;
            Reset = reset;
            Leave = leave;
            Clear = clear;
        }

        public bool IsVisible { get; }

        public string HypothesisLabel { get; }

        public string Evidence { get; }

        public LastBearingFieldDeskActionProjection SelectA { get; }

        public LastBearingFieldDeskActionProjection SelectB { get; }

        public LastBearingFieldDeskActionProjection Manipulate { get; }

        public LastBearingFieldDeskActionProjection Rotate { get; }

        public LastBearingFieldDeskActionProjection TogglePiece { get; }

        public LastBearingFieldDeskActionProjection Connect { get; }

        public LastBearingFieldDeskActionProjection Advance { get; }

        public LastBearingFieldDeskActionProjection RecordClear { get; }

        public LastBearingFieldDeskActionProjection RecordUnclear { get; }

        public LastBearingFieldDeskActionProjection Reset { get; }

        public LastBearingFieldDeskActionProjection Leave { get; }

        public LastBearingFieldDeskActionProjection Clear { get; }
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
        private const string ActivateInfrastructureObjective =
            "activate-slice-infrastructure";
        private const string SelectPreparationObjective =
            "select-preparation-and-module";
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static LastBearingFieldDeskProjection Present(
            LastBearingGameController controller)
        {
            LastBearingReadModel? model = controller.ReadModel;
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
                    permitJob,
                    unavailable,
                    unavailable,
                    CreateSurvey(controller, null, false),
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
                permitJob,
                primary,
                secondary,
                CreateSurvey(
                    controller,
                    model,
                    canDispatch && controller.CityNeedInspected &&
                    model.NextObjective == ActivateInfrastructureObjective),
                Action(
                    LastBearingFieldDeskIntent.TogglePause,
                    model.PauseCause == PauseCause.None ? "PAUSE" : "RESUME",
                    model.PauseCause == PauseCause.AutoAlert
                        ? "The depot alert must be resolved in place."
                        : "Hold or resume the settlement clocks.",
                    true,
                    canDispatch && model.PauseCause != PauseCause.AutoAlert,
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
                   Matches(projection.Survey.SelectA, intent) ||
                   Matches(projection.Survey.SelectB, intent) ||
                   Matches(projection.Survey.Manipulate, intent) ||
                   Matches(projection.Survey.Rotate, intent) ||
                   Matches(projection.Survey.TogglePiece, intent) ||
                   Matches(projection.Survey.Connect, intent) ||
                   Matches(projection.Survey.Advance, intent) ||
                   Matches(projection.Survey.RecordClear, intent) ||
                   Matches(projection.Survey.RecordUnclear, intent) ||
                   Matches(projection.Survey.Reset, intent) ||
                   Matches(projection.Survey.Leave, intent) ||
                   Matches(projection.Survey.Clear, intent);
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

            LastBearingReadModel? model = controller.ReadModel;
            if (model == null)
            {
                Mix(ref hash, -1);
                return new LastBearingFieldDeskStamp(hash);
            }

            Mix(ref hash, model.Composition.GetHashCode());
            Mix(ref hash, model.AssignedResidentId);
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
            Mix(ref hash, model.NextObjective);
            return new LastBearingFieldDeskStamp(hash);
        }

        private static void DeriveCurrentOrder(
            LastBearingGameController controller,
            LastBearingReadModel model,
            bool canDispatch,
            out LastBearingFieldDeskActionProjection primary,
            out LastBearingFieldDeskActionProjection secondary)
        {
            secondary = Hidden();
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

            if (model.NextObjective == ActivateInfrastructureObjective)
            {
                DeriveTrialOrder(controller, canDispatch, out primary, out secondary);
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
                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.TurbineCondition == TurbineCondition.Failing &&
                model.RepairCargoKind == RepairCargoKind.None &&
                (model.PreparationPhase == PreparationPhase.Ready ||
                 model.PreparationPhase == PreparationPhase.Committed))
            {
                primary = Action(
                    LastBearingFieldDeskIntent.CommitExpedition,
                    "COMMIT THE MANIFEST",
                    "Take responsibility for the bounded fuel and cargo load.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = Action(
                    LastBearingFieldDeskIntent.OpenGarage,
                    "INSPECT THE GARAGE",
                    "Review the fitted Sasha Scout before departure.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Quiet);
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
                    LastBearingFieldDeskIntent.InstallCityImprovement,
                    "SEAT THE AUXILIARY PUMP",
                    "Commit the returned rotor to the existing civic socket.",
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
                    "SERVICE THE FIELD SLEEVE",
                    "Keep the cooperative maintenance obligation legible.",
                    true,
                    canDispatch,
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
        }

        private static void DeriveTrialOrder(
            LastBearingGameController controller,
            bool canDispatch,
            out LastBearingFieldDeskActionProjection primary,
            out LastBearingFieldDeskActionProjection secondary)
        {
            secondary = Hidden();
            if (controller.HasCompletedCityGrammarObservation)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.ActivateInfrastructure,
                    "BRING THE SERVICE CELL ONLINE",
                    "Use the recorded observation without selecting a city grammar.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            if (controller.CityGrammarHypothesis ==
                LastBearingCityGrammarHypothesis.Unselected)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.SelectTrialA,
                    "STAGE TRIAL A",
                    "Place the recycler and workshop as individual service pieces.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = Action(
                    LastBearingFieldDeskIntent.SelectTrialB,
                    "STAGE TRIAL B",
                    "Move the same empty service cell as one district stamp.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Signal);
                return;
            }

            if (!controller.CityGrammarLogisticsConnected)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.ManipulateTrial,
                    "PLACE / MOVE THE TRIAL",
                    "Stage the active empty service-cell hypothesis.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                secondary = Action(
                    LastBearingFieldDeskIntent.ConnectTrial,
                    "CONNECT THE SERVICE LINK",
                    controller.CanConnectCityGrammarLogistics
                        ? "Connect recycler output to workshop input."
                        : "Place recycler and workshop on different pads first.",
                    controller.CityGrammarHypothesis ==
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid,
                    canDispatch && controller.CanConnectCityGrammarLogistics,
                    LastBearingFieldDeskActionTone.Signal);
                return;
            }

            if (controller.CityGrammarDeliveryStage !=
                LastBearingCityTrialDeliveryStage.DeliveredToWorkshop)
            {
                primary = Action(
                    LastBearingFieldDeskIntent.AdvanceTrialDelivery,
                    "ADVANCE THE EMPTY SLED",
                    "Move the same calibration sled across the service path.",
                    true,
                    canDispatch,
                    LastBearingFieldDeskActionTone.Primary);
                return;
            }

            primary = Action(
                LastBearingFieldDeskIntent.RecordPathClear,
                "RECORD: CLEAR",
                "Record only the raw path observation.",
                true,
                canDispatch,
                LastBearingFieldDeskActionTone.Primary);
            secondary = Action(
                LastBearingFieldDeskIntent.RecordPathUnclear,
                "RECORD: UNCLEAR",
                "Record only the raw path observation.",
                true,
                canDispatch,
                LastBearingFieldDeskActionTone.Signal);
        }

        private static LastBearingFieldDeskSurveyProjection CreateSurvey(
            LastBearingGameController controller,
            LastBearingReadModel? model,
            bool visible)
        {
            bool selected = controller.CityGrammarHypothesis !=
                            LastBearingCityGrammarHypothesis.Unselected;
            bool trialA = controller.CityGrammarHypothesis ==
                          LastBearingCityGrammarHypothesis.RestrainedSnapGrid;
            bool delivered = controller.CityGrammarDeliveryStage ==
                             LastBearingCityTrialDeliveryStage.DeliveredToWorkshop;
            bool unrecorded = controller.CityGrammarPathRead ==
                              LastBearingCityTrialPathRead.Unrecorded;
            bool canUse = visible && !controller.HasPendingPlayerCommands;
            string label = selected
                ? FormatHypothesis(controller.CityGrammarHypothesis) +
                  " · " + FormatPiece(controller.ActiveCityGrammarPiece) +
                  " · " + FormatDelivery(controller.CityGrammarDeliveryStage)
                : "NO HYPOTHESIS STAGED · BOTH TRIALS REMAIN REVERSIBLE";
            string evidence = visible
                ? controller.CityGrammarEvidence
                : "Comparison stowed outside the current civic trial.";
            return new LastBearingFieldDeskSurveyProjection(
                visible,
                label,
                evidence,
                Action(LastBearingFieldDeskIntent.SelectTrialA, "TRIAL A", "Restrained snap-grid.", true, canUse, LastBearingFieldDeskActionTone.Signal),
                Action(LastBearingFieldDeskIntent.SelectTrialB, "TRIAL B", "District stamp.", true, canUse, LastBearingFieldDeskActionTone.Signal),
                Action(LastBearingFieldDeskIntent.ManipulateTrial, trialA ? "PLACE / MOVE" : "MOVE STAMP", "Manipulate the active presentation trial.", selected, canUse && selected, LastBearingFieldDeskActionTone.Primary),
                Action(LastBearingFieldDeskIntent.RotateTrial, "ROTATE", "Rotate the active presentation trial.", selected, canUse && selected, LastBearingFieldDeskActionTone.Quiet),
                Action(LastBearingFieldDeskIntent.ToggleTrialPiece, "SWITCH PIECE", "Switch recycler and workshop in trial A.", trialA, canUse && trialA, LastBearingFieldDeskActionTone.Quiet),
                Action(LastBearingFieldDeskIntent.ConnectTrial, "CONNECT", "Connect the existing service link.", trialA, canUse && controller.CanConnectCityGrammarLogistics, LastBearingFieldDeskActionTone.Signal),
                Action(LastBearingFieldDeskIntent.AdvanceTrialDelivery, "ADVANCE SLED", "Advance the empty calibration sled.", selected, canUse && selected && controller.CityGrammarLogisticsConnected && !delivered, LastBearingFieldDeskActionTone.Primary),
                Action(LastBearingFieldDeskIntent.RecordPathClear, "CLEAR", "Record raw path evidence.", selected, canUse && selected && delivered && unrecorded, LastBearingFieldDeskActionTone.Signal),
                Action(LastBearingFieldDeskIntent.RecordPathUnclear, "UNCLEAR", "Record raw path evidence.", selected, canUse && selected && delivered && unrecorded, LastBearingFieldDeskActionTone.Signal),
                Action(LastBearingFieldDeskIntent.ResetActiveTrial, "RESET ACTIVE", "Reset only the active trial.", selected, canUse && selected, LastBearingFieldDeskActionTone.Quiet),
                Action(LastBearingFieldDeskIntent.LeaveTrial, "LEAVE", "Stow the comparison without clearing it.", selected, canUse && selected, LastBearingFieldDeskActionTone.Quiet),
                Action(LastBearingFieldDeskIntent.ClearTrials, "CLEAR ALL", "Clear both in-memory trials.", selected || controller.HasCompletedCityGrammarObservation, canUse && (selected || controller.HasCompletedCityGrammarObservation), LastBearingFieldDeskActionTone.Hazard));
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
                _ => "CLOCK STATE UNKNOWN",
            };
        }

        private static string FormatWater(long milli)
        {
            long whole = milli / 1000;
            long tenths = (milli < 0 ? -milli : milli) % 1000 / 100;
            return whole + "." + tenths + " WATER";
        }

        private static string FormatTrend(long trend)
        {
            string sign = trend > 0 ? "+" : string.Empty;
            return sign + trend + " / SETTLEMENT TICK";
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

        private static string FormatHypothesis(
            LastBearingCityGrammarHypothesis hypothesis)
        {
            return hypothesis == LastBearingCityGrammarHypothesis.RestrainedSnapGrid
                ? "TRIAL A · RESTRAINED SNAP-GRID"
                : "TRIAL B · DISTRICT STAMP";
        }

        private static string FormatPiece(LastBearingCityTrialPiece piece)
        {
            return piece == LastBearingCityTrialPiece.Recycler
                ? "RECYCLER ACTIVE"
                : "WORKSHOP ACTIVE";
        }

        private static string FormatDelivery(
            LastBearingCityTrialDeliveryStage stage)
        {
            return stage switch
            {
                LastBearingCityTrialDeliveryStage.AtRecycler => "SLED AT RECYCLER",
                LastBearingCityTrialDeliveryStage.InTransit => "SLED IN TRANSIT",
                LastBearingCityTrialDeliveryStage.DeliveredToWorkshop => "SLED AT WORKSHOP",
                _ => "SLED STATE UNKNOWN",
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
