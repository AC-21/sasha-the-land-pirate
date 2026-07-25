#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingRoadSafeLineEditModeTests
    {
        private static readonly string[] SurfaceNames =
        {
            LastBearingWorldBuilder.RouteApronName,
            LastBearingWorldBuilder.CollapsedShortBranchName,
            LastBearingWorldBuilder.ExposedLongRouteAName,
            LastBearingWorldBuilder.ExposedLongRouteBName,
        };

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
        public void OnePhysicsFreeViewOwnsPairedLinesAcrossFourSurfaces()
        {
            LastBearingGameController controller = CreateController();
            LastBearingWorldBuilder world = controller.World!;
            LastBearingRoadSafeLineView view = world.RoadSafeLineView!;
            Transform corridor = RequireNamed(
                world.transform,
                LastBearingWorldBuilder.RoadCorridorRootName);

            Assert.That(
                world.GetComponentsInChildren<LastBearingRoadSafeLineView>(
                    includeInactive: true),
                Has.Length.EqualTo(1));
            Assert.That(view.transform.parent, Is.SameAs(corridor));
            Assert.That(
                view.GetComponentsInChildren<RoadFeelSurface>(true),
                Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<Camera>(true),
                Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<AudioListener>(true),
                Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<Light>(true),
                Is.Empty);

            Transform[] descendants =
                view.GetComponentsInChildren<Transform>(true);
            Transform[] segmentRoots = descendants.Where(item =>
                item.name.StartsWith(
                    LastBearingRoadSafeLineView.SegmentRootPrefix,
                    StringComparison.Ordinal)).ToArray();
            Transform[] leftLines = descendants.Where(item =>
                item.name.StartsWith(
                    LastBearingRoadSafeLineView.LeftSafeLinePrefix,
                    StringComparison.Ordinal)).ToArray();
            Transform[] rightLines = descendants.Where(item =>
                item.name.StartsWith(
                    LastBearingRoadSafeLineView.RightSafeLinePrefix,
                    StringComparison.Ordinal)).ToArray();
            Transform[] leftWarnings = descendants.Where(item =>
                item.name.StartsWith(
                    LastBearingRoadSafeLineView.LeftWarningPrefix,
                    StringComparison.Ordinal)).ToArray();
            Transform[] rightWarnings = descendants.Where(item =>
                item.name.StartsWith(
                    LastBearingRoadSafeLineView.RightWarningPrefix,
                    StringComparison.Ordinal)).ToArray();

            Assert.That(
                segmentRoots,
                Has.Length.EqualTo(
                    LastBearingRoadSafeLineView.SurfaceCount));
            Assert.That(
                leftLines.Concat(rightLines).Count(),
                Is.EqualTo(
                    LastBearingRoadSafeLineView.SafeLineMarkerCount));
            Assert.That(
                leftWarnings.Concat(rightWarnings).Count(),
                Is.EqualTo(
                    LastBearingRoadSafeLineView.WarningWitnessCount));

            for (var index = 0; index < SurfaceNames.Length; index++)
            {
                Transform surface = RequireNamed(corridor, SurfaceNames[index]);
                Transform segment = segmentRoots.Single(item =>
                    item.name ==
                    LastBearingRoadSafeLineView.SegmentRootPrefix +
                    index.ToString("00"));
                Assert.That(
                    Vector3.Distance(
                        segment.localPosition,
                        surface.localPosition),
                    Is.LessThan(0.00001f),
                    SurfaceNames[index]);
                Assert.That(
                    Quaternion.Angle(
                        segment.localRotation,
                        surface.localRotation),
                    Is.LessThan(0.001f),
                    SurfaceNames[index]);
                Assert.That(
                    Vector3.Distance(
                        segment.localScale,
                        surface.localScale),
                    Is.LessThan(0.00001f),
                    SurfaceNames[index]);
            }

            Renderer[] safeRenderers = leftLines
                .Concat(rightLines)
                .Select(item => item.GetComponent<Renderer>())
                .ToArray();
            Renderer[] warningRenderers = leftWarnings
                .Concat(rightWarnings)
                .Select(item => item.GetComponent<Renderer>())
                .ToArray();
            Assert.That(
                safeRenderers.Select(item => item.sharedMaterial)
                    .Distinct()
                    .Count(),
                Is.EqualTo(1));
            Assert.That(
                warningRenderers.Select(item => item.sharedMaterial)
                    .Distinct()
                    .Count(),
                Is.EqualTo(1));
            Assert.That(
                warningRenderers[0].sharedMaterial,
                Is.Not.SameAs(safeRenderers[0].sharedMaterial));
            Assert.That(
                view.GetComponentsInChildren<Collider>(true),
                Is.Empty);
        }

        [Test]
        public void CanonicalThresholdsChangeOnlyTheDerivedWitness()
        {
            LastBearingGameController controller = CreateController();
            LastBearingRoadSafeLineView view =
                controller.World!.RoadSafeLineView!;
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                7626);
            byte[] canonicalBefore = LastBearingCanonicalCodec.Encode(state);
            LastBearingReadModel model = LastBearingReadModel.FromState(state);

            view.Apply(model, isDrivingMode: true);
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Safe));
            AssertAllMarkersActive(view, expected: true);
            int transitions = view.WarningTransitionCount;

            SetLateral(model, -750);
            view.Apply(model, isDrivingMode: true);
            SetLateral(model, 750);
            view.Apply(model, isDrivingMode: true);
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Safe));
            Assert.That(view.WarningTransitionCount, Is.EqualTo(transitions));

            SetLateral(model, 751);
            view.Apply(model, isDrivingMode: true);
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.RightRisk));
            Assert.That(view.IsRightWarningVisible, Is.True);
            Assert.That(view.IsLeftWarningVisible, Is.False);
            int rightTransitions = view.WarningTransitionCount;
            view.Apply(model, isDrivingMode: true);
            Assert.That(
                view.WarningTransitionCount,
                Is.EqualTo(rightTransitions));

            SetLateral(model, -751);
            view.Apply(model, isDrivingMode: true);
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.LeftRisk));
            Assert.That(view.IsLeftWarningVisible, Is.True);
            Assert.That(view.IsRightWarningVisible, Is.False);

            view.Apply(model, isDrivingMode: false);
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Clear));
            AssertAllMarkersActive(view, expected: false);
            view.Apply(null, isDrivingMode: true);
            AssertAllMarkersActive(view, expected: false);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(state));
        }

        private LastBearingGameController CreateController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller =
                _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            return controller;
        }

        private static void SetLateral(
            LastBearingReadModel model,
            int value)
        {
            PropertyInfo? property = typeof(LastBearingReadModel).GetProperty(
                nameof(LastBearingReadModel.VehicleLateralMilli));
            MethodInfo? setter = property?.GetSetMethod(nonPublic: true);
            Assert.That(setter, Is.Not.Null);
            setter!.Invoke(model, new object[] { value });
        }

        private static Transform RequireNamed(
            Transform root,
            string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName);
        }

        private static void AssertAllMarkersActive(
            LastBearingRoadSafeLineView view,
            bool expected)
        {
            Transform[] markers = view.GetComponentsInChildren<Transform>(true)
                .Where(item =>
                    item.name.StartsWith(
                        LastBearingRoadSafeLineView.LeftSafeLinePrefix,
                        StringComparison.Ordinal) ||
                    item.name.StartsWith(
                        LastBearingRoadSafeLineView.RightSafeLinePrefix,
                        StringComparison.Ordinal) ||
                    item.name.StartsWith(
                        LastBearingRoadSafeLineView.LeftWarningPrefix,
                        StringComparison.Ordinal) ||
                    item.name.StartsWith(
                        LastBearingRoadSafeLineView.RightWarningPrefix,
                        StringComparison.Ordinal))
                .ToArray();
            Assert.That(
                markers.Count(item => item.gameObject.activeInHierarchy),
                Is.EqualTo(
                    expected
                        ? LastBearingRoadSafeLineView.SafeLineMarkerCount
                        : 0));
        }
    }
}
