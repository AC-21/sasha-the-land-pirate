#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class FieldDeskSourceContract
    {
        public static void Verify(string repoRoot)
        {
            string runtimeRoot = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime");
            string controller = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingGameController.cs"));
            string hud = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingHud.cs"));
            string fieldDesk = File.ReadAllText(
                Path.Combine(runtimeRoot, "UI/LastBearingFieldDesk.cs"));
            string fieldDeskPresenter = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/LastBearingFieldDeskPresenter.cs"));
            string roadDeskPresenter = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/LastBearingRoadDeskPresenter.cs"));
            string serviceCellInteractor = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingCityServiceCellInteractor.cs"));
            string fieldDeskLayout = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/Resources/LastBearingFieldDeskLayout.uxml"));
            string fieldDeskStyles = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/Resources/LastBearingFieldDeskStyles.uss"));

            Require(controller, "private LastBearingFieldDesk? _fieldDesk;");
            Require(controller, "public LastBearingFieldDesk? FieldDesk => _fieldDesk;");
            Require(controller, "public bool IsExactFieldDeskCityOverview");
            Require(controller, "public bool IsExactFieldDeskDriving");
            Require(controller, "public bool HasPendingPlayerCommands");
            Require(controller, "public bool CanAcknowledgeDustFront");
            Require(controller, "public bool CanOpenEmergencyCisternPump");
            Require(controller, "public bool IsEmergencyCisternPumpFocused");
            Require(controller, "public bool CanPumpEmergencyCistern");
            Require(controller, "public bool IsEmergencyCisternPumpQueued");
            Require(
                controller,
                "public bool IsDustFrontAcknowledgementQueued");
            Require(controller, "public bool CanOpenDustFrontRelay");
            Require(controller, "public bool IsDustFrontRelayFocused");

            const string addDesk =
                "gameObject.AddComponent<LastBearingFieldDesk>()";
            const string configureDesk = "_fieldDesk.Configure(this);";
            const string addHud = "gameObject.AddComponent<LastBearingHud>()";
            const string configureHud = "_hud.Configure(this, _fieldDesk);";
            int addDeskAt = controller.IndexOf(addDesk, StringComparison.Ordinal);
            int configureDeskAt = controller.IndexOf(
                configureDesk,
                StringComparison.Ordinal);
            int addHudAt = controller.IndexOf(addHud, StringComparison.Ordinal);
            int configureHudAt = controller.IndexOf(
                configureHud,
                StringComparison.Ordinal);
            TestHarness.True(
                addDeskAt >= 0 && configureDeskAt > addDeskAt &&
                addHudAt > configureDeskAt && configureHudAt > addHudAt,
                "the controller must configure one Field Desk before the legacy HUD");
            TestHarness.Equal(
                1,
                CountOccurrences(controller, addDesk),
                "the controller must own exactly one Field Desk component");
            string failOpenSetup = controller.Substring(
                addDeskAt,
                addHudAt - addDeskAt);
            Require(failOpenSetup, "catch (Exception");
            Require(failOpenSetup, "_fieldDesk = null;");

            string hudConfiguration = Segment(
                hud,
                "public void Configure(",
                "private void OnGUI()");
            Require(
                hudConfiguration,
                "LastBearingFieldDesk? fieldDesk = null");
            Require(hudConfiguration, "_fieldDesk = fieldDesk;");
            string hudEntry = Segment(
                hud,
                "private void OnGUI()",
                "private void DrawHeader()");
            Require(
                hudEntry,
                "if (_fieldDesk?.OwnsRetainedHud == true)");
            TestHarness.Equal(
                1,
                CountOccurrences(hudEntry, "OwnsRetainedHud"),
                "the legacy HUD must yield to the retained city or road surface");
            TestHarness.True(
                hud.IndexOf(
                    "IsExactFieldDeskCityOverview",
                    StringComparison.Ordinal) < 0,
                "the legacy HUD must not duplicate the city-ownership predicate");
            Require(hudEntry, "DrawActiveGame(_controller.RuntimeReadModel);");
            Require(hudEntry, "DrawTitle();");

            Require(fieldDesk, "using UnityEngine.UIElements;");
            Require(fieldDesk, "public sealed class LastBearingFieldDesk");
            Require(fieldDesk, "public bool IsOperational");
            Require(fieldDesk, "public bool OwnsCityOverview");
            Require(fieldDesk, "public bool OwnsDriving");
            Require(fieldDesk, "public bool OwnsRetainedHud");
            Require(fieldDesk, "IsExactFieldDeskCityOverview");
            Require(fieldDesk, "IsExactFieldDeskDriving");
            Require(fieldDesk, "public void Configure(");
            Require(fieldDesk, "public void Refresh(");
            Require(fieldDesk, "public void ResetForLifecycle()");
            Require(fieldDesk, "LastBearingFieldDeskPresenter.Present(");
            Require(fieldDesk, "LastBearingFieldDeskPresenter.IsIntentAvailable(");
            Require(fieldDesk, "LastBearingRoadDeskPresenter.Present(");
            Require(fieldDesk, "SetPickingModeRecursive(");
            Require(fieldDesk, "PickingMode.Ignore");
            string keyboardOwnership = Segment(
                fieldDesk,
                "public bool OwnsKeyboardFocus",
                "public bool BlocksWorldPointer");
            Require(keyboardOwnership, "if (!OwnsCityOverview");
            string pointerOwnership = Segment(
                fieldDesk,
                "public bool BlocksWorldPointer",
                "internal void TrackPhysicalWorkRoute");
            Require(pointerOwnership, "if (!OwnsCityOverview");

            Require(fieldDesk, "UIDocument");
            Require(fieldDesk, "PanelSettings");
            Require(fieldDesk, "Resources.Load<VisualTreeAsset>");
            Require(fieldDesk, "Resources.Load<StyleSheet>");
            Require(fieldDesk, "Resources.Load<ThemeStyleSheet>");
            Require(fieldDesk, ".themeStyleSheet = theme;");
            Require(fieldDesk, "PanelScaleMode.ConstantPixelSize");
            Require(fieldDesk, ".focusedElement?.Blur();");
            Require(fieldDesk, "LastBearingFieldDeskLayout");
            Require(fieldDesk, "LastBearingFieldDeskStyles");
            Require(fieldDesk, "LastBearingFieldDeskTheme");
            Require(fieldDesk, "secondary-action-detail");
            Require(fieldDesk, "CloneTree(");
            Require(fieldDesk, ".Q<");
            Require(fieldDesk, "RegisterCallbacks");
            Require(fieldDesk, "UnregisterCallbacks");
            Require(fieldDesk, "OnDestroy");
            TestHarness.Equal(
                1,
                CountOccurrences(fieldDesk, "CloneTree("),
                "the Field Desk must clone its retained element tree exactly once");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    fieldDesk,
                    "Resources.Load<VisualTreeAsset>"),
                "the Field Desk must load its layout exactly once");
            TestHarness.Equal(
                1,
                CountOccurrences(fieldDesk, "Resources.Load<StyleSheet>"),
                "the Field Desk must load its style sheet exactly once");

            Require(
                fieldDeskPresenter,
                "public static class LastBearingFieldDeskPresenter");
            Require(
                fieldDeskPresenter,
                "public static LastBearingFieldDeskProjection Present(");
            Require(
                fieldDeskPresenter,
                "public static bool IsIntentAvailable(");
            Require(
                fieldDeskPresenter,
                "LastBearingPermitJobPresenter.Present(");
            Require(
                fieldDeskPresenter,
                "public readonly struct LastBearingDryLineProjection");
            Require(
                fieldDeskPresenter,
                "public static LastBearingDryLineProjection ProjectDryLine(");
            Require(
                fieldDeskPresenter,
                "model.DustFrontCrisisTicks");
            Require(
                fieldDeskPresenter,
                "ProjectWaterAtConstantDraw(");
            Require(
                fieldDeskPresenter,
                "projectedWater > dryLine");
            Require(
                fieldDeskPresenter,
                "\"FRONT IN \" + frontTicks");
            Require(
                fieldDeskPresenter,
                "\" TICKS · DRY LINE \"");
            Require(
                fieldDeskPresenter,
                "\" IF CURRENT DRAW CONTINUES\"");
            Require(
                fieldDeskPresenter,
                "FormatPressure(model)");
            Require(fieldDesk, "front-forecast-label");
            Require(fieldDesk, "projection.DryLine.Forecast");
            Require(
                fieldDeskLayout,
                "name=\"front-forecast-label\"");
            string roadStrip = Segment(
                fieldDeskLayout,
                "name=\"road-strip\"",
                "name=\"field-desk\"");
            Require(roadStrip, "picking-mode=\"Ignore\"");
            Require(roadStrip, "name=\"road-leg-label\"");
            Require(roadStrip, "name=\"road-route-label\"");
            Require(roadStrip, "name=\"road-progress-bar\"");
            Require(roadStrip, "name=\"road-scout-label\"");
            Require(roadStrip, "name=\"road-cargo-label\"");
            Require(roadStrip, "name=\"road-edge-label\"");
            Require(roadStrip, "name=\"road-next-verb-label\"");
            Require(roadStrip, "name=\"road-controls-label\"");
            TestHarness.True(
                roadStrip.IndexOf("<ui:Button", StringComparison.Ordinal) < 0,
                "the retained road strip must remain read-only");
            Require(fieldDeskStyles, ".road-strip");
            Require(fieldDeskStyles, ".road-edge-risk");

            Require(
                roadDeskPresenter,
                "public static class LastBearingRoadDeskPresenter");
            Require(
                roadDeskPresenter,
                "LastBearingRoadSafeLineView.DeriveState(");
            Require(
                roadDeskPresenter,
                "DerivePresentationCargoMassKilograms(model)");
            Require(
                roadDeskPresenter,
                "DerivePresentationDamageBand(");
            Require(roadDeskPresenter, "IsWreckLineModulePointAvailable");
            Require(
                roadDeskPresenter,
                "IsWreckLineFrameRailRecoveryAvailable");
            Require(
                roadDeskPresenter,
                "IsDepotApproachRecoveryAvailable");
            Require(
                serviceCellInteractor,
                "EMERGENCY_STORAGE_DRY_LINE_GAUGE");
            Require(
                serviceCellInteractor,
                "EMERGENCY_STORAGE_WATER_COLUMN");
            Require(
                serviceCellInteractor,
                "EMERGENCY_STORAGE_DRY_LINE_MARKER");
            Require(
                serviceCellInteractor,
                "EMERGENCY_STORAGE_FRONT_APPROACH_TELLTALE");
            Require(
                serviceCellInteractor,
                "LastBearingFieldDeskPresenter.ProjectDryLine(model)");
            Require(
                serviceCellInteractor,
                "projection.IsApproaching");

            foreach (string delegation in new[]
            {
                ".AssignRoadHand(",
                ".InspectCityNeed(",
                ".SelectCityBuildingPreview(",
                ".MoveCityBuildingPreview(",
                ".RotateCityBuildingPreview(",
                ".PlaceCityBuildingPreview(",
                ".ConnectCityServiceLink(",
                ".AssignCityServiceResident(",
                ".AdvanceCityServiceSled(",
                ".CancelCityBuildingPreview(",
                ".StartHotShift(",
                ".OpenDustFrontRelay(",
                ".AcknowledgeDustFrontFallback(",
                ".OpenEmergencyCisternPump(",
                ".BeginGaragePlan(",
                ".OpenGarageBay(",
                ".CommitExpedition(",
                ".OpenPumpHallRepair(",
                ".OpenPumpHallImprovement(",
                ".OpenOneGoodBatchWorkshop(",
                ".OpenFieldSleeveService(",
                ".TogglePause(",
                ".Save(",
                ".Load(",
                ".ReturnToTitle(",
            })
            {
                Require(fieldDesk, delegation);
            }

            Require(
                fieldDeskPresenter,
                "AssignHumanRoadHand = 36");
            Require(
                fieldDeskPresenter,
                "AssignRobotRoadHand = 37");
            Require(
                fieldDeskPresenter,
                "CHOOSE HUMAN ROAD HAND");
            Require(
                fieldDeskPresenter,
                "CHOOSE UTILITY-ROBOT ROAD HAND");
            Require(
                controller,
                "public void AssignRoadHand(string stableId)");
            Require(
                controller,
                "This colony has two valid road hands.");
            TestHarness.Equal(
                2,
                CountOccurrences(fieldDesk, ".AssignRoadHand("),
                "the Field Desk must dispatch both exact road-hand choices");
            Require(
                fieldDesk,
                ".AssignRoadHand(ResidentRoster.HumanResidentId)");
            Require(
                fieldDesk,
                ".AssignRoadHand(ResidentRoster.RobotResidentId)");

            TestHarness.True(
                fieldDesk.IndexOf(
                    ".InstallCityImprovement(",
                    StringComparison.Ordinal) < 0,
                "Field Desk must route to the physical pump-hall socket instead of submitting the installation");
            TestHarness.True(
                fieldDesk.IndexOf(
                    ".ServiceFieldSleeve(",
                    StringComparison.Ordinal) < 0,
                "Field Desk must route to the physical field-sleeve service control instead of submitting maintenance");

            Require(fieldDeskPresenter, "OpenPumpHallImprovement = 22");
            Require(
                fieldDeskPresenter,
                "OPEN PUMP HALL · SEAT AUXILIARY PUMP");
            Require(
                fieldDeskPresenter,
                "OPEN PUMP HALL · KEEP THE PROMISE");
            Require(
                fieldDeskPresenter,
                "controller.CanOpenFieldSleeveService");
            Require(
                hud,
                "_controller!.OpenFieldSleeveService();");
            TestHarness.True(
                hud.IndexOf(
                    ".ServiceFieldSleeve(",
                    StringComparison.Ordinal) < 0,
                "legacy HUD must route to the physical field-sleeve service control instead of submitting maintenance");
            Require(fieldDeskPresenter, "RunHotShift = 28");
            Require(fieldDeskPresenter, "AcknowledgeDustFront = 29");
            Require(
                fieldDeskPresenter,
                "OpenEmergencyCisternPump = 30");
            Require(fieldDeskPresenter, "OpenDustFrontRelay = 31");
            Require(
                fieldDeskPresenter,
                "OPEN EMERGENCY STORAGE · FACE DUST FRONT");
            Require(
                fieldDeskPresenter,
                "model.IsDustFrontAcknowledgementRequired");
            Require(fieldDeskPresenter, "PauseCause.DustFrontAlert");
            Require(
                fieldDeskPresenter,
                "LastBearingFieldDeskActionTone.Hazard");
            Require(hud, "DUST FRONT · GLOBAL ALERT");
            Require(hud, "_controller.OpenDustFrontRelay();");
            Require(
                hud,
                "_controller.AcknowledgeDustFrontFallback();");
            Require(
                hud,
                "\"OPEN EMERGENCY STORAGE · FACE DUST FRONT\"");
            TestHarness.True(
                fieldDesk.IndexOf(
                    ".AcknowledgeDustFront(",
                    StringComparison.Ordinal) < 0,
                "Field Desk must route to the physical Dust Front relay instead of submitting the command");
            Require(
                fieldDeskPresenter,
                "OPEN EMERGENCY STORAGE · WORK CISTERN PUMP");
            Require(
                fieldDeskPresenter,
                "controller.CanOpenEmergencyCisternPump");
            Require(
                hud,
                "_controller.OpenEmergencyCisternPump();");
            Require(
                hud,
                "\"OPEN EMERGENCY STORAGE · WORK CISTERN PUMP\"");
            TestHarness.True(
                fieldDesk.IndexOf(
                    ".PumpEmergencyCistern(",
                    StringComparison.Ordinal) < 0,
                "Field Desk must route to the physical cistern pump instead of submitting the command");
            Require(
                fieldDeskPresenter,
                "RUN HOT SHIFT\";");
            Require(
                fieldDeskPresenter,
                "RUN ANOTHER HOT SHIFT");
            Require(
                fieldDeskPresenter,
                "IsPreparationStalledByHotShift");
            Require(
                fieldDeskPresenter,
                "1 fuel powers 120 settlement ticks for +2 parts at -0.010 water per tick.");
            Require(
                fieldDeskPresenter,
                "The active shift owns the single machine-shop service slot");
            Require(
                fieldDeskPresenter,
                "Workshop Push preparation is held and the garage gauge is frozen");
            Require(
                fieldDeskPresenter,
                "Civic Buffer leaves the single machine-shop service slot available");
            Require(
                fieldDeskPresenter,
                "FormatActiveServiceOrderName(model)");
            Require(
                fieldDeskPresenter,
                " still owns the single machine-shop service slot");
            Require(
                fieldDeskPresenter,
                "Workshop Push still owns the single machine-shop service slot");
            Require(
                fieldDeskPresenter,
                "Workshop Push owns the single machine-shop service slot and is actively advancing");
            TestHarness.True(
                fieldDeskPresenter.IndexOf(
                    "HOT SHIFT · STALLED",
                    StringComparison.Ordinal) < 0,
                "Field Desk must not claim Workshop Push stalls Hot Shift");
            TestHarness.True(
                fieldDeskPresenter.IndexOf(
                    "Workshop Push borrowed the machine-shop operator",
                    StringComparison.Ordinal) < 0,
                "Field Desk must show Hot Shift owning the service slot");
            Require(
                fieldDeskPresenter,
                "COMMISSIONING DELIVERY · ONCE");
            Require(
                fieldDeskPresenter,
                "SERVICE CELL · CITY WORK ORDERS");
            Require(
                fieldDeskPresenter,
                "PARTS SHIFT: 1 FUEL · 120 TICKS · +2 PARTS");
            Require(
                fieldDeskPresenter,
                "WATER SHIFT: 1 FUEL · 120 TICKS");
            Require(
                fieldDeskPresenter,
                "GROSS +10.000 WATER · NO PARTS");

            foreach (string retiredDelegation in new[]
            {
                ".SelectCityGrammarHypothesis(",
                ".ManipulateCityGrammarPrimary(",
                ".RotateCityGrammarPrimary(",
                ".ToggleCityGrammarTrialPiece(",
                ".ConnectCityGrammarLogistics(",
                ".AdvanceCityGrammarDelivery(",
                ".RecordCityGrammarPathRead(",
                ".ResetActiveCityGrammarTrial(",
                ".LeaveCityGrammarComparison(",
                ".ResetCityGrammarComparison(",
                ".ActivateInfrastructure(",
            })
            {
                TestHarness.True(
                    fieldDesk.IndexOf(
                        retiredDelegation,
                        StringComparison.Ordinal) < 0,
                    "Field Desk still surfaces retired comparison control " +
                    retiredDelegation);
            }

            foreach (string source in new[] { fieldDesk, fieldDeskPresenter })
            {
                foreach (string forbidden in new[]
                {
                    "Queue(",
                    "LastBearingKernel",
                    "LastBearingState",
                    "LastBearingCommand",
                    ".World",
                    "ModeCoordinator",
                    "LastBearingSaveAdapter",
                    "LastBearingProfileStore",
                    "OpenFixedProfileDirectory",
                    "TryPersist(",
                    "AtomicLandPirate.Save",
                    "Application.persistentDataPath",
                    "PlayerPrefs",
                })
                {
                    TestHarness.True(
                        source.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                        "Field Desk source contains forbidden authority " +
                        forbidden);
                }

                TestHarness.True(
                    !Regex.IsMatch(
                        source,
                        @"\bnew\s+[A-Za-z_][A-Za-z0-9_]*Command\s*\("),
                    "Field Desk source constructs a canonical command");
                TestHarness.True(
                    !Regex.IsMatch(
                        source,
                        @"\b(?:model|readModel)\s*\.\s*[A-Za-z_]" +
                        @"[A-Za-z0-9_]*\s*=(?!=)"),
                    "Field Desk source writes into a SimulationCore read model");
            }
        }

        private static void Require(string source, string token)
        {
            TestHarness.True(
                source.IndexOf(token, StringComparison.Ordinal) >= 0,
                "Field Desk source contract is missing " + token);
        }

        private static string Segment(
            string source,
            string startToken,
            string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            TestHarness.True(
                start >= 0,
                "Field Desk source contract is missing " + startToken);
            int end = source.IndexOf(
                endToken,
                start,
                StringComparison.Ordinal);
            TestHarness.True(
                end > start,
                "Field Desk source contract is missing " + endToken);
            return source.Substring(start, end - start);
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var offset = 0;
            while (offset <= source.Length - token.Length)
            {
                int match = source.IndexOf(
                    token,
                    offset,
                    StringComparison.Ordinal);
                if (match < 0)
                {
                    break;
                }

                count++;
                offset = match + token.Length;
            }

            return count;
        }
    }
}
