#nullable enable

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingServiceScoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReturnedRailRackRestoresClearsAndRecreditsOnce()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState serviceReady = CreateServiceReadyState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                7341);
            LastBearingState credited = Apply(
                serviceReady,
                sequence => new ServiceScoutCommand(sequence));
            if (credited.PauseCause == PauseCause.Explicit)
            {
                credited = Apply(
                    credited,
                    sequence => new SetPauseCommand(
                        sequence,
                        isPaused: false));
            }

            Assert.That(
                credited.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Credited));
            Assert.That(credited.PauseCause, Is.EqualTo(PauseCause.None));
            InstallControllerState(controller, credited);
            controller.ShowCityOverview();
            yield return null;

            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            byte[] creditedBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string creditedHash = controller.CanonicalHash;
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: true,
                "first credited return");

            for (var cycle = 0; cycle < 4; cycle++)
            {
                InvokeApplyPresentation(controller);
                AssertReturnedRailRackInPlayMode(
                    view,
                    expectedVisible: true,
                    "city apply " + cycle);
                controller.OpenGarageBay();
                AssertReturnedRailRackInPlayMode(
                    view,
                    expectedVisible: false,
                    "garage " + cycle);
                controller.ShowCityOverview();
                AssertReturnedRailRackInPlayMode(
                    view,
                    expectedVisible: true,
                    "city return " + cycle);
                CollectionAssert.AreEqual(
                    creditedBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(
                    controller.CanonicalHash,
                    Is.EqualTo(creditedHash));
            }

            controller.Save();
            controller.ReturnToTitle();
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: false,
                "title");
            controller.Load();
            controller.ShowCityOverview();
            yield return null;
            CollectionAssert.AreEqual(
                creditedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(creditedHash));
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: true,
                "loaded credited return");

            LastBearingState outbound = LaunchRepeat(controller.State!);
            Assert.That(
                outbound.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.WreckLine));
            InstallControllerState(controller, outbound);
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: false,
                "next repeat");

            LastBearingState atDepot = AdvanceRepeatToDepot(outbound);
            string transactionId = atDepot.TransactionId!;
            string fingerprint = atDepot.TransactionFingerprint!;
            LastBearingState returning = Apply(
                atDepot,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            LastBearingState returned =
                AdvanceRepeatToHomeApron(returning);
            long checkInSequence = returned.NextCommandSequence;
            LastBearingState recredited = new LastBearingKernel()
                .Step(
                    returned,
                    new LastBearingCommand[]
                    {
                        new CreditCityReturnCommand(
                            checkInSequence,
                            transactionId,
                            fingerprint),
                        new FinalizeExpeditionTransactionCommand(
                            checkInSequence + 1,
                            transactionId,
                            fingerprint),
                    })
                .State;
            Assert.That(
                recredited.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Credited));
            InstallControllerState(controller, recredited);
            controller.ShowCityOverview();
            yield return null;
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: true,
                "repeat credited return");

            byte[] recreditedBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string recreditedHash = controller.CanonicalHash;
            for (var apply = 0; apply < 5; apply++)
            {
                InvokeApplyPresentation(controller);
                AssertReturnedRailRackInPlayMode(
                    view,
                    expectedVisible: true,
                    "recredited apply " + apply);
            }

            CollectionAssert.AreEqual(
                recreditedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(recreditedHash));

            controller.Save();
            controller.ReturnToTitle();
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: false,
                "repeat title");
            controller.Load();
            controller.ShowCityOverview();
            yield return null;
            CollectionAssert.AreEqual(
                recreditedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(recreditedHash));
            AssertReturnedRailRackInPlayMode(
                view,
                expectedVisible: true,
                "loaded repeat credited return");
        }

        private static LastBearingState LaunchRepeat(
            LastBearingState source)
        {
            long sequence = source.NextCommandSequence;
            string suffix = sequence.ToString(
                CultureInfo.InvariantCulture);
            string transactionId = "tx:repeat:" + suffix;
            string fingerprint = "fp:repeat:" + suffix;
            return new LastBearingKernel()
                .Step(
                    source,
                    new LastBearingCommand[]
                    {
                        new PrepareRepeatExpeditionTransactionCommand(
                            sequence,
                            source.TransactionId!,
                            source.TransactionFingerprint!,
                            transactionId,
                            fingerprint),
                        new DebitCityManifestCommand(
                            sequence + 1,
                            transactionId,
                            fingerprint),
                        new DepartExpeditionCommand(sequence + 2),
                    })
                .State;
        }

        private static void AssertReturnedRailRackInPlayMode(
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
