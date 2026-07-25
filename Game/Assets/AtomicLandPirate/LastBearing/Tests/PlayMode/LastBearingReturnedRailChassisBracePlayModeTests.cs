#nullable enable

using System.Collections;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingServiceScoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator CreditedRackBecomesOneDurableScoutBrace()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState serviceReady = CreateServiceReadyState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                7381);
            LastBearingState braceReady = Apply(
                serviceReady,
                sequence => new ServiceScoutCommand(sequence));
            InstallControllerState(controller, braceReady);
            controller.ShowCityOverview();
            yield return null;

            LastBearingReadModel readyModel = controller.ReadModel!;
            LastBearingWorldBuilder world = controller.World!;
            LastBearingReturnedRailChassisBraceInteractor jig =
                world.ReturnedRailChassisBraceInteractor!;
            SashaScoutVisual canonicalScout =
                world.VehicleView!.ScoutVisual!;
            SashaScoutVisual roadScout =
                world.RoadFeelRig!.ScoutVisual;
            LastBearingCityServiceCellView serviceCell =
                world.CityServiceCellView!;

            Assert.That(
                readyModel.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Credited));
            Assert.That(
                readyModel.IsReturnedRailChassisBraceInstallAvailable,
                Is.True);
            Assert.That(
                readyModel.NextObjective,
                Is.EqualTo("install-returned-rail-chassis-brace"));
            Assert.That(serviceCell.IsReturnedRailRackVisible, Is.True);
            Assert.That(
                LastBearingFieldDeskPresenter
                    .Present(controller)
                    .PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent
                        .OpenReturnedRailChassisBraceJig));
            AssertBraceTopology(canonicalScout, expectedVisible: false);
            AssertBraceTopology(roadScout, expectedVisible: false);
            Assert.That(
                canonicalScout.IsPatchworkSkidPlateVisible,
                Is.True,
                "the returned-rail brace must not replace the skid plate");

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            controller.OpenReturnedRailChassisBraceJig();
            yield return null;
            InvokeBraceJigUpdate(jig);
            Assert.That(jig.IsControlFocused, Is.True);
            Assert.That(jig.IsInputArmed, Is.False);
            Assert.That(jig.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            yield return null;
            InvokeBraceJigUpdate(jig);
            Assert.That(jig.IsInputArmed, Is.True);
            Assert.That(
                controller.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));

            byte[] beforeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            long beforeParts = controller.State!.PartsUnits;
            long beforeLoss =
                readyModel.ProjectedRoundTripConditionLossMilli;
            Vector3 screen = world.MainCamera!.WorldToScreenPoint(
                jig.ControlWorldPosition);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(
                jig.TryActivateAtScreenPosition(
                    new Vector2(screen.x, screen.y)),
                Is.True);
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<InstallReturnedRailChassisBraceCommand>());
            controller.InstallReturnedRailChassisBrace();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "duplicate input queued a second brace");
            CollectionAssert.AreEqual(
                beforeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            LastBearingReadModel installed = controller.ReadModel!;
            Assert.That(
                installed.ReturnedRailChassisBraceInstalled,
                Is.True);
            Assert.That(
                installed.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.None));
            Assert.That(
                installed.PartsUnits,
                Is.EqualTo(
                    beforeParts -
                    readyModel.ReturnedRailChassisBracePartsCostUnits));
            Assert.That(
                installed.ProjectedRoundTripConditionLossMilli,
                Is.EqualTo(
                    beforeLoss -
                    readyModel.ReturnedRailChassisBraceProtectionMilli));
            Assert.That(serviceCell.IsReturnedRailRackVisible, Is.False);
            AssertBraceTopology(canonicalScout, expectedVisible: true);
            AssertBraceTopology(roadScout, expectedVisible: true);
            Assert.That(jig.IsAcceptedReceiptVisible, Is.True);
            Assert.That(controller.SaveStatus, Does.Not.Contain("Unsaved"));
            AssertExactlyOnePresentedBrace(
                canonicalScout,
                roadScout);

            byte[] installedBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string installedHash = controller.CanonicalHash;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.ShowCityOverview();
                AssertExactlyOnePresentedBrace(
                    canonicalScout,
                    roadScout);
                controller.OpenGarageBay();
                AssertExactlyOnePresentedBrace(
                    canonicalScout,
                    roadScout);
                CollectionAssert.AreEqual(
                    installedBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(
                    controller.CanonicalHash,
                    Is.EqualTo(installedHash));
            }

            controller.ReturnToTitle();
            Assert.That(
                canonicalScout.IsReturnedRailChassisBraceVisible,
                Is.False);
            Assert.That(
                roadScout.IsReturnedRailChassisBraceVisible,
                Is.False);
            controller.Load();
            CollectionAssert.AreEqual(
                installedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(installedHash));
            yield return null;
            Assert.That(
                controller.ReadModel!.ReturnedRailChassisBraceInstalled,
                Is.True);
            AssertBraceTopology(canonicalScout, expectedVisible: true);
            AssertBraceTopology(roadScout, expectedVisible: true);
            Assert.That(serviceCell.IsReturnedRailRackVisible, Is.False);
        }

        private static void AssertBraceTopology(
            SashaScoutVisual scout,
            bool expectedVisible)
        {
            Transform? brace =
                scout.ReturnedRailChassisBraceUpgradeRoot;
            Assert.That(brace, Is.Not.Null);
            Assert.That(
                scout.IsReturnedRailChassisBraceVisible,
                Is.EqualTo(expectedVisible));
            Assert.That(
                brace!.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(10));
            Assert.That(
                brace.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty);
            foreach (Collider collider in
                     brace.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }
        }

        private static void AssertExactlyOnePresentedBrace(
            SashaScoutVisual canonicalScout,
            SashaScoutVisual roadScout)
        {
            int active =
                (canonicalScout.ReturnedRailChassisBraceUpgradeRoot!
                    .gameObject.activeInHierarchy ? 1 : 0) +
                (roadScout.ReturnedRailChassisBraceUpgradeRoot!
                    .gameObject.activeInHierarchy ? 1 : 0);
            Assert.That(active, Is.EqualTo(1));
        }

        private static void InvokeBraceJigUpdate(
            LastBearingReturnedRailChassisBraceInteractor jig)
        {
            MethodInfo? update = typeof(
                    LastBearingReturnedRailChassisBraceInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update!.Invoke(jig, null);
        }
    }
}
