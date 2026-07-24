#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    /// <summary>
    /// Player-visible acceptance for the Patchwork Skid Plate presentation.
    /// Canonical installation still belongs entirely to SimulationCore.
    /// </summary>
    public sealed class LastBearingBoltTheBellyPlayModeTests
    {
        private GameObject? _root;
        private string? _temporarySaveRoot;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_temporarySaveRoot != null &&
                Directory.Exists(_temporarySaveRoot))
            {
                Directory.Delete(_temporarySaveRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator AcceptedInstallPhysicalizesBothScoutsAndPulsesOnce()
        {
            LastBearingGameController controller = BuildController();
            controller.StartNewGame(ColonyComposition.Mixed);
            yield return null;

            LastBearingWorldBuilder world = controller.World!;
            SashaScoutVisual canonicalScout =
                world.VehicleView!.ScoutVisual!;
            SashaScoutVisual roadScout = world.RoadFeelRig!.ScoutVisual;
            LastBearingGarageBayView garage = world.GarageBayView!;

            AssertSkidPlate(canonicalScout, expectedVisible: false);
            AssertSkidPlate(roadScout, expectedVisible: false);
            Assert.That(garage.RigUpgradeInstallPulseCount, Is.Zero);

            controller.InstallPatchworkSkidPlate();
            InvokeSimulationTick(controller);

            AssertSkidPlate(canonicalScout, expectedVisible: false);
            AssertSkidPlate(roadScout, expectedVisible: false);
            Assert.That(garage.RigUpgradeInstallPulseCount, Is.Zero);

            PrepareWorkingGarage(controller);
            controller.OpenGarageBay();
            Assert.That(controller.IsPatchworkSkidPlateInstallAvailable, Is.True);

            controller.InstallPatchworkSkidPlate();

            AssertSkidPlate(canonicalScout, expectedVisible: false);
            AssertSkidPlate(roadScout, expectedVisible: false);
            Assert.That(garage.RigUpgradeInstallPulseCount, Is.Zero);

            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.RigUpgrade,
                Is.EqualTo(RigUpgrade.PatchworkSkidPlate));
            AssertSkidPlate(canonicalScout, expectedVisible: true);
            AssertSkidPlate(roadScout, expectedVisible: true);
            Assert.That(garage.RigUpgradeInstallPulseCount, Is.EqualTo(1));
            Assert.That(garage.IsRigUpgradeInstallPulseActive, Is.True);
            Assert.That(
                garage.ModuleWorkLightIntensity,
                Is.GreaterThan(180f));

            controller.ShowCityOverview();
            Assert.That(garage.gameObject.activeInHierarchy, Is.False);
            yield return new WaitForSecondsRealtime(
                LastBearingGarageBayView
                    .RigUpgradeInstallPulseDurationSeconds + 0.1f);
            controller.OpenGarageBay();
            yield return null;
            Assert.That(garage.gameObject.activeInHierarchy, Is.True);
            Assert.That(garage.IsRigUpgradeInstallPulseActive, Is.False);
            Assert.That(
                garage.ModuleWorkLightIntensity,
                Is.EqualTo(180f).Within(0.01f));

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);

            Assert.That(canonicalScout.IsWinchVisible, Is.True);
            Assert.That(roadScout.IsWinchVisible, Is.True);
            Assert.That(canonicalScout.IsRangeTankVisible, Is.False);
            Assert.That(roadScout.IsRangeTankVisible, Is.False);
            Assert.That(canonicalScout.IsPatchworkSkidPlateVisible, Is.True);
            Assert.That(roadScout.IsPatchworkSkidPlateVisible, Is.True);
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(VehicleModule.WinchAssembly));
            controller.Save();

            yield return new WaitForSecondsRealtime(
                LastBearingGarageBayView
                    .RigUpgradeInstallPulseDurationSeconds + 0.1f);

            Assert.That(garage.IsRigUpgradeInstallPulseActive, Is.False);
            Assert.That(
                garage.ModuleWorkLightIntensity,
                Is.EqualTo(110f).Within(0.01f));

            int pulseCountBeforeLoad = garage.RigUpgradeInstallPulseCount;
            string installedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            AssertSkidPlate(canonicalScout, expectedVisible: false);
            AssertSkidPlate(roadScout, expectedVisible: false);

            controller.Load();

            Assert.That(controller.CanonicalHash, Is.EqualTo(installedHash));
            Assert.That(
                controller.ReadModel!.RigUpgrade,
                Is.EqualTo(RigUpgrade.PatchworkSkidPlate));
            AssertSkidPlate(canonicalScout, expectedVisible: true);
            AssertSkidPlate(roadScout, expectedVisible: true);
            Assert.That(canonicalScout.IsWinchVisible, Is.True);
            Assert.That(roadScout.IsWinchVisible, Is.True);
            Assert.That(
                garage.RigUpgradeInstallPulseCount,
                Is.EqualTo(pulseCountBeforeLoad));
            Assert.That(garage.IsRigUpgradeInstallPulseActive, Is.False);

            controller.InstallPatchworkSkidPlate();
            InvokeSimulationTick(controller);
            Assert.That(
                garage.RigUpgradeInstallPulseCount,
                Is.EqualTo(pulseCountBeforeLoad));
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller =
                _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.enabled = false;
            InstallTemporarySaveAdapter(controller);
            return controller;
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            _temporarySaveRoot = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "bolt-the-belly-" + Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(
                _temporarySaveRoot,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(_temporarySaveRoot);
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
            adapterField!.SetValue(
                controller,
                constructor!.Invoke(new object[] { store }));
        }

        private static void PrepareWorkingGarage(
            LastBearingGameController controller)
        {
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.SliceInfrastructureActive,
                Is.True);
        }

        private static void AssertSkidPlate(
            SashaScoutVisual scout,
            bool expectedVisible)
        {
            Transform? socket = scout.FindSocket(
                SashaScoutSemanticContract.UnderbodyUpgradeSocketName);
            Transform? upgrade = scout.PatchworkSkidPlateUpgradeRoot;
            Assert.That(socket, Is.Not.Null);
            Assert.That(upgrade, Is.Not.Null);
            Assert.That(
                scout.IsPatchworkSkidPlateVisible,
                Is.EqualTo(expectedVisible));
            Assert.That(
                Vector3.Distance(socket!.position, upgrade!.position),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(socket.rotation, upgrade.rotation),
                Is.LessThan(0.00001f));
            Assert.That(
                upgrade.GetComponentsInChildren<Renderer>(includeInactive: true),
                Has.Length.EqualTo(5));
            foreach (Collider collider in
                     upgrade.GetComponentsInChildren<Collider>(
                         includeInactive: true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "SimulateOneTick");
        }

        private static void InvokePrivate(
            LastBearingGameController controller,
            string methodName)
        {
            MethodInfo? method =
                typeof(LastBearingGameController).GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method!.Invoke(controller, null);
        }

        private static string GetConfinementSafeTemporaryRoot()
        {
            string root = Path.GetTempPath();
            bool mac = Application.platform == RuntimePlatform.OSXEditor ||
                       Application.platform == RuntimePlatform.OSXPlayer;
            return mac && root.StartsWith("/var/", StringComparison.Ordinal)
                ? "/private" + root
                : root;
        }
    }
}
