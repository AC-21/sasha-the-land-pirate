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
        private LastBearingFieldDesk? _fieldDesk;
        private GUIStyle? _panelStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _headingStyle;
        private GUIStyle? _bodyStyle;
        private GUIStyle? _mutedStyle;
        private GUIStyle? _buttonStyle;
        private Vector2 _scroll;

        public void Configure(
            LastBearingGameController controller,
            LastBearingFieldDesk? fieldDesk = null)
        {
            _controller = controller;
            _fieldDesk = fieldDesk;
        }

        private void OnGUI()
        {
            if (_controller == null)
            {
                return;
            }

            if (_fieldDesk?.OwnsCityOverview == true)
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
            if (_controller.HasActiveGame && _controller.RuntimeReadModel != null)
            {
                DrawActiveGame(_controller.RuntimeReadModel);
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

            DrawDustFrontAlert(model);

            // Keep the playable journey above the temporary development
            // instruments so the next verb never depends on scrolling through
            // internal state first.
            DrawPermitJob(permitJob);
            GUILayout.Space(12f);

            DrawGarageUpgrade(model);

            GUILayout.Label("NEXT MOVE", _headingStyle);
            DrawContextActions(model, permitJob);
            GUILayout.Space(10f);

            GUILayout.Label("EXACT CONTROLS", _headingStyle);
            GUILayout.Label(
                BuildControlsText(
                    model,
                    _controller.CanRecoverRoadPresentation,
                    _controller.IsDepotRepairCargoLoadAvailable,
                    _controller.IsDepotRepairCargoLoadQueued,
                    _controller.IsReturnCheckInAvailable,
                    _controller.IsPumpHallRepairAvailable,
                    _controller.IsWorkshopBatchStartAvailable,
                    _controller.IsWorkshopBarterAvailable,
                    _controller.IsGaragePlanIntentActive,
                    _controller.CityNeedInspected),
                _mutedStyle);
            GUILayout.Space(10f);

            GUILayout.Label("JOURNEY LEDGER", _headingStyle);
            GUILayout.Label(BuildJourneyLedgerText(model), _bodyStyle);
            GUILayout.Space(10f);

            DrawServiceControls(model);
            GUILayout.Space(10f);

            GUILayout.Label("DEVELOPMENT DIAGNOSTICS", _headingStyle);
            GUILayout.Label("CIVIC INSTRUMENTS", _headingStyle);
            GUILayout.Label(BuildInstrumentText(model), _bodyStyle);
            GUILayout.Space(10f);

            DrawCityNeedAndGrammar(model);
            GUILayout.Space(10f);

            DrawPresentationMode(model);
            GUILayout.Space(10f);

            DrawOneGoodBatch(model);
        }

        private void DrawDustFrontAlert(LastBearingReadModel model)
        {
            if (!model.IsDustFrontAcknowledgementRequired)
            {
                return;
            }

            GUILayout.Label("DUST FRONT · GLOBAL ALERT", _headingStyle);
            GUILayout.Label(
                model.DustFrontOutcome == DustFrontOutcome.Held
                    ? "HELD · Last Bearing kept the reserve above the recoverable line."
                    : "BREACHED · The failing turbine could not hold the dry line. Hot Shift stays stalled until turbine repair.",
                _bodyStyle);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled &&
                          _controller!.CanAcknowledgeDustFront;
            if (GUILayout.Button("ACKNOWLEDGE FRONT", _buttonStyle))
            {
                _controller.AcknowledgeDustFront();
            }

            GUI.enabled = wasEnabled;
            GUILayout.Space(12f);
        }

        private void DrawGarageUpgrade(LastBearingReadModel model)
        {
            LastBearingModeCoordinator? coordinator = _controller!.ModeCoordinator;
            if (coordinator?.HasActiveMode != true ||
                coordinator.CurrentMode != LastBearingPresentationMode.GarageBay)
            {
                return;
            }

            GUILayout.Label("RIG ARMOR · PATCHWORK SKID PLATE", _headingStyle);
            if (model.RigUpgrade == RigUpgrade.PatchworkSkidPlate)
            {
                GUILayout.Label(
                    "INSTALLED · round-trip condition loss reduced by " +
                    model.PatchworkSkidPlateProtectionMilli + ".",
                    _bodyStyle);
                if (model.ProjectedRoundTripConditionLossMilli > 0)
                {
                    GUILayout.Label(
                        "PROJECTED ROUND-TRIP LOSS · " +
                        model.ProjectedRoundTripConditionLossMilli,
                        _mutedStyle);
                }

                GUILayout.Space(10f);
                return;
            }

            GUILayout.Label(
                "Bolt settlement-cut plate beneath Sasha's scout for " +
                model.PatchworkSkidPlatePartsCostUnits +
                " reclaimed parts. It reduces the next round-trip condition " +
                "loss by " + model.PatchworkSkidPlateProtectionMilli + ".",
                _bodyStyle);
            if (_controller.IsPatchworkSkidPlateInstallQueued)
            {
                GUILayout.Label(
                    "INSTALLATION QUEUED · awaiting the next deterministic tick. " +
                    "Garage preparation remains unchanged.",
                    _mutedStyle);
            }
            else if (_controller.IsPatchworkSkidPlateInstallAvailable)
            {
                if (GUILayout.Button(
                        "INSTALL PATCHWORK SKID PLATE · " +
                        model.PatchworkSkidPlatePartsCostUnits + " PARTS",
                        _buttonStyle))
                {
                    _controller.InstallPatchworkSkidPlate();
                }
            }
            else
            {
                GUILayout.Label(
                    "Requires the working service cell, Sasha at home, no " +
                    "active transaction, and enough reclaimed parts.",
                    _mutedStyle);
            }

            GUILayout.Space(10f);
        }

        private void DrawServiceControls(LastBearingReadModel model)
        {
            GUILayout.Label("SERVICE", _headingStyle);
            GUILayout.BeginHorizontal();
            if (model.PauseCause == PauseCause.DustFrontAlert)
            {
                GUILayout.Label(
                    "AUTO-PAUSED · acknowledge the Dust Front verdict above",
                    _mutedStyle);
            }
            else if (model.PauseCause == PauseCause.AutoAlert)
            {
                GUILayout.Label(
                    "AUTO-PAUSED · choose the depot response above",
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

        private void DrawEmergencyCisternAction(
            LastBearingReadModel model)
        {
            if (!model.SliceInfrastructureActive ||
                model.EmergencyStoragePadIndex < 0 ||
                model.PreparationChoice == PreparationChoice.Unselected ||
                model.PlannedModule == VehicleModule.None ||
                model.ExpeditionPhase != ExpeditionPhase.AtHome)
            {
                return;
            }

            GUILayout.Label("EMERGENCY CISTERN", _headingStyle);
            if (model.EmergencyCisternCharged)
            {
                GUILayout.Label(
                    "CHARGED · the one authorized 10.000-water fill is in storage.",
                    _bodyStyle);
                GUILayout.Space(10f);
                return;
            }

            GUILayout.Label(
                "One full fill only: spend 1 fuel, preserve Sasha's planned " +
                "route reserve, and add +10.000 water before the Dust Front.",
                _bodyStyle);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled &&
                          _controller!.CanPumpEmergencyCistern;
            if (GUILayout.Button(
                    "PUMP EMERGENCY CISTERN · 1 FUEL · +10.000 WATER · ONE FILL",
                    _buttonStyle))
            {
                _controller!.PumpEmergencyCistern();
            }

            GUI.enabled = wasEnabled;
            if (!model.IsEmergencyCisternPumpAvailable)
            {
                GUILayout.Label(
                    "Requires the commissioned operator, an idle Hot Shift, " +
                    "an unresolved Dust Front, room for the full fill, and " +
                    "fuel beyond the planned route reserve.",
                    _mutedStyle);
            }

            GUILayout.Space(10f);
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
                if (GUILayout.Button(
                        "INSPECT FAILING WATER SYSTEM",
                        _buttonStyle))
                {
                    _controller.InspectCityNeed();
                }

                return;
            }

            DrawEmergencyCisternAction(model);

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                model.PreparationChoice == PreparationChoice.Unselected &&
                model.TurbineCondition == TurbineCondition.Failing)
            {
                if (IsWorkingServiceCellObjective(model.NextObjective))
                {
                    DrawWorkingServiceCellActions(model);
                    return;
                }

                if (model.NextObjective == "activate-slice-infrastructure")
                {
                    if (!_controller.HasCompletedCityGrammarObservation)
                    {
                        GUILayout.Label(
                            "Complete either neutral service-cell trial in the development diagnostics below: stage the same recycler and workshop, move one empty calibration sled, and record whether the path reads clearly.",
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

                if (_controller.IsGaragePlanIntentActive)
                {
                    GUILayout.Label(
                        "CITY POSTURE · " +
                        _controller.GaragePreparationIntent +
                        "\nThis is an uncommitted local note. No clock, cost, inventory, save, or vehicle state has changed.",
                        _bodyStyle);

                    if (!_controller.IsGaragePlanCommitAvailable)
                    {
                        GUILayout.Label(
                            "Return to Sasha's fixed garage cutaway to choose the rig module.",
                            _mutedStyle);
                        if (GUILayout.Button(
                                "RETURN TO SASHA'S GARAGE",
                                _buttonStyle))
                        {
                            _controller.OpenGarageBay();
                        }

                        if (GUILayout.Button(
                                "CANCEL UNCOMMITTED PLAN",
                                _buttonStyle))
                        {
                            _controller.CancelGaragePlan();
                        }

                        return;
                    }

                    GUILayout.Label(
                        "Both authored module stands are valid. Commit exactly one at Sasha's rig.",
                        _mutedStyle);
                    DrawGarageModuleButton(VehicleModule.WinchAssembly);
                    DrawGarageModuleButton(VehicleModule.SealedRangeTank);
                    if (GUILayout.Button(
                            "CANCEL · RETURN TO CITY",
                            _buttonStyle))
                    {
                        _controller.CancelGaragePlan();
                    }

                    return;
                }

                GUILayout.Label(
                    "Choose the city's preparation posture first. This opens Sasha's garage without queuing a command; both rig modules remain valid.",
                    _mutedStyle);
                DrawPreparationButton(
                    "WORKSHOP PUSH\nLEAVE SOONER · ASK HOME TO RUN LEAN",
                    PreparationChoice.WorkshopPush);
                DrawPreparationButton(
                    "RECOMMENDED FIRST RUN · CIVIC BUFFER\n" +
                    "PAIR WITH WINCH · PROTECT HOME · RISK A LATER CLAIM",
                    PreparationChoice.CivicBuffer);
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

                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    GUILayout.Label(
                        "The fitted skid plate can belly under the wreck and free one fixed bundle of frame rails. It occupies one ordinary cargo unit until home check-in.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "E — Recover frame rails · +4 reclaimed parts at home",
                            _buttonStyle))
                    {
                        _controller!.RecoverWreckLineFrameRails();
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
                if (!_controller!.IsDepotDecisionAvailable)
                {
                    GUILayout.Label(
                        "DEPOT RESPONSE UNAVAILABLE · finish the queued action " +
                        "or return to the depot encounter.",
                        _mutedStyle);
                    return;
                }

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
                            ? "RECOMMENDED FIRST RUN · TAKE THE CERAMIC BEARING\nOPENS ONE GOOD BATCH"
                            : "TAKE THE CERAMIC BEARING\nCERAMIC REPAIR + AGGRIEVED DEPOT",
                        _buttonStyle))
                {
                    _controller!.ResolveDepot(cooperate: false);
                }

                return;
            }

            if (model.IsRepairCargoLoadAvailable)
            {
                bool fieldSleeve =
                    model.RepairCargoKind == RepairCargoKind.FieldSleeve;
                GUILayout.Label(
                    fieldSleeve
                        ? "The faction field sleeve remains on its service stand. Cargo custody has not changed yet."
                        : model.RepairCargoCustody == RepairCargoCustody.Faction
                            ? "The faction-held ceramic bearing remains on the service stand. Cargo custody has not changed yet."
                            : "The unclaimed ceramic bearing remains in the depot cradle. Cargo custody has not changed yet.",
                    _bodyStyle);
                if (_controller!.IsDepotRepairCargoLoadQueued)
                {
                    GUILayout.Label(
                        "REPAIR CARGO LOAD QUEUED · custody changes on the authoritative tick.",
                        _mutedStyle);
                    return;
                }

                if (!_controller.IsDepotRepairCargoLoadAvailable)
                {
                    GUILayout.Label(
                        "REPAIR CARGO LOAD UNAVAILABLE · return to the depot source after the current action.",
                        _mutedStyle);
                    return;
                }

                if (GUILayout.Button(
                        fieldSleeve
                            ? "LOAD FIELD SLEEVE · E / GAMEPAD SOUTH"
                            : "LOAD CERAMIC BEARING · E / GAMEPAD SOUTH",
                        _buttonStyle))
                {
                    _controller!.LoadDepotRepairCargo();
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

            if (_controller!.IsReturnCheckInAvailable)
            {
                GUILayout.Label(
                    model.FrameRailSalvageCustody ==
                        FrameRailSalvageCustody.Vehicle
                        ? "Sasha is seated at the fixed return apron with the exact repair cargo and Wreck Line frame rails still on the scout. Check-in adds +4 reclaimed parts."
                        : "Sasha is seated at the fixed return apron with the exact repair cargo still on the scout.",
                    _bodyStyle);
                if (GUILayout.Button(
                        "CHECK IN LOADED RETURN · E / GAMEPAD SOUTH",
                        _buttonStyle))
                {
                    _controller!.CompleteReturn();
                }

                return;
            }

            if (_controller.IsTurbineRepairReady)
            {
                if (!_controller.IsPumpHallRepairAvailable)
                {
                    GUILayout.Label(
                        "The repair remains on Sasha's cargo socket. Open the pump hall to seat it at the failing civic organ.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "OPEN PUMP HALL REPAIR LINE",
                            _buttonStyle))
                    {
                        _controller.OpenPumpHallRepair();
                    }
                }
                else
                {
                    GUILayout.Label(
                        model.RepairCargoKind == RepairCargoKind.CeramicBearing
                            ? "The empty keyed target is ready for the ceramic bearing."
                            : "The field sleeve will be consumed by this repair.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "SEAT TURBINE REPAIR · E / GAMEPAD SOUTH",
                            _buttonStyle))
                    {
                        _controller.RepairTurbine();
                    }
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
                if (!_controller.IsWorkshopBatchStartAvailable)
                {
                    GUILayout.Label(
                        "The machine shop can commit exactly one bounded spare-bearing batch while preserving the civic parts reserve. Open the fixed workshop line before committing it.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "OPEN MACHINE SHOP · ONE GOOD BATCH",
                            _buttonStyle))
                    {
                        _controller.OpenOneGoodBatchWorkshop();
                    }
                }
                else
                {
                    GUILayout.Label(
                        "Two approved input parts are staged at the selected machine. Press E or gamepad south to commit them to One Good Batch.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "START ONE SPARE-BEARING LOT",
                            _buttonStyle))
                    {
                        _controller.StartSpareBearingBatch();
                    }
                }

                return;
            }

            if (model.IsSpareBearingBarterAvailable)
            {
                if (!_controller.IsWorkshopBarterAvailable)
                {
                    GUILayout.Label(
                        "The completed physical lot can cross the claims wicket exactly once for the fixed depot corridor permit. Open the fixed workshop line before handing it over.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "OPEN CLAIMS WICKET · PHYSICAL LOT",
                            _buttonStyle))
                    {
                        _controller.OpenOneGoodBatchWorkshop();
                    }
                }
                else
                {
                    GUILayout.Label(
                        "The physical lot remains at workshop output. Press E or gamepad south to hand it across the selected claims wicket.",
                        _bodyStyle);
                    if (GUILayout.Button(
                            "BARTER LOT FOR DEPOT ROUTE PERMIT",
                            _buttonStyle))
                    {
                        _controller.BarterSpareBearingLot();
                    }
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

        private void DrawWorkingServiceCellActions(
            LastBearingReadModel model)
        {
            if (_controller!.HasPendingPlayerCommands)
            {
                GUILayout.Label(
                    "SERVICE-CELL ACTION QUEUED · let the next settlement tick " +
                    "accept it before issuing another canonical action.",
                    _mutedStyle);
                return;
            }

            if (_controller.HasCityBuildingPreview)
            {
                DrawCityBuildingPreview(model);
                return;
            }

            switch (model.NextObjective)
            {
                case "place-city-recycler":
                    DrawSelectCityBuilding(
                        "SELECT RECYCLER · 2 PARTS",
                        CityBuildingKind.Recycler);
                    return;
                case "place-city-machine-shop":
                    DrawSelectCityBuilding(
                        "SELECT MACHINE SHOP · 3 PARTS",
                        CityBuildingKind.MachineShop);
                    return;
                case "place-city-emergency-storage":
                    DrawSelectCityBuilding(
                        "SELECT EMERGENCY STORAGE · 1 PART",
                        CityBuildingKind.EmergencyStorage);
                    return;
                case "connect-city-service-link":
                    GUILayout.Label(
                        "Optional: reposition any placed building for free " +
                        "before the permanent link lock.",
                        _mutedStyle);
                    DrawSelectCityBuilding(
                        "MOVE RECYCLER · FREE",
                        CityBuildingKind.Recycler);
                    DrawSelectCityBuilding(
                        "MOVE MACHINE SHOP · FREE",
                        CityBuildingKind.MachineShop);
                    DrawSelectCityBuilding(
                        "MOVE EMERGENCY STORAGE · FREE",
                        CityBuildingKind.EmergencyStorage);
                    GUILayout.Label(
                        "Locking spends 1 reclaimed part and permanently fixes " +
                        "all three pads and orientations. V0 has no demolition " +
                        "or refund.",
                        _bodyStyle);
                    if (_controller.CanConnectCityServiceLink &&
                        GUILayout.Button(
                            "LOCK SERVICE LINK · 1 PART",
                            _buttonStyle))
                    {
                        _controller.ConnectCityServiceLink();
                    }
                    else if (!_controller.CanConnectCityServiceLink)
                    {
                        GUILayout.Label(
                            "The permanent link needs all three buildings and " +
                            "1 reclaimed part.",
                            _mutedStyle);
                    }

                    return;
                case "staff-city-service-cell":
                    GUILayout.Label(
                        "Assign one eligible resident to the machine-shop slot. " +
                        "Human and utility-robot operators are neutral here; " +
                        "neither grants a V0 bonus.",
                        _bodyStyle);
                    if (_controller.CanAssignCityServiceHuman &&
                        GUILayout.Button(
                            "STAFF HUMAN · NEUTRAL",
                            _buttonStyle))
                    {
                        _controller.AssignCityServiceResident(
                            ResidentRoster.HumanResidentId);
                    }

                    if (_controller.CanAssignCityServiceRobot &&
                        GUILayout.Button(
                            "STAFF UTILITY ROBOT · NEUTRAL",
                            _buttonStyle))
                    {
                        _controller.AssignCityServiceResident(
                            ResidentRoster.RobotResidentId);
                    }

                    return;
                case "advance-city-service-sled":
                    bool atRecycler =
                        model.CityDeliveryStage ==
                        CityDeliveryStage.AtRecycler;
                    GUILayout.Label(
                        atRecycler
                            ? "The first advance moves the calibration sled " +
                              "into transit; it returns no parts yet."
                            : "The commissioning delivery completes the route and " +
                              "returns exactly 2 reclaimed parts once.",
                        _bodyStyle);
                    if (_controller.CanAdvanceCityServiceSled &&
                        GUILayout.Button(
                            atRecycler
                                ? "ADVANCE PARTS SLED"
                                : "COMMISSIONING DELIVERY · ONCE",
                            _buttonStyle))
                    {
                        _controller.AdvanceCityServiceSled();
                    }

                    return;
            }
        }

        private void DrawCityBuildingPreview(LastBearingReadModel model)
        {
            CityBuildingKind building = _controller!.CityPreviewBuilding;
            bool moving = CityBuildingPad(model, building) >= 0;
            GUILayout.Label(
                "PREVIEW · " + FormatCityBuilding(building) +
                " · PAD " + (_controller.CityPreviewPadIndex + 1) +
                " · " + (_controller.CityPreviewQuarterTurns * 90) + "°\n" +
                "Pad changes, quarter-turns, and cancellation are free.",
                _bodyStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PREVIOUS PAD", _buttonStyle))
            {
                _controller.MoveCityBuildingPreview(-1);
            }

            if (GUILayout.Button("NEXT PAD", _buttonStyle))
            {
                _controller.MoveCityBuildingPreview(1);
            }

            GUILayout.EndHorizontal();
            if (GUILayout.Button("ROTATE 90°", _buttonStyle))
            {
                _controller.RotateCityBuildingPreview();
            }

            if (_controller.CanPlaceCityBuildingPreview)
            {
                string placementLabel = moving
                    ? "MOVE " + FormatCityBuilding(building) + " · FREE"
                    : "PLACE " + FormatCityBuilding(building) + " · " +
                      _controller.CityPreviewPartsCost +
                      (_controller.CityPreviewPartsCost == 1
                          ? " PART"
                          : " PARTS");
                if (GUILayout.Button(placementLabel, _buttonStyle))
                {
                    _controller.PlaceCityBuildingPreview();
                }
            }
            else
            {
                GUILayout.Label(
                    "That pad is occupied or the required reclaimed parts are " +
                    "not available. Choose another pad or cancel.",
                    _mutedStyle);
            }

            if (GUILayout.Button("CANCEL PREVIEW · NO PARTS SPENT", _buttonStyle))
            {
                _controller.CancelCityBuildingPreview();
            }
        }

        private void DrawSelectCityBuilding(
            string label,
            CityBuildingKind building)
        {
            if (GUILayout.Button(label, _buttonStyle))
            {
                _controller!.SelectCityBuildingPreview(building);
            }
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
                    "Use the primary action above to inspect them before " +
                    "taking action.",
                    _bodyStyle);
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

        private void DrawPreparationButton(
            string label,
            PreparationChoice preparation)
        {
            if (GUILayout.Button(label, _buttonStyle))
            {
                _controller!.BeginGaragePlan(preparation);
            }
        }

        private void DrawGarageModuleButton(VehicleModule module)
        {
            string label = BuildGarageModuleLabel(
                _controller!.GaragePreparationIntent,
                module);
            if (GUILayout.Button(label, _buttonStyle))
            {
                _controller.CommitGaragePlan(module);
            }
        }

        private static string BuildGarageModuleLabel(
            PreparationChoice preparation,
            VehicleModule module)
        {
            bool recommended = preparation == PreparationChoice.CivicBuffer &&
                               module == VehicleModule.WinchAssembly;
            return module == VehicleModule.WinchAssembly
                ? (recommended ? "RECOMMENDED FIRST RUN · " : string.Empty) +
                  "COMMIT WINCH ASSEMBLY\n" +
                  "RECOVER THE PUMP ROTOR · START THE PREPARATION CLOCK"
                : "COMMIT SEALED RANGE TANK\n" +
                  "CARRY WATER OR FUEL · START THE PREPARATION CLOCK";
        }

        private static string BuildControlsText(
            LastBearingReadModel model,
            bool canRecoverRoadPresentation,
            bool isDepotCargoLoadAvailable,
            bool isDepotCargoLoadQueued,
            bool isReturnCheckInAvailable,
            bool isPumpHallRepairAvailable,
            bool isWorkshopBatchStartAvailable,
            bool isWorkshopBarterAvailable,
            bool isGaragePlanIntentActive,
            bool cityNeedInspected)
        {
            const string serviceControls =
                "\nP · pause  F5 · save  F9 · load";
            if (model.IsDustFrontAcknowledgementRequired)
            {
                return "Click ACKNOWLEDGE FRONT above · accept the Held or " +
                       "Breached verdict before settlement clocks resume." +
                       serviceControls;
            }

            if (model.AssignedResidentId == null)
            {
                return "Click ASSIGN DEFAULT EXPEDITION LEAD above · the " +
                       "resident enters the manifest without changing colony " +
                       "mechanics." + serviceControls;
            }

            if (!cityNeedInspected)
            {
                return "Click INSPECT FAILING WATER SYSTEM above · the stopped " +
                       "turbine becomes the active city need." +
                       serviceControls;
            }

            string? workingCellControls =
                BuildWorkingServiceCellControlsText(model);
            if (workingCellControls != null)
            {
                return workingCellControls + serviceControls;
            }

            if (model.NextObjective == "activate-slice-infrastructure")
            {
                return "Use TRIAL A or TRIAL B in the diagnostics below, then " +
                       "click BRING THE SAME RECYCLER + MACHINE SHOP ONLINE · " +
                       "the service cell becomes available without selecting a " +
                       "layout winner." + serviceControls;
            }

            if (model.IsWreckLineModulePointAvailable)
            {
                return (model.RouteActionKind == RouteActionKind.DeployWinch
                        ? "Press E / gamepad south · deploy the winch and put " +
                          "the pump rotor on Sasha's scout."
                        : "Press E / gamepad south · seal the scout and cross " +
                          "the dust exposure without the pump rotor.") +
                       serviceControls;
            }

            if (model.IsWreckLineFrameRailRecoveryAvailable)
            {
                return "Press E / gamepad south · recover the Wreck Line " +
                       "frame rails for +4 reclaimed parts at home." +
                       serviceControls;
            }

            if (model.IsDepotApproachRecoveryAvailable)
            {
                return "Press E / gamepad south · seat the recovery bridle " +
                       "and open the depot decision." + serviceControls;
            }

            if (model.IsRepairCargoLoadAvailable)
            {
                if (isDepotCargoLoadQueued)
                {
                    return "Repair cargo lift queued · custody changes on " +
                           "the authoritative tick." + serviceControls;
                }

                if (!isDepotCargoLoadAvailable)
                {
                    return "Return to the physical depot source after the " +
                           "current action." + serviceControls;
                }

                return model.RepairCargoKind == RepairCargoKind.FieldSleeve
                    ? "Press E / gamepad south · move the field sleeve from " +
                      "the faction stand into Sasha's cargo socket." +
                      serviceControls
                    : "Press E / gamepad south · move the ceramic bearing " +
                      "from its depot source into Sasha's cargo socket." +
                      serviceControls;
            }

            if (isReturnCheckInAvailable)
            {
                return "Press E / gamepad south · credit the loaded return to " +
                       "Last Bearing and open the repair route." +
                       serviceControls;
            }

            if (isPumpHallRepairAvailable)
            {
                return "Press E / gamepad south · install the carried repair " +
                       "and reverse the failing water trend." +
                       serviceControls;
            }

            if (isWorkshopBatchStartAvailable)
            {
                return "Press E / gamepad south · commit two parts to One " +
                       "Good Batch and start its settlement clock." +
                       serviceControls;
            }

            if (isWorkshopBarterAvailable)
            {
                return "Press E / gamepad south · hand the physical lot across " +
                       "the claims wicket for the depot route permit." +
                       serviceControls;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.Outbound ||
                model.ExpeditionPhase == ExpeditionPhase.Returning)
            {
                return "Hold W / right trigger · advance the route\n" +
                       "S / left trigger · brake, then reverse locally\n" +
                       "A/D / left stick · steer; leaving the safe lane costs " +
                       "rig condition\n" +
                       "Space / LB · handbrake" +
                       (canRecoverRoadPresentation
                           ? "\nR / gamepad north · recover the local rig to " +
                             "the current route marker"
                           : "\nLocal rig recovery is unavailable") +
                       serviceControls;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                if (model.RepairCargoKind == RepairCargoKind.None)
                {
                    return "Face the two depot stations · LEFT / RIGHT or " +
                           "D-pad chooses; E / gamepad south commits. " +
                           "COOPERATE brings a maintenance promise; TAKE " +
                           "brings the ceramic bearing and a grievance." +
                           serviceControls;
                }

                if (model.VehicleModule == VehicleModule.SealedRangeTank &&
                    model.LiquidCargoKind == LiquidCargoKind.None)
                {
                    return "Click LOAD WATER or LOAD FUEL above · the chosen " +
                           "liquid becomes part of the frozen return payload." +
                           serviceControls;
                }

                return "Click FREEZE PAYLOAD + RETURN above · lock the exact " +
                       "cargo and consequences, then start the road home." +
                       serviceControls;
            }

            if (isGaragePlanIntentActive &&
                model.PreparationChoice == PreparationChoice.Unselected)
            {
                return "Click one rig module above · the choice commits its " +
                       "costs and starts the preparation clock. Nothing is " +
                       "auto-selected." + serviceControls;
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                (model.PreparationPhase == PreparationPhase.Ready ||
                 model.PreparationPhase == PreparationPhase.Committed) &&
                model.TurbineCondition == TurbineCondition.Failing &&
                model.RepairCargoKind == RepairCargoKind.None)
            {
                return "Click COMMIT MANIFEST + DEPART above · debit the exact " +
                       "fuel and cargo manifest and begin the outbound route." +
                       serviceControls;
            }

            if (model.PreparationPhase == PreparationPhase.Preparing)
            {
                return "Keep the settlement unpaused · preparation advances " +
                       "on the settlement clock; use the view buttons below " +
                       "while the crew works." + serviceControls;
            }

            if (model.SpareBearingBatchPhase == SpareBearingBatchPhase.InProgress)
            {
                return "Keep the settlement unpaused · the machine advances " +
                       "the committed batch; completion moves one physical lot " +
                       "to workshop output." + serviceControls;
            }

            return "Click the highlighted action above.\n" +
                   "WASD · camera pan  Q/E · rotate\n" +
                   "Mouse wheel · zoom  RMB · orbit" +
                   serviceControls;
        }

        private static string? BuildWorkingServiceCellControlsText(
            LastBearingReadModel model)
        {
            switch (model.NextObjective)
            {
                case "place-city-recycler":
                    return "Click SELECT RECYCLER · 2 PARTS above; choose one " +
                           "of five pads, rotate if wanted, then click PLACE " +
                           "RECYCLER · 2 PARTS. Preview and cancel are free.";
                case "place-city-machine-shop":
                    return "Click SELECT MACHINE SHOP · 3 PARTS above; choose " +
                           "a free pad, rotate if wanted, then click PLACE " +
                           "MACHINE SHOP · 3 PARTS. Preview and cancel are free.";
                case "place-city-emergency-storage":
                    return "Click SELECT EMERGENCY STORAGE · 1 PART above; " +
                           "choose a free pad, rotate if wanted, then click " +
                           "PLACE EMERGENCY STORAGE · 1 PART. Preview and " +
                           "cancel are free.";
                case "connect-city-service-link":
                    return "Optionally move any building for free, then click " +
                           "LOCK SERVICE LINK · 1 PART above. Locking " +
                           "permanently fixes every pad and orientation.";
                case "staff-city-service-cell":
                    switch (model.Composition)
                    {
                        case ColonyComposition.HumanOnly:
                            return "Click STAFF HUMAN · NEUTRAL above; fill the " +
                                   "one machine-shop slot with no V0 bonus.";
                        case ColonyComposition.RobotOnly:
                            return "Click STAFF UTILITY ROBOT · NEUTRAL above; " +
                                   "fill the one machine-shop slot with no V0 bonus.";
                        default:
                            return "Click STAFF HUMAN · NEUTRAL or STAFF UTILITY " +
                                   "ROBOT · NEUTRAL above; either fills the same " +
                                   "one machine-shop slot with no V0 bonus.";
                    }
                case "advance-city-service-sled":
                    return model.CityDeliveryStage == CityDeliveryStage.AtRecycler
                        ? "Click ADVANCE PARTS SLED above; the first advance " +
                          "moves the calibration sled into transit and returns " +
                          "no parts yet."
                        : "Click COMMISSIONING DELIVERY · ONCE above; the second " +
                          "advance completes commissioning and returns exactly " +
                          "2 reclaimed parts once.";
                default:
                    return null;
            }
        }

        private static bool IsWorkingServiceCellObjective(string objective)
        {
            return objective == "place-city-recycler" ||
                   objective == "place-city-machine-shop" ||
                   objective == "place-city-emergency-storage" ||
                   objective == "connect-city-service-link" ||
                   objective == "staff-city-service-cell" ||
                   objective == "advance-city-service-sled";
        }

        private static int CityBuildingPad(
            LastBearingReadModel model,
            CityBuildingKind building)
        {
            switch (building)
            {
                case CityBuildingKind.Recycler:
                    return model.RecyclerPadIndex;
                case CityBuildingKind.MachineShop:
                    return model.MachineShopPadIndex;
                case CityBuildingKind.EmergencyStorage:
                    return model.EmergencyStoragePadIndex;
                default:
                    return LastBearingState.UnplacedCityPadIndex;
            }
        }

        private static string FormatCityBuilding(CityBuildingKind building)
        {
            switch (building)
            {
                case CityBuildingKind.Recycler:
                    return "RECYCLER";
                case CityBuildingKind.MachineShop:
                    return "MACHINE SHOP";
                case CityBuildingKind.EmergencyStorage:
                    return "EMERGENCY STORAGE";
                default:
                    return "CITY BUILDING";
            }
        }

        private static string BuildJourneyLedgerText(
            LastBearingReadModel model)
        {
            var text = new StringBuilder(320);
            text.Append("ROUTE  ").Append(FormatJourneyRoute(model)).AppendLine();
            text.Append("RIG  ")
                .Append(FormatPlayerModule(model.VehicleModule))
                .Append(" · condition ")
                .Append(model.VehicleConditionMilli)
                .Append('/')
                .Append(LastBearingBalanceV1.StartingVehicleConditionMilli)
                .Append(" · ")
                .Append(FormatCondition(model.VehicleConditionMilli));
            if (model.RigUpgrade == RigUpgrade.PatchworkSkidPlate)
            {
                text.Append(" · patchwork skid plate +")
                    .Append(model.PatchworkSkidPlateProtectionMilli)
                    .Append(" protection");
            }

            text.AppendLine();
            text.Append("CARGO  ").Append(FormatJourneyCargo(model)).AppendLine();
            text.Append("CONSEQUENCE  ").Append(FormatJourneyConsequence(model));
            return text.ToString();
        }

        private static string FormatJourneyRoute(LastBearingReadModel model)
        {
            switch (model.ExpeditionPhase)
            {
                case ExpeditionPhase.Outbound:
                    return "outbound " + model.RouteProgressTicks + '/' +
                           model.RouteTargetTicks + " · lane " +
                           model.VehicleLateralMilli + "/±" +
                           LastBearingBalanceV1.RoadLateralLimitMilli;
                case ExpeditionPhase.AtDepot:
                    return "at the Last Bearing depot";
                case ExpeditionPhase.Returning:
                    return "homebound " + model.RouteProgressTicks + '/' +
                           model.RouteTargetTicks + " · lane " +
                           model.VehicleLateralMilli + "/±" +
                           LastBearingBalanceV1.RoadLateralLimitMilli;
                case ExpeditionPhase.Returned:
                    return "at the fixed return apron";
                default:
                    return "home at Last Bearing";
            }
        }

        private static string FormatPlayerModule(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return "winch fitted";
                case VehicleModule.SealedRangeTank:
                    return "sealed range tank fitted";
                default:
                    return "no expedition module fitted";
            }
        }

        private static string FormatCondition(long conditionMilli)
        {
            if (conditionMilli <=
                LastBearingBalanceV1.MinimumReturnVehicleConditionMilli)
            {
                return "critical handling";
            }

            return conditionMilli <
                LastBearingBalanceV1.StartingVehicleConditionMilli
                    ? "worn handling"
                    : "healthy handling";
        }

        private static string FormatJourneyCargo(LastBearingReadModel model)
        {
            var cargo = new StringBuilder(128);
            if (model.HeavyCargoKind == HeavyCargoKind.PumpRotor)
            {
                AppendCargoItem(
                    cargo,
                    "pump rotor " + FormatHeavyCargoCustody(
                        model.HeavyCargoCustody));
            }

            if (model.RepairCargoKind != RepairCargoKind.None)
            {
                AppendCargoItem(
                    cargo,
                    FormatRepairCargo(model.RepairCargoKind) + " " +
                    FormatRepairCargoCustody(model.RepairCargoCustody));
            }

            if (model.FrameRailSalvageCustody !=
                FrameRailSalvageCustody.None)
            {
                AppendCargoItem(
                    cargo,
                    "frame rails " + FormatFrameRailSalvageCustody(
                        model.FrameRailSalvageCustody));
            }

            if (model.LiquidCargoKind != LiquidCargoKind.None)
            {
                AppendCargoItem(
                    cargo,
                    model.LiquidCargoKind == LiquidCargoKind.Water
                        ? "emergency water " +
                          FormatLiquidCargoCustody(model.LiquidCargoCustody)
                        : "return fuel " +
                          FormatLiquidCargoCustody(model.LiquidCargoCustody));
            }

            return cargo.Length == 0 ? "empty" : cargo.ToString();
        }

        private static void AppendCargoItem(StringBuilder text, string item)
        {
            if (text.Length > 0)
            {
                text.Append(" · ");
            }

            text.Append(item);
        }

        private static string FormatHeavyCargoCustody(
            HeavyCargoCustody custody)
        {
            switch (custody)
            {
                case HeavyCargoCustody.Depot:
                    return "waiting at the Wreck Line";
                case HeavyCargoCustody.Vehicle:
                    return "on Sasha's scout";
                case HeavyCargoCustody.Settlement:
                    return "staged at Last Bearing";
                case HeavyCargoCustody.InstalledAtAuxiliaryPump:
                    return "installed at the auxiliary pump";
                default:
                    return "not recovered";
            }
        }

        private static string FormatRepairCargo(RepairCargoKind kind)
        {
            return kind == RepairCargoKind.FieldSleeve
                ? "field sleeve"
                : "ceramic bearing";
        }

        private static string FormatRepairCargoCustody(
            RepairCargoCustody custody)
        {
            switch (custody)
            {
                case RepairCargoCustody.Depot:
                    return "at the depot cradle";
                case RepairCargoCustody.Faction:
                    return "at the faction stand";
                case RepairCargoCustody.Vehicle:
                    return "on Sasha's scout";
                case RepairCargoCustody.Turbine:
                    return "installed in the turbine";
                case RepairCargoCustody.Consumed:
                    return "consumed by the repair";
                default:
                    return "not yet claimed";
            }
        }

        private static string FormatFrameRailSalvageCustody(
            FrameRailSalvageCustody custody)
        {
            switch (custody)
            {
                case FrameRailSalvageCustody.WreckLine:
                    return "waiting at the Wreck Line";
                case FrameRailSalvageCustody.Vehicle:
                    return "strapped to Sasha's scout";
                case FrameRailSalvageCustody.Credited:
                    return "credited as +4 reclaimed parts";
                default:
                    return "not recovered";
            }
        }

        private static string FormatJourneyConsequence(
            LastBearingReadModel model)
        {
            string civic = model.TurbineCondition != TurbineCondition.Failing
                ? "water recovering at " +
                  FormatSigned(model.WaterTrendMilliPerSettlementTick) +
                  " per settlement tick"
                : "turbine failing · water trend " +
                  FormatSigned(model.WaterTrendMilliPerSettlementTick) +
                  " per settlement tick";
            if (model.FactionClaimState == FactionClaimState.Aggrieved)
            {
                return civic + " · depot aggrieved · grievance " +
                       model.FactionGrievance +
                       (model.FutureRouteTollFuelUnits > 0
                           ? " · next passage +" +
                             model.FutureRouteTollFuelUnits + " fuel"
                           : " · future access consequence pending");
            }

            if (model.FactionClaimState == FactionClaimState.Cooperating ||
                model.MaintenanceObligationActive)
            {
                return civic + " · depot cooperation · trust " +
                       model.FactionTrust +
                       (model.MaintenanceDue
                           ? " · field-sleeve service due"
                           : " · field-sleeve maintenance promise active");
            }

            if (model.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                return civic + " · depot choice not yet carried home";
            }

            return civic;
        }

        private static string FormatLiquidCargoCustody(
            LiquidCargoCustody custody)
        {
            return custody == LiquidCargoCustody.Settlement
                ? "credited at Last Bearing"
                : "in the range tank";
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
            text.Append("Rig upgrade  ").Append(model.RigUpgrade)
                .Append("  ·  projected round-trip loss ")
                .Append(model.ProjectedRoundTripConditionLossMilli)
                .AppendLine();
            text.Append("Hot shift  ")
                .Append(FormatHotShift(model))
                .AppendLine();
            text.Append("Emergency cistern  ")
                .Append(model.EmergencyCisternCharged
                    ? "charged · one full fill consumed"
                    : model.IsEmergencyCisternPumpAvailable
                        ? "ready · 1 fuel / +10.000 water"
                        : "uncharged · prerequisites unmet")
                .AppendLine();
            text.Append("Dust front  ")
                .Append(model.DustFrontOutcome)
                .Append("  ·  acknowledgement ")
                .Append(model.IsDustFrontAcknowledgementRequired
                    ? "required"
                    : "clear")
                .AppendLine();
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
            text.Append("Repair cargo  ").Append(model.RepairCargoKind)
                .Append("  ·  custody ").Append(model.RepairCargoCustody)
                .AppendLine();
            text.Append("Frame rails  ")
                .Append(model.FrameRailSalvageCustody)
                .Append("  ·  value +")
                .Append(model.FrameRailSalvagePartsUnits)
                .Append(" parts  ·  cargo ")
                .Append(model.FrameRailSalvageCargoUnits)
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

        private static string FormatHotShift(LastBearingReadModel model)
        {
            if (model.HotShiftPhase == HotShiftPhase.InProgress)
            {
                return (model.IsHotShiftStalledByDustFront
                        ? "front-stalled · turbine repair required · no added water draw"
                        : model.IsHotShiftStalledByWorkshopPush
                            ? "stalled · operator borrowed · no added water draw"
                            : "working · -0.010 water / settlement tick") +
                    " · " + model.HotShiftElapsedTicks + '/' +
                    model.HotShiftRequiredTicks;
            }

            return "idle · completed " + model.HotShiftCompletedCount +
                   " · next run 1 fuel / 120 ticks / +2 parts";
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
