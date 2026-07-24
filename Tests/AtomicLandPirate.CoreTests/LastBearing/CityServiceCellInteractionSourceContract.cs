#nullable enable

using System;
using System.IO;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class CityServiceCellInteractionSourceContract
    {
        public static void Verify(string repoRoot)
        {
            string runtimeRoot = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime");
            string interactor = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingCityServiceCellInteractor.cs"));
            string view = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingCityServiceCellView.cs"));
            string world = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingWorldBuilder.cs"));
            string controller = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingGameController.cs"));
            string fieldDesk = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/LastBearingFieldDesk.cs"));
            string fieldDeskPresenter = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "UI/LastBearingFieldDeskPresenter.cs"));
            string playMode = File.ReadAllText(
                Path.Combine(
                    repoRoot,
                    "Game/Assets/AtomicLandPirate/LastBearing/Tests/" +
                    "PlayMode/LastBearingPlayModeTests.cs"));

            Require(
                interactor,
                "public sealed class LastBearingCityServiceCellInteractor");
            Require(interactor, "public const int InteractionLayer = 30;");
            Require(interactor, "Physics.RaycastNonAlloc(");
            Require(interactor, "1 << InteractionLayer");
            Require(interactor, "QueryTriggerInteraction.Collide");
            Require(interactor, "target.AddComponent<BoxCollider>()");
            Require(interactor, "collider.isTrigger = true;");
            Require(interactor, "Mouse.current");
            Require(interactor, "Keyboard.current");
            Require(interactor, "keyboard?.rKey.wasPressedThisFrame");
            Require(interactor, "BlocksWorldPointer(");
            Require(interactor, "private void OnDisable()");
            Require(interactor, "ResetLocalSelection();");
            Require(interactor, "_linkSourceSelected ||");
            Require(interactor, "_selectedResidentId != null");
            Require(interactor, "model.RecyclerQuarterTurns");
            Require(interactor, "model.MachineShopQuarterTurns");
            Require(interactor, "RotateOffset(");
            Require(interactor, "2 PARTS · MOVE FREE");
            Require(interactor, "3 PARTS · MOVE FREE");
            Require(interactor, "1 PART · MOVE FREE");
            Require(interactor, "_controller?.CanAssignCityServiceHuman == true");
            Require(interactor, "_controller?.CanAssignCityServiceRobot == true");
            Require(
                interactor,
                "public bool TryActivateAtScreenPosition(Vector2 screenPosition)");

            foreach (string target in new[]
            {
                "INTERACT_SELECT_RECYCLER",
                "INTERACT_SELECT_MACHINE_SHOP",
                "INTERACT_SELECT_EMERGENCY_STORAGE",
                "INTERACT_WORK_PAD_",
                "SOCKET_RECYCLER_OUTPUT_INTERACTION",
                "SOCKET_MACHINE_SHOP_INTAKE_INTERACTION",
                "TOKEN_SERVICE_OPERATOR_HUMAN",
                "TOKEN_SERVICE_OPERATOR_ROBOT",
                "SOCKET_MACHINE_SHOP_OPERATOR_INTERACTION",
                "INTERACT_CALIBRATION_SLED",
                "SOCKET_CALIBRATION_SLED_DESTINATION",
                "WORKING_SERVICE_CELL_FEEDBACK",
            })
            {
                Require(interactor, target);
            }

            foreach (string delegation in new[]
            {
                ".SelectCityBuildingPreview(",
                ".MoveCityBuildingPreview(",
                ".RotateCityBuildingPreview(",
                ".PlaceCityBuildingPreview(",
                ".ConnectCityServiceLink(",
                ".AssignCityServiceResident(",
                ".AdvanceCityServiceSled(",
            })
            {
                Require(interactor, delegation);
            }

            foreach (string forbidden in new[]
            {
                "LastBearingKernel",
                "LastBearingState",
                "LastBearingCommand",
                "new PlaceCityBuildingCommand",
                "new ConnectCityServiceLinkCommand",
                "new AssignCityServiceResidentCommand",
                "new AdvanceCityServiceSledCommand",
                "Queue(",
                "SaveContracts",
                "LastBearingSaveAdapter",
                "PlayerPrefs",
                "System.IO",
                "File.",
                "UnityWebRequest",
                "NavMesh",
                "Rigidbody",
            })
            {
                TestHarness.True(
                    interactor.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "world service-cell interaction contains forbidden authority " +
                    forbidden);
            }

            Require(
                view,
                "public LastBearingCityServiceCellInteractor? Interactor");
            Require(
                view,
                "gameObject.AddComponent<LastBearingCityServiceCellInteractor>()");
            Require(view, "Interactor?.Apply(model, _sled);");
            Require(view, "model.RecyclerQuarterTurns");
            Require(view, "model.MachineShopQuarterTurns");
            Require(view, "collider.enabled = false;");
            Require(world, "ConfigureCityServiceCellInteraction(");
            Require(world, "ResetCityServiceCellInteraction()");
            Require(
                controller,
                "_world.ConfigureCityServiceCellInteraction(this);");
            Require(
                controller,
                "_world?.ResetCityServiceCellInteraction();");
            Require(
                fieldDesk,
                "public bool BlocksWorldPointer(Vector2 screenPosition)");
            Require(fieldDesk, "RuntimePanelUtils.ScreenToPanel(");
            Require(fieldDesk, "_desk.worldBound.Contains(panelPosition)");
            Require(
                fieldDeskPresenter,
                "World: click Recycler output, then Machine Shop intake.");
            Require(
                fieldDeskPresenter,
                "Keyboard/accessibility fallback: cycle the world ghost");
            Require(
                fieldDeskPresenter,
                "World: click the sled, then its Machine Shop destination.");

            Require(
                playMode,
                "HandsOnServiceCellRunsFromWorldAndSurvivesFourModeTransitions");
            Require(
                playMode,
                "private static void ActivateWorldTarget(");
            Require(
                playMode,
                "interactor.TryActivateAtScreenPosition(pointer)");
            Require(playMode, "BlocksWorldPointer(");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.RecyclerSelectorName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.MachineShopSelectorName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.EmergencyStorageSelectorName");
            Require(playMode, "\"INTERACT_WORK_PAD_01\"");
            Require(playMode, "\"INTERACT_WORK_PAD_05\"");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.RecyclerOutputSocketName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.MachineShopIntakeSocketName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.HumanResidentTokenName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.OperatorSocketName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.SledInteractionName");
            Require(
                playMode,
                "LastBearingCityServiceCellInteractor.SledDestinationName");
            Require(playMode, "interactor.HoverPad(");
            Require(playMode, "Press(keyboard.rKey);");
            Require(playMode, "interactor.SelectResident(");
            Require(playMode, "interactor.ClickSledDestination();");
            Require(playMode, "controller.OpenBuildingCutaway();");
            Require(playMode, "Assert.That(interactor.SelectedResidentId, Is.Null);");
            Require(playMode, "controller.Save();");
            Require(playMode, "controller.ReturnToTitle();");
            Require(playMode, "controller.Load();");
        }

        private static void Require(string source, string fragment)
        {
            TestHarness.True(
                source.IndexOf(fragment, StringComparison.Ordinal) >= 0,
                "world service-cell interaction is missing " + fragment);
        }
    }
}
