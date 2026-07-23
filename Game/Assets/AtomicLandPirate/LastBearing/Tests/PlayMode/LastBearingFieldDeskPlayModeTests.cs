#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFieldDeskPlayModeTests
    {
        private GameObject? _root;
        private readonly List<string> _temporarySaveRoots =
            new List<string>();

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (LastBearingGameController controller in
                     Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(controller.gameObject);
            }

            foreach (string root in _temporarySaveRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            _temporarySaveRoots.Clear();
        }

        [UnityTest]
        public IEnumerator DeskOwnsOnlyCityAndReusesDocumentAcrossLifecycle()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);
            Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
            Assert.That(
                document.panelSettings.scaleMode,
                Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            Assert.That(document.panelSettings.scale, Is.EqualTo(1f));
            VisualElement overlay = document.rootVisualElement.Q<VisualElement>(
                "field-desk-overlay");
            Assert.That(overlay, Is.Not.Null);

            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            LastBearingHud legacyHud =
                controller.GetComponent<LastBearingHud>();
            Assert.That(legacyHud.enabled, Is.False);

            desk.ResetForLifecycle();
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(legacyHud.enabled, Is.True);
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);

            controller.OpenGarageBay();
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(legacyHud.enabled, Is.True);
            Assert.That(RequireDocument(controller), Is.SameAs(document));

            controller.ShowCityOverview();
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);

            controller.ReturnToTitle();
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(legacyHud.enabled, Is.True);

            controller.StartNewGame(ColonyComposition.RobotOnly);
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);
            Assert.That(RequireDocument(controller), Is.SameAs(document));
            Assert.That(
                controller.GetComponentsInChildren<UIDocument>(true),
                Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeskRendersCurrentOrderAndClearsFocusOnExit()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);

            controller.InspectCityNeed();
            desk.Refresh(force: true);
            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Label primaryDetail = document.rootVisualElement.Q<Label>(
                "current-action-detail");
            Label secondaryDetail = document.rootVisualElement.Q<Label>(
                "secondary-action-detail");
            Button primary = document.rootVisualElement.Q<Button>(
                "primary-action-button");

            Assert.That(primaryDetail.text, Is.EqualTo(projection.PrimaryAction.Detail));
            Assert.That(
                secondaryDetail.text,
                Is.EqualTo(projection.SecondaryAction.Detail));
            Assert.That(
                secondaryDetail.style.display.value,
                Is.EqualTo(DisplayStyle.None));

            primary.Focus();
            yield return null;
            Assert.That(
                document.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(primary));
            controller.OpenGarageBay();
            desk.Refresh(force: true);
            Assert.That(
                document.rootVisualElement.panel.focusController.focusedElement,
                Is.Not.SameAs(primary));
        }

        [UnityTest]
        public IEnumerator SubmitDelegatesOnceAcrossSameFrameModeReentry()
        {
            LastBearingGameController controller = BuildController();
            LastBearingFieldDesk desk = RequireDesk(controller);
            yield return null;
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            desk.Refresh(force: true);

            Button primary = RequireDocument(controller)
                .rootVisualElement.Q<Button>("primary-action-button");
            Assert.That(primary, Is.Not.Null);
            Assert.That(primary.text, Is.EqualTo("PENCIL CIVIC BUFFER"));
            string canonicalBefore = controller.CanonicalHash;

            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.GarageBay);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            controller.ShowCityOverview();
            desk.Refresh(force: true);
            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.CityOverview);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            yield return null;
            desk.Refresh(force: true);
            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.GarageBay);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [UnityTest]
        public IEnumerator HotShiftStallsThenMovesTheCommissioningSledAndAutosaves()
        {
            LastBearingGameController controller = BuildController();
            LastBearingFieldDesk desk = RequireDesk(controller);
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            CompleteWorkingServiceCell(controller);
            Transform sled = controller.World!.CityServiceCellView!.transform.Find(
                "Canonical Parts Sled");
            Assert.That(sled, Is.Not.Null);
            Vector3 commissionedAtShop = sled.localPosition;

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);
            controller.ShowCityOverview();
            yield return null;
            desk.Refresh(force: true);

            LastBearingFieldDeskProjection available =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                available.PrimaryAction.Label,
                Is.EqualTo("INSPECT SASHA'S RIG"));
            Assert.That(
                available.SecondaryAction.Label,
                Is.EqualTo(
                    "RUN HOT SHIFT · 1 FUEL · 120 TICKS · +2 PARTS"));

            VisualElement root =
                RequireDocument(controller).rootVisualElement;
            Button secondaryAction =
                root.Q<Button>("secondary-action-button");
            Button advanceAction = root.Q<Button>("advance-button");
            long partsBefore = controller.ReadModel!.PartsUnits;
            long fuelBefore = controller.ReadModel.FuelUnits;
            int generationsBeforeStart =
                GenerationCount(profileDirectory);
            string hashBeforeStart = controller.CanonicalHash;
            Submit(secondaryAction);
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            InvokeSimulationTick(controller);
            desk.Refresh(force: true);
            Assert.That(
                controller.ReadModel.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.InProgress));
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.Zero);
            Assert.That(
                controller.ReadModel.FuelUnits,
                Is.EqualTo(fuelBefore - 1));
            Assert.That(
                LastBearingFieldDeskPresenter.Present(controller)
                    .SecondaryAction.Label,
                Is.EqualTo("HOT SHIFT · STALLED · 0 / 120"));
            Assert.That(
                controller.Status,
                Does.Contain("borrowed the operator"));
            string startHash = AssertAutosaveBoundary(
                controller,
                profileDirectory,
                generationsBeforeStart,
                hashBeforeStart);
            Vector3 stalledAtRecycler = sled.localPosition;
            Assert.That(
                Vector3.Distance(stalledAtRecycler, commissionedAtShop),
                Is.GreaterThan(0.01f));

            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(startHash));
            Assert.That(
                controller.ReadModel!.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.InProgress));
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.Zero);
            Assert.That(controller.ReadModel.HotShiftCompletedCount, Is.Zero);
            Assert.That(
                controller.ReadModel.FuelUnits,
                Is.EqualTo(fuelBefore - 1));
            controller.ShowCityOverview();
            desk.Refresh(force: true);

            InvokeSimulationTick(controller);
            Assert.That(sled.localPosition, Is.EqualTo(stalledAtRecycler));

            var preparationGuard = 0;
            long preparationBound =
                controller.ReadModel!.PreparationRemainingTicks + 2;
            while (controller.ReadModel!.PreparationPhase ==
                       PreparationPhase.Preparing &&
                   preparationGuard < preparationBound)
            {
                InvokeSimulationTick(controller);
                preparationGuard++;
            }

            Assert.That(
                controller.ReadModel!.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.Zero);
            desk.Refresh(force: true);
            Assert.That(
                root.Q<Button>("primary-action-button").text,
                Is.EqualTo("COMMIT THE MANIFEST"));
            Assert.That(
                root.Q<Button>("secondary-action-button").text,
                Is.EqualTo("INSPECT THE GARAGE"));
            Assert.That(
                advanceAction.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(
                advanceAction.text,
                Is.EqualTo("HOT SHIFT · 0 / 120"));
            Assert.That(advanceAction.enabledSelf, Is.False);

            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.EqualTo(1));
            Assert.That(
                Vector3.Distance(sled.localPosition, stalledAtRecycler),
                Is.GreaterThan(0.001f));

            var checkpointGuard = 0;
            while (controller.ReadModel.HotShiftElapsedTicks < 59 &&
                   checkpointGuard <
                       LastBearingBalanceV1.HotShiftRequiredSettlementTicks)
            {
                InvokeSimulationTick(controller);
                checkpointGuard++;
            }

            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.EqualTo(59));
            int generationsBeforeCheckpoint =
                GenerationCount(profileDirectory);
            string hashBeforeCheckpoint = controller.CanonicalHash;
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.EqualTo(60));
            Assert.That(controller.Status, Does.Contain("checkpoint: 60 / 120"));
            string checkpointHash = AssertAutosaveBoundary(
                controller,
                profileDirectory,
                generationsBeforeCheckpoint,
                hashBeforeCheckpoint);
            desk.Refresh(force: true);
            Assert.That(advanceAction.text, Is.EqualTo("HOT SHIFT · 60 / 120"));
            Assert.That(advanceAction.enabledSelf, Is.False);

            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(checkpointHash));
            Assert.That(
                controller.ReadModel!.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.InProgress));
            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.EqualTo(60));
            Assert.That(controller.ReadModel.HotShiftCompletedCount, Is.Zero);
            controller.ShowCityOverview();
            desk.Refresh(force: true);

            var completionGuard = 0;
            while (controller.ReadModel.HotShiftElapsedTicks < 119 &&
                   completionGuard <
                       LastBearingBalanceV1.HotShiftRequiredSettlementTicks)
            {
                InvokeSimulationTick(controller);
                completionGuard++;
            }

            Assert.That(controller.ReadModel.HotShiftElapsedTicks, Is.EqualTo(119));
            int generationsBeforeCompletion =
                GenerationCount(profileDirectory);
            string hashBeforeCompletion = controller.CanonicalHash;
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.HotShiftPhase, Is.EqualTo(HotShiftPhase.Idle));
            Assert.That(controller.ReadModel.HotShiftCompletedCount, Is.EqualTo(1));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partsBefore + 2));
            Assert.That(controller.Status, Does.Contain("Hot Shift complete"));
            string completionHash = AssertAutosaveBoundary(
                controller,
                profileDirectory,
                generationsBeforeCompletion,
                hashBeforeCompletion);
            Assert.That(
                Vector3.Distance(sled.localPosition, commissionedAtShop),
                Is.LessThan(0.001f));

            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(completionHash));
            Assert.That(
                controller.ReadModel!.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.Idle));
            Assert.That(controller.ReadModel.HotShiftCompletedCount, Is.EqualTo(1));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partsBefore + 2));
            controller.ShowCityOverview();
            yield return null;
            desk.Refresh(force: true);
            Assert.That(
                advanceAction.text,
                Is.EqualTo(
                    "RUN ANOTHER HOT SHIFT · 1 FUEL · 120 TICKS · +2 PARTS"));
            Assert.That(advanceAction.enabledSelf, Is.True);

            Submit(advanceAction);
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.HotShiftCompletedCount, Is.EqualTo(1));
            Assert.That(controller.ReadModel.HotShiftPhase, Is.EqualTo(HotShiftPhase.InProgress));
            Assert.That(
                Vector3.Distance(sled.localPosition, stalledAtRecycler),
                Is.LessThan(0.05f));
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            return controller;
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "hot-shift-tests-" + Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _temporarySaveRoots.Add(root);

            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(
                    profileDirectory);
            ConstructorInfo? constructor =
                typeof(LastBearingSaveAdapter).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    new[] { typeof(LastBearingProfileStore) },
                    modifiers: null);
            FieldInfo? adapterField =
                typeof(LastBearingGameController).GetField(
                    "_saveAdapter",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(adapterField, Is.Not.Null);
            var adapter = constructor!.Invoke(new object[] { store }) as
                LastBearingSaveAdapter;
            Assert.That(adapter, Is.Not.Null);
            adapterField!.SetValue(controller, adapter);
            return profileDirectory;
        }

        private static string GetConfinementSafeTemporaryRoot()
        {
            string temporaryRoot = Path.GetTempPath();
            bool isMacOs = Application.platform == RuntimePlatform.OSXEditor ||
                           Application.platform == RuntimePlatform.OSXPlayer;
            if (isMacOs &&
                temporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
            {
                return "/private" + temporaryRoot;
            }

            return temporaryRoot;
        }

        private static string AssertAutosaveBoundary(
            LastBearingGameController controller,
            string profileDirectory,
            int generationsBefore,
            string hashBefore)
        {
            string currentHash = controller.CanonicalHash;
            int expectedGeneration = generationsBefore + 1;
            Assert.That(currentHash, Is.Not.EqualTo(hashBefore));
            Assert.That(
                GenerationCount(profileDirectory),
                Is.EqualTo(expectedGeneration));
            Assert.That(
                controller.SaveStatus,
                Is.EqualTo(
                    LastBearingSaveCodes.SaveOk +
                    " · generation " + expectedGeneration +
                    " · " + currentHash.Substring(0, 12)));
            return currentHash;
        }

        private static int GenerationCount(string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(profileDirectory, "gen-*.lbg").Length;
        }

        private static LastBearingFieldDesk RequireDesk(
            LastBearingGameController controller)
        {
            Assert.That(controller.FieldDesk, Is.Not.Null);
            Assert.That(controller.FieldDesk!.IsOperational, Is.True);
            return controller.FieldDesk;
        }

        private static UIDocument RequireDocument(
            LastBearingGameController controller)
        {
            UIDocument[] documents =
                controller.GetComponentsInChildren<UIDocument>(true);
            Assert.That(documents, Has.Length.EqualTo(1));
            return documents[0];
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void CompleteDistrictObservation(
            LastBearingGameController controller)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
        }

        private static void CompleteWorkingServiceCell(
            LastBearingGameController controller)
        {
            controller.InspectCityNeed();
            PlaceBuilding(controller, CityBuildingKind.Recycler);
            PlaceBuilding(controller, CityBuildingKind.MachineShop);
            PlaceBuilding(controller, CityBuildingKind.EmergencyStorage);
            controller.ConnectCityServiceLink();
            InvokeSimulationTick(controller);
            controller.AssignCityServiceResident(ResidentRoster.HumanResidentId);
            InvokeSimulationTick(controller);
            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);
            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);

            Assert.That(controller.ReadModel!.SliceInfrastructureActive, Is.True);
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.DeliveredToWorkshop));
        }

        private static void PlaceBuilding(
            LastBearingGameController controller,
            CityBuildingKind building)
        {
            controller.SelectCityBuildingPreview(building);
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(controller, null);
        }

        private static void AssertMode(
            LastBearingGameController controller,
            LastBearingPresentationMode expected)
        {
            Assert.That(controller.ModeCoordinator!.CurrentMode, Is.EqualTo(expected));
        }
    }
}
