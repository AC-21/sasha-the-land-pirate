#nullable enable

using System.Collections;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingOneGoodBatchPlayModeTests
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

        [UnityTest]
        public IEnumerator SharedInspectionCameraReadsMachineMotionAndCustody()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.enabled = false;
            LastBearingWorldBuilder world = controller.World!;
            LastBearingOneGoodBatchCutawayView view =
                world.OneGoodBatchCutawayView!;
            string canonicalBefore = controller.CanonicalHash;

            world.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            yield return new WaitForSecondsRealtime(1f);

            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            Assert.That(world.CameraRig!.IsInspectionMode, Is.True);
            Assert.That(
                Vector3.Distance(
                    world.MainCamera!.transform.position,
                    view.CameraAnchor!.position),
                Is.LessThan(0.15f));
            Assert.That(
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include),
                Has.Length.EqualTo(1));

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
            Quaternion spindleBefore = view.MachineSpindle!.localRotation;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(
                Quaternion.Angle(
                    spindleBefore,
                    view.MachineSpindle.localRotation),
                Is.GreaterThan(0.5f));
            Assert.That(view.IsWorkpieceVisible, Is.True);

            GameObject lot = view.BearingLot!;
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
            Assert.That(lot.transform.parent, Is.SameAs(view.OutputAnchor));
            Assert.That(view.IsMachineRunning, Is.False);

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
            yield return null;

            Assert.That(view.BearingLot, Is.SameAs(lot));
            Assert.That(lot.transform.parent, Is.SameAs(view.ClaimsAnchor));
            Assert.That(view.IsPermitGrantedVisible, Is.True);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [UnityTest]
        public IEnumerator PermitStateSurvivesCutawayToggleWithoutReplacingCamera()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.RobotOnly);
            controller.enabled = false;
            LastBearingWorldBuilder world = controller.World!;
            LastBearingOneGoodBatchCutawayView workshop =
                world.OneGoodBatchCutawayView!;
            string canonicalBefore = controller.CanonicalHash;

            world.ApplyOneGoodBatch(
                batchStartAvailable: false,
                SpareBearingBatchPhase.Settled,
                SpareBearingLotCustody.LastBearingClaimsCounter,
                lotQuantity: 1,
                routePermitGranted: true,
                futureRouteTollFuelUnits:
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                humanVisible: false,
                robotVisible: true);
            world.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            yield return null;

            Assert.That(workshop.IsPermitGrantedVisible, Is.True);
            Assert.That(
                world.DepotApproachRecoveryView!.IsRoutePermitGrantedVisible,
                Is.True);
            Assert.That(workshop.IsHumanWorkerVisible, Is.False);
            Assert.That(workshop.IsRobotWorkerVisible, Is.True);

            world.SelectPumpHallCutaway();
            controller.OpenBuildingCutaway();
            yield return null;
            world.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            yield return null;

            Assert.That(workshop.IsPermitGrantedVisible, Is.True);
            Assert.That(
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }
    }
}
