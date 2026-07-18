#nullable enable

using System;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingDepotCargoLoadingPresentationTests
    {
        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void AuthoredLoadPointIsOnePhysicsFreeDerivedView()
        {
            LastBearingGameController controller = BuildController();
            LastBearingDepotCargoLoadingView view =
                controller.World!.DepotCargoLoadingView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(
                LastBearingDepotCargoLoadingView.DirectionPackageId,
                Is.EqualTo("C0-VGR-10"));
            Assert.That(
                LastBearingDepotCargoLoadingView.Revision,
                Is.EqualTo("R1"));
            Assert.That(
                LastBearingDepotCargoLoadingView.ContentId,
                Is.EqualTo("poi_depot_repair_cargo_load_a"));
            Assert.That(view.name, Is.EqualTo(
                LastBearingDepotCargoLoadingView.RootName));
            Assert.That(view.InteractionAnchor, Is.Not.Null);
            Assert.That(
                view.InteractionAnchor!.name,
                Is.EqualTo(
                    LastBearingDepotCargoLoadingView.InteractionAnchorName));
            Assert.That(
                _root!.GetComponentsInChildren<LastBearingDepotCargoLoadingView>(
                    includeInactive: true),
                Has.Length.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);
            foreach (Collider collider in view.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }

            Assert.That(
                _root.GetComponentsInChildren<Camera>(includeInactive: true),
                Has.Length.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void ExactCargoAndCustodySelectOneTruthfulPhysicalSourceOrScoutLoad()
        {
            LastBearingGameController controller = BuildController();
            LastBearingDepotCargoLoadingView view =
                controller.World!.DepotCargoLoadingView!;
            string canonicalBefore = controller.CanonicalHash;

            view.Apply(RepairCargoKind.None, RepairCargoCustody.None);
            AssertState(view, DepotCargoLoadingPresentationState.Dormant);

            view.Apply(
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Depot);
            AssertState(
                view,
                DepotCargoLoadingPresentationState.CeramicBearingAtDepot,
                ceramicAtDepot: true);

            view.Apply(
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Faction);
            AssertState(
                view,
                DepotCargoLoadingPresentationState.CeramicBearingAtFaction,
                ceramicAtFaction: true);

            view.Apply(
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Faction);
            AssertState(
                view,
                DepotCargoLoadingPresentationState.FieldSleeveAtFaction,
                fieldSleeveAtFaction: true);

            view.Apply(
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Vehicle);
            AssertState(
                view,
                DepotCargoLoadingPresentationState.CeramicBearingOnVehicle,
                canonicalCeramic: true,
                roadCeramic: true);

            view.Apply(
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Vehicle);
            AssertState(
                view,
                DepotCargoLoadingPresentationState.FieldSleeveOnVehicle,
                canonicalSleeve: true,
                roadSleeve: true);

            view.Apply(
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Turbine);
            AssertState(view, DepotCargoLoadingPresentationState.Applied);
            view.Apply(
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Consumed);
            AssertState(view, DepotCargoLoadingPresentationState.Applied);

            Assert.Throws<InvalidOperationException>(() => view.Apply(
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Consumed));
            Assert.Throws<InvalidOperationException>(() => view.Apply(
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Depot));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void CanonicalAndRoadCargoProxiesUseTheAuthoredScoutSocket()
        {
            LastBearingGameController controller = BuildController();
            LastBearingWorldBuilder world = controller.World!;
            Transform canonicalCargo = RequireNamed(
                world.VehicleView!.transform,
                "Canonical Scout Ceramic Bearing Load");
            Transform roadCargo = RequireNamed(
                world.RoadFeelRig!.Root.transform,
                "Road Scout Ceramic Bearing Load");

            Assert.That(
                canonicalCargo.parent!.name,
                Is.EqualTo(SashaScoutSemanticContract.CargoSocket01Name));
            Assert.That(
                roadCargo.parent!.name,
                Is.EqualTo(SashaScoutSemanticContract.CargoSocket01Name));
            Assert.That(canonicalCargo.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(roadCargo.localPosition, Is.EqualTo(Vector3.zero));
            AssertPhysicsFree(canonicalCargo);
            AssertPhysicsFree(roadCargo);
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            return controller;
        }

        private static void AssertState(
            LastBearingDepotCargoLoadingView view,
            DepotCargoLoadingPresentationState expected,
            bool ceramicAtDepot = false,
            bool ceramicAtFaction = false,
            bool fieldSleeveAtFaction = false,
            bool canonicalCeramic = false,
            bool canonicalSleeve = false,
            bool roadCeramic = false,
            bool roadSleeve = false)
        {
            Assert.That(view.State, Is.EqualTo(expected));
            Assert.That(
                view.IsCeramicBearingAtDepotVisible,
                Is.EqualTo(ceramicAtDepot));
            Assert.That(
                view.IsCeramicBearingAtFactionVisible,
                Is.EqualTo(ceramicAtFaction));
            Assert.That(
                view.IsFieldSleeveAtFactionVisible,
                Is.EqualTo(fieldSleeveAtFaction));
            Assert.That(
                view.IsCanonicalCeramicBearingVisible,
                Is.EqualTo(canonicalCeramic));
            Assert.That(
                view.IsCanonicalFieldSleeveVisible,
                Is.EqualTo(canonicalSleeve));
            Assert.That(
                view.IsRoadCeramicBearingVisible,
                Is.EqualTo(roadCeramic));
            Assert.That(
                view.IsRoadFieldSleeveVisible,
                Is.EqualTo(roadSleeve));
            Assert.That(
                view.IsLoadAvailable,
                Is.EqualTo(
                    ceramicAtDepot || ceramicAtFaction ||
                    fieldSleeveAtFaction));
        }

        private static Transform RequireNamed(Transform root, string name)
        {
            foreach (Transform candidate in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            throw new AssertionException("Missing authored transform: " + name);
        }

        private static void AssertPhysicsFree(Transform root)
        {
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }
        }
    }
}
