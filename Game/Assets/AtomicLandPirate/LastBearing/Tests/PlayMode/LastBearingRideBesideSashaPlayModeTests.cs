#nullable enable

using System.Collections;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingPlayModeTests
    {
        [UnityTest]
        public IEnumerator RoadHandRidesTheWholeLoopAndReturnsToOneCityBody()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            _ = InstallTemporarySaveAdapter(controller);
            LastBearingState atHome =
                CreateAtHomeModuleState(VehicleModule.WinchAssembly);
            InstallControllerState(controller, atHome);
            controller.OpenGarageBay();
            yield return null;

            LastBearingWorldBuilder world = controller.World!;
            SashaScoutVisual canonicalScout =
                world.VehicleView!.ScoutVisual!;
            SashaScoutVisual roadScout = world.RoadFeelRig!.ScoutVisual;
            Transform canonicalHuman = canonicalScout.HumanRoadHandRoot!;
            Transform canonicalRobot =
                canonicalScout.UtilityRobotRoadHandRoot!;
            Transform roadHuman = roadScout.HumanRoadHandRoot!;
            Transform roadRobot = roadScout.UtilityRobotRoadHandRoot!;
            byte[] atHomeBytes = LastBearingCanonicalCodec.Encode(
                controller.State!);

            Assert.That(
                world.GarageBayView!.ActiveRoadHandManifest,
                Is.EqualTo(GarageRoadHandManifestPresentation.Human));
            AssertRoadHand(
                canonicalScout,
                SashaScoutRoadHandPresentation.None);
            AssertRoadHand(
                roadScout,
                SashaScoutRoadHandPresentation.None);
            Assert.That(
                world.CityServiceCellView!.IsHumanOperatorVisible,
                Is.True);

            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.ShowCityOverview();
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                controller.OpenGarageBay();
                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                CollectionAssert.AreEqual(
                    atHomeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                AssertRoadHand(
                    canonicalScout,
                    SashaScoutRoadHandPresentation.None);
                AssertRoadHand(
                    roadScout,
                    SashaScoutRoadHandPresentation.None);
            }

            var kernel = new LastBearingKernel();
            const string transactionId = "tx:ride-beside-sasha:playmode";
            const string fingerprint = "fp:ride-beside-sasha:playmode";
            LastBearingState outbound = Apply(
                kernel,
                atHome,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            outbound = Apply(
                kernel,
                outbound,
                sequence => new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            InstallControllerState(controller, outbound);
            AssertLoopPhase(
                controller,
                ExpeditionPhase.Outbound,
                LastBearingPresentationMode.Driving,
                canonicalScout,
                roadScout);
            Assert.That(
                world.CityServiceCellView.IsHumanOperatorVisible,
                Is.False);

            byte[] outboundBytes = LastBearingCanonicalCodec.Encode(
                controller.State!);
            string outboundHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            AssertRoadHand(
                canonicalScout,
                SashaScoutRoadHandPresentation.None);
            AssertRoadHand(
                roadScout,
                SashaScoutRoadHandPresentation.None);
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(outboundHash));
            CollectionAssert.AreEqual(
                outboundBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            AssertLoopPhase(
                controller,
                ExpeditionPhase.Outbound,
                LastBearingPresentationMode.Driving,
                canonicalScout,
                roadScout);

            LastBearingState recoveryGate =
                DriveUntilDepotRecoveryAvailable(controller.State!);
            LastBearingState atDepot = Apply(
                kernel,
                recoveryGate,
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            InstallControllerState(controller, atDepot);
            AssertLoopPhase(
                controller,
                ExpeditionPhase.AtDepot,
                LastBearingPresentationMode.DepotEncounter,
                canonicalScout,
                roadScout);

            LastBearingState resolved = Apply(
                kernel,
                atDepot,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));
            LastBearingState loaded = Apply(
                kernel,
                resolved,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            LastBearingState returning = Apply(
                kernel,
                loaded,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            InstallControllerState(controller, returning);
            AssertLoopPhase(
                controller,
                ExpeditionPhase.Returning,
                LastBearingPresentationMode.Driving,
                canonicalScout,
                roadScout);

            LastBearingState returned = DriveUntilPhase(
                returning,
                ExpeditionPhase.Returned);
            InstallControllerState(controller, returned);
            AssertLoopPhase(
                controller,
                ExpeditionPhase.Returned,
                LastBearingPresentationMode.CityReturn,
                canonicalScout,
                roadScout);

            byte[] returnedBytes = LastBearingCanonicalCodec.Encode(
                controller.State!);
            string returnedHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(returnedHash));
            CollectionAssert.AreEqual(
                returnedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            AssertLoopPhase(
                controller,
                ExpeditionPhase.Returned,
                LastBearingPresentationMode.CityReturn,
                canonicalScout,
                roadScout);

            controller.CompleteReturn();
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtHome));
            AssertRoadHand(
                canonicalScout,
                SashaScoutRoadHandPresentation.None);
            AssertRoadHand(
                roadScout,
                SashaScoutRoadHandPresentation.None);
            Assert.That(
                world.CityServiceCellView.IsHumanOperatorVisible,
                Is.True);
            Assert.That(
                world.GarageBayView.ActiveRoadHandManifest,
                Is.EqualTo(GarageRoadHandManifestPresentation.Human));
            Assert.That(
                canonicalScout.HumanRoadHandRoot,
                Is.SameAs(canonicalHuman));
            Assert.That(
                canonicalScout.UtilityRobotRoadHandRoot,
                Is.SameAs(canonicalRobot));
            Assert.That(roadScout.HumanRoadHandRoot, Is.SameAs(roadHuman));
            Assert.That(
                roadScout.UtilityRobotRoadHandRoot,
                Is.SameAs(roadRobot));
        }

        private static void AssertLoopPhase(
            LastBearingGameController controller,
            ExpeditionPhase expectedPhase,
            LastBearingPresentationMode expectedMode,
            SashaScoutVisual canonicalScout,
            SashaScoutVisual roadScout)
        {
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(expectedPhase));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(expectedMode));
            AssertRoadHand(
                canonicalScout,
                SashaScoutRoadHandPresentation.Human);
            AssertRoadHand(
                roadScout,
                SashaScoutRoadHandPresentation.Human);
            Assert.That(
                controller.World!.GarageBayView!.ActiveRoadHandManifest,
                Is.EqualTo(GarageRoadHandManifestPresentation.Human));
            Assert.That(
                controller.World.CityServiceCellView!.IsHumanOperatorVisible,
                Is.False);
        }

        private static void AssertRoadHand(
            SashaScoutVisual scout,
            SashaScoutRoadHandPresentation expected)
        {
            Assert.That(scout.RoadHand, Is.EqualTo(expected));
            Assert.That(
                scout.IsHumanRoadHandVisible,
                Is.EqualTo(expected == SashaScoutRoadHandPresentation.Human));
            Assert.That(
                scout.IsUtilityRobotRoadHandVisible,
                Is.EqualTo(
                    expected ==
                    SashaScoutRoadHandPresentation.UtilityRobot));
            Assert.That(
                scout.HumanRoadHandRoot!
                    .GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                scout.UtilityRobotRoadHandRoot!
                    .GetComponentsInChildren<Collider>(true),
                Is.Empty);
        }
    }
}
