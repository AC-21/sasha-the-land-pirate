#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingAdapterTests
    {
        [Test]
        public void ReturnedRailRackIsOnePhysicsFreeCanonicalProjection()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller =
                _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            PrepareControllerForGaragePlan(controller);
            controller.ShowCityOverview();

            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingReadModel model = controller.ReadModel!;
            string canonicalBefore = controller.CanonicalHash;
            byte[] stateBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);

            foreach (FrameRailSalvageCustody custody in new[]
                     {
                         FrameRailSalvageCustody.None,
                         FrameRailSalvageCustody.WreckLine,
                         FrameRailSalvageCustody.Vehicle,
                     })
            {
                SetReturnedRailCustody(model, custody);
                view.Apply(
                    model,
                    CityBuildingKind.None,
                    previewPadIndex: 0,
                    previewQuarterTurns: 0,
                    previewActive: false);
                AssertReturnedRailRack(
                    view,
                    expectedVisible: false,
                    custody.ToString());
            }

            SetReturnedRailCustody(
                model,
                FrameRailSalvageCustody.Credited);
            for (var apply = 0; apply < 5; apply++)
            {
                view.Apply(
                    model,
                    CityBuildingKind.None,
                    previewPadIndex: 0,
                    previewQuarterTurns: 0,
                    previewActive: false);
                AssertReturnedRailRack(
                    view,
                    expectedVisible: true,
                    "credited apply " + apply);
            }

            Transform rack = view.GetComponentsInChildren<Transform>(true)
                .Single(item =>
                    item.name ==
                    LastBearingCityServiceCellView.ReturnedRailRackName);
            Assert.That(
                rack.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty);
            foreach (Collider collider in
                     rack.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }

            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            CollectionAssert.AreEqual(
                stateBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));
        }

        private static void SetReturnedRailCustody(
            LastBearingReadModel model,
            FrameRailSalvageCustody custody)
        {
            PropertyInfo? property = typeof(LastBearingReadModel).GetProperty(
                nameof(LastBearingReadModel.FrameRailSalvageCustody));
            MethodInfo? setter = property?.GetSetMethod(nonPublic: true);
            Assert.That(setter, Is.Not.Null);
            setter!.Invoke(model, new object[] { custody });
        }

        private static void AssertReturnedRailRack(
            LastBearingCityServiceCellView view,
            bool expectedVisible,
            string context)
        {
            Transform[] descendants =
                view.GetComponentsInChildren<Transform>(true);
            Transform[] racks = descendants
                .Where(item =>
                    item.name ==
                    LastBearingCityServiceCellView.ReturnedRailRackName)
                .ToArray();
            Transform[] rails = descendants
                .Where(item => item.name.StartsWith(
                    LastBearingCityServiceCellView
                        .ReturnedFrameRailNamePrefix,
                    StringComparison.Ordinal))
                .ToArray();

            Assert.That(racks, Has.Length.EqualTo(1), context);
            Assert.That(rails, Has.Length.EqualTo(4), context);
            Assert.That(
                rails.Count(item => item.gameObject.activeInHierarchy),
                Is.EqualTo(expectedVisible ? 4 : 0),
                context);
            Assert.That(
                view.IsReturnedRailRackVisible,
                Is.EqualTo(expectedVisible),
                context);
        }
    }
}
