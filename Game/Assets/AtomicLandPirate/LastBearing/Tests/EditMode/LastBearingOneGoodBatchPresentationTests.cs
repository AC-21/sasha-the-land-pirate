#nullable enable

using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingOneGoodBatchPresentationTests
    {
        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            foreach (LastBearingGameController controller in
                     Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void AuthoredCutawayUsesFixedSharedCameraContractAndNoPhysics()
        {
            LastBearingGameController controller = BuildController(
                ColonyComposition.Mixed);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingOneGoodBatchCutawayView view =
                world.OneGoodBatchCutawayView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(
                LastBearingOneGoodBatchCutawayView.ContentId,
                Is.EqualTo(LastBearingState.OneGoodBatchPresentationContentId));
            Assert.That(
                view.name,
                Is.EqualTo(LastBearingOneGoodBatchCutawayView.RootName));
            Assert.That(view.IsDollhouseCutaway, Is.True);
            Assert.That(view.HasRoof, Is.False);
            Assert.That(view.HasNearWall, Is.False);
            AssertAnchor(
                view.CameraAnchor,
                LastBearingOneGoodBatchCutawayView.CameraAnchorName,
                LastBearingOneGoodBatchCutawayView.CameraAnchorPosition);
            AssertAnchor(
                view.FocusAnchor,
                LastBearingOneGoodBatchCutawayView.FocusAnchorName,
                LastBearingOneGoodBatchCutawayView.FocusAnchorPosition);
            AssertAnchor(
                view.InputAnchor,
                LastBearingOneGoodBatchCutawayView.InputAnchorName,
                LastBearingOneGoodBatchCutawayView.InputAnchorPosition);
            AssertAnchor(
                view.WorkAnchor,
                LastBearingOneGoodBatchCutawayView.WorkAnchorName,
                LastBearingOneGoodBatchCutawayView.WorkAnchorPosition);
            AssertAnchor(
                view.OutputAnchor,
                LastBearingOneGoodBatchCutawayView.OutputAnchorName,
                LastBearingOneGoodBatchCutawayView.OutputAnchorPosition);
            AssertAnchor(
                view.ClaimsAnchor,
                LastBearingOneGoodBatchCutawayView.ClaimsAnchorName,
                LastBearingOneGoodBatchCutawayView.ClaimsAnchorPosition);
            AssertAnchor(
                view.PermitAnchor,
                LastBearingOneGoodBatchCutawayView.PermitAnchorName,
                LastBearingOneGoodBatchCutawayView.PermitAnchorPosition);

            Assert.That(view.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);
            Collider[] colliders =
                view.GetComponentsInChildren<Collider>(true);
            Transform fuelBondTargetTransform = view.transform.Find(
                LastBearingFuelBondInteractor.RootName + "/" +
                LastBearingFuelBondInteractor.ControlName);
            Assert.That(fuelBondTargetTransform, Is.Not.Null);
            Collider fuelBondTarget =
                fuelBondTargetTransform.GetComponent<BoxCollider>();
            Assert.That(fuelBondTarget, Is.Not.Null);
            Assert.That(fuelBondTarget.isTrigger, Is.True);
            Assert.That(fuelBondTarget.enabled, Is.False);
            foreach (Collider collider in colliders)
            {
                if (collider != fuelBondTarget)
                {
                    Assert.That(collider.enabled, Is.False, collider.name);
                }
            }
            Assert.That(
                CountNamedParts(view, "PHYSICAL_INPUT_PART_"),
                Is.EqualTo(2));
            Assert.That(
                CountNamedParts(view, "FUTURE_TOLL_FUEL_UNIT_"),
                Is.EqualTo(2));
            Assert.That(
                CountNamedParts(view, "RETURNED_FUEL_CAN_"),
                Is.EqualTo(LastBearingFuelBondInteractor.FuelCanCount));

            Camera[] runtimeCameras =
                controller.GetComponentsInChildren<Camera>(true);
            Assert.That(runtimeCameras, Has.Length.EqualTo(1));
            Assert.That(runtimeCameras[0], Is.SameAs(world.MainCamera));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void OnePhysicalLotMovesAcrossExactCustodyAnchors()
        {
            LastBearingGameController controller = BuildController(
                ColonyComposition.Mixed);
            controller.World!.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            LastBearingOneGoodBatchCutawayView view =
                controller.World!.OneGoodBatchCutawayView!;
            string canonicalBefore = controller.CanonicalHash;
            GameObject lot = view.BearingLot!;

            Assert.That(view.IsTwoFuelTollVisible, Is.False);
            Assert.That(
                CountActiveNamedParts(view, "FUTURE_TOLL_FUEL_UNIT_"),
                Is.EqualTo(0));

            view.Apply(
                batchStartAvailable: true,
                SpareBearingBatchPhase.None,
                SpareBearingLotCustody.None,
                lotQuantity: 0,
                routePermitGranted: false,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.IsInputStockVisible, Is.True);
            Assert.That(view.IsWorkpieceVisible, Is.False);
            Assert.That(view.IsBearingLotVisible, Is.False);
            Assert.That(view.IsPermitLockedVisible, Is.True);
            Assert.That(
                CountActiveNamedParts(view, "FUTURE_TOLL_FUEL_UNIT_"),
                Is.EqualTo(2));

            view.Apply(
                batchStartAvailable: false,
                SpareBearingBatchPhase.InProgress,
                SpareBearingLotCustody.None,
                lotQuantity: 0,
                routePermitGranted: false,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.IsInputStockVisible, Is.False);
            Assert.That(view.IsWorkpieceVisible, Is.True);
            Assert.That(view.IsMachineRunning, Is.True);

            view.Apply(
                batchStartAvailable: false,
                SpareBearingBatchPhase.Complete,
                SpareBearingLotCustody.WorkshopOutput,
                lotQuantity: 1,
                routePermitGranted: false,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.IsWorkpieceVisible, Is.False);
            Assert.That(view.IsBearingLotVisible, Is.True);
            Assert.That(view.BearingLot, Is.SameAs(lot));
            Assert.That(lot.transform.parent, Is.SameAs(view.OutputAnchor));

            view.Apply(
                batchStartAvailable: false,
                SpareBearingBatchPhase.Settled,
                SpareBearingLotCustody.LastBearingClaimsCounter,
                lotQuantity: 1,
                routePermitGranted: true,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.BearingLot, Is.SameAs(lot));
            Assert.That(lot.transform.parent, Is.SameAs(view.ClaimsAnchor));
            Assert.That(view.IsPermitLockedVisible, Is.False);
            Assert.That(view.IsPermitGrantedVisible, Is.True);
            Assert.That(
                CountActiveNamedParts(view, "FUTURE_TOLL_FUEL_UNIT_"),
                Is.EqualTo(2));
            Assert.That(
                view.GetComponentsInChildren<LastBearingOneGoodBatchCutawayView>(true)
                    .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                    .Count(item => item.name == "LOT_SPARE_BEARING_ONE_GOOD_BATCH"),
                Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void CutawaySelectionAndPermitSilhouetteStayDerivedOnly()
        {
            LastBearingGameController controller = BuildController(
                ColonyComposition.HumanOnly);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingOneGoodBatchCutawayView workshop =
                world.OneGoodBatchCutawayView!;
            LastBearingPumpHallCutawayView pumpHall = world.PumpHallCutawayView!;
            LastBearingDepotApproachRecoveryView depot =
                world.DepotApproachRecoveryView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(pumpHall.gameObject.activeSelf, Is.True);
            Assert.That(workshop.gameObject.activeSelf, Is.False);
            world.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            Assert.That(pumpHall.gameObject.activeSelf, Is.False);
            Assert.That(workshop.gameObject.activeSelf, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));

            depot.ApplyState(DepotApproachRecoveryPresentationState.Available);
            depot.ApplyRoutePermit(granted: false);
            Assert.That(depot.IsRoutePermitLockedVisible, Is.True);
            Assert.That(depot.IsRoutePermitGrantedVisible, Is.False);
            Assert.That(
                depot.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Available));

            depot.ApplyRoutePermit(granted: true);
            Assert.That(depot.IsRoutePermitLockedVisible, Is.False);
            Assert.That(depot.IsRoutePermitGrantedVisible, Is.True);
            Assert.That(
                depot.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Available));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [TestCase(ColonyComposition.HumanOnly, true, false)]
        [TestCase(ColonyComposition.RobotOnly, false, true)]
        [TestCase(ColonyComposition.Mixed, true, true)]
        public void ColonyCompositionChangesOnlyExactWorkerVisibility(
            ColonyComposition composition,
            bool humanVisible,
            bool robotVisible)
        {
            LastBearingGameController controller = BuildController(composition);
            LastBearingOneGoodBatchCutawayView view =
                controller.World!.OneGoodBatchCutawayView!;

            view.Apply(
                batchStartAvailable: false,
                SpareBearingBatchPhase.Complete,
                SpareBearingLotCustody.WorkshopOutput,
                lotQuantity: 1,
                routePermitGranted: false,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible,
                robotVisible);

            Assert.That(view.IsHumanWorkerVisible, Is.EqualTo(humanVisible));
            Assert.That(view.IsRobotWorkerVisible, Is.EqualTo(robotVisible));
            Assert.That(view.IsBearingLotVisible, Is.True);
            Assert.That(view.BearingLot!.transform.parent, Is.SameAs(view.OutputAnchor));
        }

        private LastBearingGameController BuildController(
            ColonyComposition composition)
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(composition);
            return controller;
        }

        private static void AssertAnchor(
            Transform? anchor,
            string expectedName,
            Vector3 expectedPosition)
        {
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor!.name, Is.EqualTo(expectedName));
            Assert.That(anchor.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(anchor.localRotation, Is.EqualTo(Quaternion.identity));
        }

        private static int CountNamedParts(
            Component root,
            string namePrefix)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith(namePrefix));
        }

        private static int CountActiveNamedParts(
            Component root,
            string namePrefix)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.gameObject.activeInHierarchy &&
                               item.name.StartsWith(namePrefix));
        }
    }
}
