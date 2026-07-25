#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingOnePairOfHandsSourceTests
    {
        [Test]
        public void ControllerRoutesBothPreparationFlagsThroughTheWorld()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string world = RuntimeSource(
                "LastBearingWorldBuilder.cs");
            string garage = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingGarageBayView.cs"));

            Assert.That(
                controller,
                Does.Contain(
                    "_readModel.IsPreparationStalledByHotShift,"));
            Assert.That(
                controller,
                Does.Contain(
                    "_readModel.IsPreparationActivelyWorking);"));
            Assert.That(
                world,
                Does.Contain("bool stalledByHotShift,"));
            Assert.That(
                world,
                Does.Contain("bool activelyWorking)"));
            Assert.That(
                world,
                Does.Contain(
                    "GarageBayView?.ApplyPreparationProgress("));
            Assert.That(
                world,
                Does.Contain(
                    "stalledByHotShift,\n                activelyWorking);"));
            Assert.That(
                garage,
                Does.Contain(
                    "public bool IsPreparationGaugeHeldByHotShift"));
            Assert.That(
                garage,
                Does.Contain(
                    "public bool IsPreparationGaugeActivelyWorking"));
        }

        [Test]
        public void ServiceCellProjectsOnePairOfHandsIntoPhysicalOwnership()
        {
            string serviceCell = RuntimeSource(
                "LastBearingCityServiceCellView.cs");

            Assert.That(
                serviceCell,
                Does.Contain(
                    "model.IsPreparationActivelyWorking;"));
            Assert.That(
                serviceCell,
                Does.Contain(
                    "bool operatorAtMachine =\n" +
                    "                !workshopPushPreparationWorking;"));
            Assert.That(
                serviceCell,
                Does.Contain(
                    "_workshopPushTransferArm.SetActive(\n" +
                    "                workshopPushPreparationWorking);"));
        }

        [Test]
        public void FieldDeskStatesTheExactShiftAndSingleSlotTrade()
        {
            string presenter = RuntimeSource(
                Path.Combine(
                    "UI",
                    "LastBearingFieldDeskPresenter.cs"));

            Assert.That(
                presenter,
                Does.Contain(
                    "1 fuel powers 120 settlement ticks for +2 parts " +
                    "at -0.010 water per tick."));
            Assert.That(
                presenter,
                Does.Contain(
                    "The active shift owns the single machine-shop " +
                    "service slot; Workshop Push preparation is held " +
                    "and the garage gauge is frozen."));
        }

        [Test]
        public void RuntimePresentationContainsNoLegacyWorkshopPushStall()
        {
            string runtimeRoot = Path.Combine(
                Application.dataPath,
                "AtomicLandPirate",
                "LastBearing",
                "Runtime");
            foreach (string path in Directory.GetFiles(
                         runtimeRoot,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                Assert.That(
                    File.ReadAllText(path),
                    Does.Not.Contain(
                        "IsHotShiftStalledByWorkshopPush"),
                    path);
            }
        }

        [Test]
        public void GaugeCachesHousingAndChangesOnlyDerivedMaterials()
        {
            string garage = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingGarageBayView.cs"));
            string build = MethodBody(
                garage,
                "private void BuildPreparationGauge(");
            string apply = MethodBody(
                garage,
                "public void ApplyPreparationProgress(");

            Assert.That(
                garage,
                Does.Contain(
                    "private Renderer? _preparationGaugeHousingRenderer;"));
            Assert.That(
                build,
                Does.Contain(
                    "_preparationGaugeHousingRenderer =\n" +
                    "                housing.GetComponent<Renderer>();"));
            Assert.That(
                apply,
                Does.Contain(
                    "_preparationGaugeHousingRenderer.sharedMaterial =\n" +
                    "                    gaugeStateMaterial;"));
            Assert.That(
                apply,
                Does.Not.Contain(".material ="));
            AssertFrameMethodDoesNotOwnGauge(
                garage,
                "private void Update(");
            AssertFrameMethodDoesNotOwnGauge(
                garage,
                "private void FixedUpdate(");
        }

        private static void AssertFrameMethodDoesNotOwnGauge(
            string source,
            string signature)
        {
            string? body = TryMethodBody(source, signature);
            if (body == null)
            {
                return;
            }

            Assert.That(body, Does.Not.Contain("_preparationGauge"));
            Assert.That(
                body,
                Does.Not.Contain("PreparationProgressNormalized"));
            Assert.That(
                body,
                Does.Not.Contain("PreparationGaugeLitSegments"));
            Assert.That(
                body,
                Does.Not.Contain("IsPreparationGaugeHeldByHotShift"));
            Assert.That(
                body,
                Does.Not.Contain("IsPreparationGaugeActivelyWorking"));
        }

        private static string MethodBody(
            string source,
            string signature)
        {
            string? body = TryMethodBody(source, signature);
            Assert.That(
                body,
                Is.Not.Null,
                "missing method " + signature);
            return body!;
        }

        private static string? TryMethodBody(
            string source,
            string signature)
        {
            int signatureIndex = source.IndexOf(
                signature,
                StringComparison.Ordinal);
            if (signatureIndex < 0)
            {
                return null;
            }

            int bodyStart = source.IndexOf(
                '{',
                signatureIndex);
            Assert.That(
                bodyStart,
                Is.GreaterThan(signatureIndex),
                "missing body for " + signature);
            int depth = 0;
            for (int index = bodyStart; index < source.Length; index++)
            {
                switch (source[index])
                {
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        if (depth == 0)
                        {
                            return source.Substring(
                                bodyStart,
                                index - bodyStart + 1);
                        }

                        break;
                }
            }

            Assert.Fail("unterminated body for " + signature);
            return null;
        }

        private static string RuntimeSource(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AtomicLandPirate",
                    "LastBearing",
                    "Runtime",
                    relativePath));
        }
    }
}
