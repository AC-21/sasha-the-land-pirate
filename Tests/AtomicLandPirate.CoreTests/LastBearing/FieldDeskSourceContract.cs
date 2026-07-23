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

            Require(controller, "private LastBearingFieldDesk? _fieldDesk;");
            Require(controller, "public LastBearingFieldDesk? FieldDesk => _fieldDesk;");
            Require(controller, "public bool IsExactFieldDeskCityOverview");
            Require(controller, "public bool HasPendingPlayerCommands");
            Require(controller, "public bool CanAcknowledgeDustFront");

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
                "if (_fieldDesk?.OwnsCityOverview == true)");
            TestHarness.Equal(
                1,
                CountOccurrences(hudEntry, "OwnsCityOverview"),
                "the legacy HUD may suppress only for Field Desk city ownership");
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
            Require(fieldDesk, "IsExactFieldDeskCityOverview");
            Require(fieldDesk, "public void Configure(");
            Require(fieldDesk, "public void Refresh(");
            Require(fieldDesk, "public void ResetForLifecycle()");
            Require(fieldDesk, "LastBearingFieldDeskPresenter.Present(");
            Require(fieldDesk, "LastBearingFieldDeskPresenter.IsIntentAvailable(");

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

            foreach (string delegation in new[]
            {
                ".AssignDefaultLeadResident(",
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
                ".AcknowledgeDustFront(",
                ".BeginGaragePlan(",
                ".OpenGarageBay(",
                ".CommitExpedition(",
                ".OpenPumpHallRepair(",
                ".OpenOneGoodBatchWorkshop(",
                ".InstallCityImprovement(",
                ".ServiceFieldSleeve(",
                ".TogglePause(",
                ".Save(",
                ".Load(",
                ".ReturnToTitle(",
            })
            {
                Require(fieldDesk, delegation);
            }

            Require(fieldDeskPresenter, "RunHotShift = 28");
            Require(fieldDeskPresenter, "AcknowledgeDustFront = 29");
            Require(fieldDeskPresenter, "\"ACKNOWLEDGE FRONT\"");
            Require(
                fieldDeskPresenter,
                "model.IsDustFrontAcknowledgementRequired");
            Require(fieldDeskPresenter, "PauseCause.DustFrontAlert");
            Require(
                fieldDeskPresenter,
                "LastBearingFieldDeskActionTone.Hazard");
            Require(hud, "DUST FRONT · GLOBAL ALERT");
            Require(hud, "GUILayout.Button(\"ACKNOWLEDGE FRONT\"");
            Require(hud, "_controller.AcknowledgeDustFront();");
            Require(
                fieldDeskPresenter,
                "RUN HOT SHIFT\";");
            Require(
                fieldDeskPresenter,
                "RUN ANOTHER HOT SHIFT");
            Require(
                fieldDeskPresenter,
                "HOT SHIFT · STALLED · ");
            Require(
                fieldDeskPresenter,
                "Workshop Push borrowed the machine-shop operator");
            Require(
                fieldDeskPresenter,
                "Civic Buffer leaves the operator available");
            Require(
                fieldDeskPresenter,
                "COMMISSIONING DELIVERY · ONCE");
            Require(
                fieldDeskPresenter,
                "HOT SHIFT · CITY WORK ORDER");

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
