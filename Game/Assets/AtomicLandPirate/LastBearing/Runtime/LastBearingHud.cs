#nullable enable

using System.Text;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Intentionally temporary IMGUI instrumentation for the constitutional
    /// toy. It reads the core model and emits semantic player intents only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingHud : MonoBehaviour
    {
        private const float PanelWidth = 430f;
        private LastBearingGameController? _controller;
        private GUIStyle? _panelStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _headingStyle;
        private GUIStyle? _bodyStyle;
        private GUIStyle? _mutedStyle;
        private GUIStyle? _buttonStyle;
        private Vector2 _scroll;

        public void Configure(LastBearingGameController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (_controller == null)
            {
                return;
            }

            EnsureStyles();
            var panelRect = new Rect(18f, 18f, PanelWidth, Screen.height - 36f);
            GUI.Box(panelRect, GUIContent.none, _panelStyle!);

            GUILayout.BeginArea(new Rect(
                panelRect.x + 18f,
                panelRect.y + 16f,
                panelRect.width - 36f,
                panelRect.height - 32f));
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            DrawHeader();
            if (_controller.HasActiveGame && _controller.ReadModel != null)
            {
                DrawActiveGame(_controller.ReadModel);
            }
            else
            {
                DrawTitle();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.Label("SASHA THE ATOMIC LAND PIRATE", _titleStyle);
            GUILayout.Label("THE LAST BEARING · DEV PROFILE", _mutedStyle);
            GUILayout.Space(8f);
            GUILayout.Label(_controller!.Status, _bodyStyle);
            GUILayout.Space(10f);
        }

        private void DrawTitle()
        {
            GUILayout.Label("WHO CALLS THIS HOME?", _headingStyle);
            GUILayout.Label(
                "Composition changes the represented residents, never the costs, " +
                "timings, commands, or viability in WP-0002.",
                _bodyStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button("HUMAN-ONLY COLONY", _buttonStyle))
            {
                _controller!.StartNewGame(ColonyComposition.HumanOnly);
            }

            if (GUILayout.Button("UTILITY-ROBOT-ONLY COLONY", _buttonStyle))
            {
                _controller!.StartNewGame(ColonyComposition.RobotOnly);
            }

            if (GUILayout.Button("MIXED COLONY", _buttonStyle))
            {
                _controller!.StartNewGame(ColonyComposition.Mixed);
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("LOAD LAST-BEARING-DEV-V1", _buttonStyle))
            {
                _controller!.Load();
            }

            GUILayout.Label(_controller!.SaveStatus, _mutedStyle);
        }

        private void DrawActiveGame(LastBearingReadModel model)
        {
            LastBearingPermitJobPresentation permitJob =
                LastBearingPermitJobPresenter.Present(
                    model,
                    _controller!.CityNeedInspected);
            DrawPermitJob(permitJob);
            GUILayout.Space(12f);

            GUILayout.Label("CIVIC INSTRUMENTS", _headingStyle);
            GUILayout.Label(BuildInstrumentText(model), _bodyStyle);
            GUILayout.Space(10f);

            DrawCityNeedAndGrammar(model);
            GUILayout.Space(10f);

            DrawPresentationMode(model);
            GUILayout.Space(10f);

            DrawOneGoodBatch(model);
            GUILayout.Space(10f);

            GUILayout.Label("CURRENT ACTION", _headingStyle);
            DrawContextActions(model, permitJob);
            GUILayout.Space(10f);

            GUILayout.Label("CONTROLS", _headingStyle);
            GUILayout.Label(
                model.IsWreckLineModulePointAvailable
                    ? model.RouteActionKind == RouteActionKind.DeployWinch
                        ? "E or gamepad south · deploy winch + recover rotor\nP · pause  F5 · save  F9 · load"
                        : "E or gamepad south · cross sealed dust exposure\nP · pause  F5 · save  F9 · load"
                    : model.IsDepotApproachRecoveryAvailable
                    ? "E or gamepad south · seat recovery bridle\nP · pause  F5 · save  F9 · load"
                    : model.ExpeditionPhase == ExpeditionPhase.Outbound ||
                model.ExpeditionPhase == ExpeditionPhase.Returning
                    ? _controller!.CanRecoverRoadPresentation
                        ? "W/right trigger · throttle\nS/left trigger · presentation brake + reverse\nA/D or stick · steer  Space/LB · handbrake\nR/gamepad north · recover local rig\nP · pause  F5 · save  F9 · load"
                        : "W/right trigger · throttle\nS/left trigger · presentation brake + reverse\nA/D or stick · steer  Space/LB · handbrake\nLocal rig recovery unavailable\nP · pause  F5 · save  F9 · load"
                    : model.ExpeditionPhase == ExpeditionPhase.AtDepot
                        ? "Depot view locked · choose encounter and cargo below\nP · pause  F5 · save  F9 · load"
                    : "WASD · camera pan  Q/E · rotate\nMouse wheel · zoom  RMB · orbit\nP · pause  F5 · save  F9 · load",
                _mutedStyle);

            GUILayout.BeginHorizontal();
            if (model.PauseCause == PauseCause.AutoAlert)
            {
                GUILayout.Label(
                    "AUTO-PAUSED · resolve the depot encounter below",
                    _mutedStyle);
            }
            else if (GUILayout.Button("PAUSE / RESUME", _buttonStyle))
            {
                _controller!.TogglePause();
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot &&
                model.RepairCargoKind == RepairCargoKind.None &&
                model.PauseCause == PauseCause.None &&
                GUILayout.Button("FORECAST ALERT", _buttonStyle))
            {
                _controller!.TriggerAutoPauseAlert();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAVE", _buttonStyle))
            {
                _controller!.Save();
            }

            if (GUILayout.Button("LOAD", _buttonStyle))
            {
                _controller!.Load();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(_controller!.SaveStatus, _mutedStyle);
            GUILayout.Label("STATE " + _controller.CanonicalHash, _mutedStyle);

            if (GUILayout.Button("RETURN TO TITLE", _buttonStyle))
            {
                _controller!.ReturnToTitle();
            }
        }

        private void DrawContextActions(
            LastBearingReadModel model,
            LastBearingPermitJobPresentation permitJob)
        {
            if (model.AssignedResidentId == null)
            {
                GUILayout.Label(
                    "This valid profile has no expedition lead. Assign the " +
                    "composition's default lead before continuing.",
                    _bodyStyle);
                if (GUILayout.Button("ASSIGN DEFAULT EXPEDITION LEAD", _buttonStyle))
                {
                    _controller!.AssignDefaultLeadResident();
                }

                return;
            }

            if (!_controller!.CityNeedInspected)
            {
                GUILayout.Label(
                    "Inspect the failing water system before committing labor, " +
                    "infrastructure, or an expedition plan.",
                    _bodyStyle);
                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.PreparationChoice == PreparationChoice.Unselected &&
                model.TurbineCondition == TurbineCondition.Failing)
            {
                if (model.NextObjective == "activate-slice-infrastructure")
                {
                    if (!_controller.HasCompletedCityGrammarObservation)
                    {
                        GUILayout.Label(
                            "Complete either neutral service-cell trial above: stage the same recycler and workshop, move one empty calibration sled, and record whether the path reads clearly.",
                            _bodyStyle);
                        return;
                    }

                    GUILayout.Label(
                        "A trial observation exists. Bringing the service cell online records only the existing infrastructure fact—never the tested layout or a D-0030 winner.",
                        _mutedStyle);
                    if (GUILayout.Button(
                            "BRING THE SAME RECYCLER + MACHINE SHOP ONLINE",
                            _buttonStyle))
                    {
                        _controller.ActivateInfrastructure();
                    }

                    return;
                }

                GUILayout.Label(
                    "All four plans are valid. The marked first run reaches the complete Permit Job continuation.",
                    _mutedStyle);
                DrawPlanButton(
                    "WORKSHOP PUSH + WINCH\nAUXILIARY-PUMP BRANCH",
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly);
                DrawPlanButton(
                    "WORKSHOP PUSH + RANGE TANK\nCISTERN BRANCH",
                    PreparationChoice.WorkshopPush,
                    VehicleModule.SealedRangeTank);
                DrawPlanButton(
                    "RECOMMENDED FIRST RUN · CIVIC BUFFER + WINCH\nONE GOOD BATCH + PERMIT JOB",
                    PreparationChoice.CivicBuffer,
                    VehicleModule.WinchAssembly);
                DrawPlanButton(
                    "CIVIC BUFFER + RANGE TANK\nDEPOT-ACCESS BRANCH",
                    PreparationChoice.CivicBuffer,
                    VehicleModule.SealedRangeTank);
                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                (model.PreparationPhase == PreparationPhase.Ready ||
                 model.PreparationPhase == PreparationPhase.Committed) &&
                model.TurbineCondition == TurbineCondition.Failing &&
                model.RepairCargoKind == RepairCargoKind.None)
            {
                if (GUILayout.Button("COMMIT MANIFEST + DEPART", _buttonStyle))
                {
                    _controller!.CommitExpedition();
                }

                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Outbound ||
                model.ExpeditionPhase == ExpeditionPhase.Returning)
            {
                if (model.IsWreckLineModulePointAvailable)
                {
                    bool winch = model.RouteActionKind ==
                        RouteActionKind.DeployWinch;
                    GUILayout.Label(
                        winch
                            ? "Wreck Line: deploy the fitted winch to take the one existing pump rotor into vehicle custody."
                            : "Wreck Line: use the sealed range tank to cross the dust exposure; the pump rotor remains behind.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            winch
                                ? "DEPLOY WINCH · RECOVER PUMP ROTOR"
                                : "CROSS SEALED DUST EXPOSURE",
                            _buttonStyle))
                    {
                        _controller!.OperateWreckLineModulePoint();
                    }

                    return;
                }

                if (model.IsDepotApproachRecoveryAvailable)
                {
                    GUILayout.Label(
                        "The route is complete. The canonical recovery point is " +
                        "lit in tungsten; seat the depot bridle before entering.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "OPERATE DEPOT RECOVERY POINT",
                            _buttonStyle))
                    {
                        _controller!.OperateDepotApproachRecoveryPoint();
                    }

                    return;
                }

                GUILayout.Label(
                    "Drive the authored corridor. Route progress comes only from " +
                    "quantized core commands.",
                    _bodyStyle);
                if (_controller.CanRecoverRoadPresentation)
                {
                    GUILayout.Label(
                        "If local physics becomes unhelpful, recover only the presentation rig to Sasha's current canonical road marker.",
                        _mutedStyle);
                    if (GUILayout.Button(
                            "RECOVER LOCAL RIG · R / GAMEPAD NORTH",
                            _buttonStyle))
                    {
                        _controller.RecoverRoadPresentation();
                    }
                }

                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot &&
                model.RepairCargoKind == RepairCargoKind.None)
            {
                if (GUILayout.Button(
                        "COOPERATE · FIELD SLEEVE + OBLIGATION\nALTERNATE CONCLUSION",
                        _buttonStyle))
                {
                    _controller!.ResolveDepot(cooperate: true);
                }

                bool opensPermitJob =
                    model.PreparationChoice == PreparationChoice.CivicBuffer &&
                    model.PlannedModule == VehicleModule.WinchAssembly;
                if (GUILayout.Button(
                        opensPermitJob
                            ? "RECOMMENDED FIRST RUN · TAKE THE CLAIMED BEARING\nOPENS ONE GOOD BATCH"
                            : "TAKE THE CLAIMED BEARING\nCERAMIC REPAIR + AGGRIEVED DEPOT",
                        _buttonStyle))
                {
                    _controller!.ResolveDepot(cooperate: false);
                }

                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                if (model.VehicleModule == VehicleModule.SealedRangeTank &&
                    model.LiquidCargoKind == LiquidCargoKind.None)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("LOAD WATER", _buttonStyle))
                    {
                        _controller!.ChooseLiquidReturn(LiquidCargoKind.Water);
                    }

                    if (GUILayout.Button("LOAD FUEL", _buttonStyle))
                    {
                        _controller!.ChooseLiquidReturn(LiquidCargoKind.Fuel);
                    }

                    GUILayout.EndHorizontal();
                }

                bool liquidSelectionRequired =
                    model.VehicleModule == VehicleModule.SealedRangeTank &&
                    model.LiquidCargoKind == LiquidCargoKind.None;
                if (liquidSelectionRequired)
                {
                    GUILayout.Label(
                        "Select one tank cargo before freezing the return payload.",
                        _mutedStyle);
                }
                else if (GUILayout.Button("FREEZE PAYLOAD + RETURN", _buttonStyle))
                {
                    _controller!.BeginReturn();
                }

                return;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Returned &&
                model.TransactionPhase != TransactionPhase.Finalized)
            {
                if (GUILayout.Button("CREDIT RETURN TO LAST BEARING", _buttonStyle))
                {
                    _controller!.CompleteReturn();
                }

                return;
            }

            if (model.TurbineCondition == TurbineCondition.Failing &&
                model.RepairCargoKind != RepairCargoKind.None)
            {
                if (GUILayout.Button("INSTALL TURBINE REPAIR", _buttonStyle))
                {
                    _controller!.RepairTurbine();
                }

                return;
            }

            if (model.IsCityImprovementInstallationAvailable)
            {
                GUILayout.Label(
                    "The returned pump rotor is staged at the exact civic socket. " +
                    "This one-shot installation costs " +
                    LastBearingBalanceV1.AuxiliaryPumpInstallationPartsUnits +
                    " parts and preserves the minimum reserve.",
                    _bodyStyle);
                if (GUILayout.Button(
                        "INSTALL REFURBISHED AUXILIARY PUMP",
                        _buttonStyle))
                {
                    _controller!.InstallCityImprovement();
                }

                return;
            }

            if (model.IsSpareBearingBatchStartAvailable)
            {
                GUILayout.Label(
                    "The machine shop can commit exactly one bounded spare-bearing batch while preserving the civic parts reserve.",
                    _bodyStyle);
                if (GUILayout.Button(
                        "START ONE SPARE-BEARING LOT",
                        _buttonStyle))
                {
                    _controller!.StartSpareBearingBatch();
                }

                return;
            }

            if (model.IsSpareBearingBarterAvailable)
            {
                GUILayout.Label(
                    "The completed physical lot can cross the claims wicket exactly once for the fixed depot corridor permit.",
                    _bodyStyle);
                if (GUILayout.Button(
                        "BARTER LOT FOR DEPOT ROUTE PERMIT",
                        _buttonStyle))
                {
                    _controller!.BarterSpareBearingLot();
                }

                return;
            }

            if (model.MaintenanceDue)
            {
                if (GUILayout.Button("SERVICE FIELD SLEEVE · 2 PARTS", _buttonStyle))
                {
                    _controller!.ServiceFieldSleeve();
                }

                return;
            }

            GUILayout.Label(permitJob.ProgressLabel, _bodyStyle);
        }

        private void DrawPermitJob(
            LastBearingPermitJobPresentation presentation)
        {
            GUILayout.Label(
                "THE PERMIT JOB · STEP " + presentation.StepIndex +
                " OF " + presentation.StepCount,
                _headingStyle);
            GUILayout.Label(presentation.ChapterLabel, _mutedStyle);
            GUILayout.Label(presentation.Headline, _titleStyle);
            GUILayout.Label(presentation.Detail, _bodyStyle);

            if (presentation.HasMeasuredPhaseProgress)
            {
                DrawProgressBar(
                    presentation.PhaseProgressCurrent,
                    presentation.PhaseProgressTarget);
            }

            GUILayout.Label(presentation.ProgressLabel, _mutedStyle);
            if (presentation.ShowRecommendedFirstRunCue)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    presentation.RecommendedFirstRunCue,
                    _bodyStyle);
            }

            if (presentation.IsFinale)
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    "PERMIT JOB COMPLETE · CONSEQUENCES RECORDED",
                    _headingStyle);
            }
            else if (presentation.IsAlternateConclusion)
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    "VALID ALTERNATE ENDING · REPLAY FOR THE PERMIT JOB",
                    _headingStyle);
            }
        }

        private static void DrawProgressBar(long current, long target)
        {
            float normalized = target <= 0
                ? 0f
                : Mathf.Clamp01((float)current / target);
            Rect rect = GUILayoutUtility.GetRect(
                1f,
                12f,
                GUILayout.ExpandWidth(true));
            Color previous = GUI.color;
            GUI.color = new Color(0.16f, 0.17f, 0.16f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.92f, 0.48f, 0.20f, 1f);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width * normalized, rect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawPresentationMode(LastBearingReadModel model)
        {
            LastBearingModeCoordinator? coordinator = _controller!.ModeCoordinator;
            GUILayout.Label("ONE-SCENE VIEW", _headingStyle);
            GUILayout.Label(
                coordinator?.HasActiveMode == true
                    ? "R0 ROUTING SCAFFOLD · " + coordinator.CurrentMode
                    : "Inactive",
                _bodyStyle);
            if (model.ExpeditionPhase != ExpeditionPhase.AtHome)
            {
                GUILayout.Label(
                    "Driving, depot encounter, and city return views follow the canonical expedition phase.",
                    _mutedStyle);
                return;
            }

            GUILayout.Label(
                "These inspection views are local presentation only. There is no on-foot mode.",
                _mutedStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CITY", _buttonStyle))
            {
                _controller.ShowCityOverview();
            }

            if (GUILayout.Button("PUMP HALL", _buttonStyle))
            {
                _controller.World?.SelectPumpHallCutaway();
                _controller.OpenBuildingCutaway();
            }

            if (GUILayout.Button("WORKSHOP / CLAIMS", _buttonStyle))
            {
                _controller.World?.SelectOneGoodBatchCutaway();
                _controller.OpenBuildingCutaway();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("GARAGE", _buttonStyle))
            {
                _controller.OpenGarageBay();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawOneGoodBatch(LastBearingReadModel model)
        {
            bool visible = model.IsSpareBearingBatchStartAvailable ||
                           model.SpareBearingRecipe != SpareBearingRecipe.None ||
                           model.RoutePermitGranted;
            if (!visible)
            {
                return;
            }

            GUILayout.Label("ONE GOOD BATCH", _headingStyle);
            string lot = model.SpareBearingLotQuantity > 0
                ? LastBearingState.SpareBearingLotId +
                  " · quantity " + model.SpareBearingLotQuantity
                : "not yet created";
            GUILayout.Label(
                "Recipe  " + model.SpareBearingRecipe + "\n" +
                "Phase  " + model.SpareBearingBatchPhase +
                "  ·  elapsed " + model.SpareBearingElapsedTicks +
                "/" + model.SpareBearingRequiredTicks +
                "  ·  remaining " + model.SpareBearingRemainingTicks + "\n" +
                "Lot  " + lot + "\n" +
                "Custody  " + model.SpareBearingLotCustody + "\n" +
                "Depot route permit  " +
                (model.RoutePermitGranted ? "GRANTED" : "LOCKED") + "\n" +
                "Future route toll  " + model.FutureRouteTollFuelUnits +
                " fuel",
                _bodyStyle);
            GUILayout.Label(
                "ONE-OFF BARTER · CARAVAN EXCHANGE CLOSED",
                _mutedStyle);
        }

        private void DrawCityNeedAndGrammar(LastBearingReadModel model)
        {
            GUILayout.Label("HOME BEFORE HORIZON · CITY BUILDING LAB", _headingStyle);
            if (!_controller!.CityNeedInspected)
            {
                GUILayout.Label(
                    "The red turbine stop and falling reserve are visible. " +
                    "Inspect them before taking action.",
                    _bodyStyle);
                if (GUILayout.Button("INSPECT FAILING WATER SYSTEM", _buttonStyle))
                {
                    _controller.InspectCityNeed();
                }

                return;
            }

            GUILayout.Label(
                "Observed: the turbine is failing and the reserve trend is " +
                FormatSigned(model.WaterTrendMilliPerSettlementTick) +
                " per settlement tick.",
                _bodyStyle);

            if (model.ExpeditionPhase != ExpeditionPhase.AtHome)
            {
                GUILayout.Label(
                    "The reversible city-grammar comparison is available at home.",
                    _mutedStyle);
                return;
            }

            GUILayout.Label(
                "Stage the same recycler-to-workshop service cell two ways, then move the same empty calibration sled. Both trials hold function, camera, residents, and canonical state constant. Neither selects or ratifies D-0030.",
                _mutedStyle);
            if (GUILayout.Button(
                    "TRIAL A · INDIVIDUAL BUILDINGS ON THREE SNAP PADS",
                    _buttonStyle))
            {
                _controller.SelectCityGrammarHypothesis(
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            }

            if (GUILayout.Button(
                    "TRIAL B · WHOLE SERVICE-CELL DISTRICT STAMP",
                    _buttonStyle))
            {
                _controller.SelectCityGrammarHypothesis(
                    LastBearingCityGrammarHypothesis.DistrictStamp);
            }

            if (_controller.CityGrammarHypothesis !=
                LastBearingCityGrammarHypothesis.Unselected)
            {
                GUILayout.Label(
                    "ACTIVE  " + _controller.CityGrammarHypothesis,
                    _bodyStyle);
                if (_controller.CityGrammarHypothesis ==
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid)
                {
                    GUILayout.Label(
                        "Active building  " + _controller.ActiveCityGrammarPiece +
                        ". Place both buildings on different pads, then connect their service link.",
                        _mutedStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("PLACE / MOVE ACTIVE", _buttonStyle))
                    {
                        _controller.ManipulateCityGrammarPrimary();
                    }

                    if (GUILayout.Button("SWITCH BUILDING", _buttonStyle))
                    {
                        _controller.ToggleCityGrammarTrialPiece();
                    }

                    GUILayout.EndHorizontal();
                    if (!_controller.CityGrammarLogisticsConnected &&
                        GUILayout.Button(
                            "CONNECT RECYCLER OUTPUT → WORKSHOP INPUT",
                            _buttonStyle))
                    {
                        _controller.ConnectCityGrammarLogistics();
                    }
                }
                else if (GUILayout.Button("PLACE / RESTAMP SERVICE CELL", _buttonStyle))
                {
                    _controller.ManipulateCityGrammarPrimary();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("ROTATE 90°", _buttonStyle))
                {
                    _controller.RotateCityGrammarPrimary();
                }

                if (GUILayout.Button("RESET ACTIVE TRIAL", _buttonStyle))
                {
                    _controller.ResetActiveCityGrammarTrial();
                }

                GUILayout.EndHorizontal();

                if (_controller.CityGrammarLogisticsConnected &&
                    _controller.CityGrammarDeliveryStage !=
                    LastBearingCityTrialDeliveryStage.DeliveredToWorkshop)
                {
                    string deliveryAction = _controller.CityGrammarDeliveryStage ==
                                            LastBearingCityTrialDeliveryStage.AtRecycler
                        ? "DISPATCH EMPTY CALIBRATION SLED"
                        : "DELIVER EMPTY SLED TO WORKSHOP";
                    if (GUILayout.Button(deliveryAction, _buttonStyle))
                    {
                        _controller.AdvanceCityGrammarDelivery();
                    }
                }

                if (_controller.CityGrammarDeliveryStage ==
                        LastBearingCityTrialDeliveryStage.DeliveredToWorkshop &&
                    _controller.CityGrammarPathRead ==
                        LastBearingCityTrialPathRead.Unrecorded)
                {
                    GUILayout.Label(
                        "Record the raw observation. There is no score and no automatic winner.",
                        _mutedStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("PATH READS CLEAR", _buttonStyle))
                    {
                        _controller.RecordCityGrammarPathRead(clear: true);
                    }

                    if (GUILayout.Button("PATH READS UNCLEAR", _buttonStyle))
                    {
                        _controller.RecordCityGrammarPathRead(clear: false);
                    }

                    GUILayout.EndHorizontal();
                }

                if (_controller.CityGrammarTrialReady)
                {
                    GUILayout.Label(
                        "OBSERVATION COMPLETE · switch trials to compare under the same camera.",
                        _bodyStyle);
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("LEAVE LAB", _buttonStyle))
                {
                    _controller.LeaveCityGrammarComparison();
                }

                if (GUILayout.Button("CLEAR BOTH TRIALS", _buttonStyle))
                {
                    _controller.ResetCityGrammarComparison();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Label(_controller.CityGrammarEvidence, _mutedStyle);
        }

        private void DrawPlanButton(
            string label,
            PreparationChoice preparation,
            VehicleModule module)
        {
            if (GUILayout.Button(label, _buttonStyle))
            {
                _controller!.ChoosePlan(preparation, module);
            }
        }

        private static string BuildInstrumentText(LastBearingReadModel model)
        {
            var text = new StringBuilder(512);
            text.Append("Phase  ").Append(model.ExpeditionPhase)
                .Append("  ·  tick ").Append(model.GlobalTick).AppendLine();
            text.Append("Water  ").Append(model.WaterMilli)
                .Append("  (").Append(FormatSigned(model.WaterTrendMilliPerSettlementTick))
                .Append(" / settlement tick)").AppendLine();
            text.Append("Parts  ").Append(model.PartsUnits)
                .Append("  ·  Fuel  ").Append(model.FuelUnits).AppendLine();
            text.Append("Turbine  ").Append(model.TurbineCondition)
                .Append("  ·  Vehicle  ").Append(model.VehicleConditionMilli)
                .AppendLine();
            text.Append("Plan  ").Append(model.PreparationChoice)
                .Append(" + ").Append(model.PlannedModule)
                .Append("  ·  module ").Append(model.VehicleModule).AppendLine();
            text.Append("Route  ").Append(model.RouteKind)
                .Append("  ").Append(model.RouteProgressTicks)
                .Append('/').Append(model.RouteTargetTicks)
                .Append("  ·  lateral ").Append(model.VehicleLateralMilli)
                .Append("/±")
                .Append(LastBearingBalanceV1.RoadLateralLimitMilli)
                .AppendLine();
            text.Append("Road verb  ").Append(model.RouteActionKind)
                .Append("  ·  operated ").Append(model.RouteActionUsed)
                .Append("  ·  Wreck Line ").Append(model.WreckLineGateTicks)
                .AppendLine();
            text.Append("Pump rotor  ").Append(model.HeavyCargoCustody)
                .AppendLine();
            text.Append("City improvement  ")
                .Append(model.InstalledCityImprovement)
                .AppendLine();
            text.Append("Faction  ").Append(model.FactionClaimState)
                .Append("  ").Append(model.FactionClaimProgressMilli)
                .Append("  ·  depot ").Append(model.DepotControl).AppendLine();
            text.Append("Access  ").Append(model.FactionAccessPolicy)
                .Append("  ·  aid ").Append(model.FactionAidPolicy).AppendLine();
            text.Append("Roster  ").Append(model.Composition)
                .Append("  ·  lead ").Append(model.AssignedResidentId ?? "unassigned")
                .AppendLine();
            text.Append("Forecast  water ").Append(FormatTicks(model.WaterZeroSettlementTicks))
                .Append("  ·  claim ").Append(FormatTicks(model.ClaimedFactionTicks))
                .Append("  ·  dust ").Append(FormatTicks(model.DustFrontCrisisTicks))
                .AppendLine();
            text.Append("Next city decision  ")
                .Append(model.NextCityDecision);
            return text.ToString();
        }

        private static string FormatSigned(long value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string FormatTicks(long? value)
        {
            return value.HasValue ? value.Value + " ticks" : "n/a";
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(new Color(0.055f, 0.058f, 0.055f, 0.95f)) },
                border = new RectOffset(1, 1, 1, 1)
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.77f, 0.42f) }
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.56f, 0.79f, 0.76f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(0.88f, 0.84f, 0.75f) }
            };
            _mutedStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.61f, 0.56f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                wordWrap = true,
                fixedHeight = 34f,
                margin = new RectOffset(0, 0, 3, 3)
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
