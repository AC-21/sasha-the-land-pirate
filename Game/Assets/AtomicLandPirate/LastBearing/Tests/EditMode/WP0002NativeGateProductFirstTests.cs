#nullable enable

using System;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
using NUnit.Framework;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class WP0002NativeGateProductFirstTests
    {
        [Test]
        public void NativeGateUsesCurrentCallerDigestWithoutFrozenGameplayPins()
        {
            const BindingFlags privateStatic =
                BindingFlags.NonPublic | BindingFlags.Static;
            Type dispatcher = typeof(WP0002GateDispatcher);

            Assert.That(
                dispatcher.GetField("NativeRuntimeSourcePins", privateStatic),
                Is.Null);
            Assert.That(
                dispatcher.GetMethod(
                    "VerifyNativeRuntimeSourcePins",
                    privateStatic),
                Is.Null);
            Assert.That(
                dispatcher.GetNestedType(
                    "NativeSourcePin",
                    BindingFlags.NonPublic),
                Is.Null);

            MethodInfo? receiptVerifier = dispatcher.GetMethod(
                "VerifyNativeAuthorizationReceipt",
                privateStatic);
            Assert.That(receiptVerifier, Is.Not.Null);
            foreach (ParameterInfo parameter in receiptVerifier!.GetParameters())
            {
                Assert.That(parameter.Name, Is.Not.EqualTo("dispatcherSha256"));
            }

            InvalidOperationException? exception = Assert.Throws<
                InvalidOperationException>(() =>
                    WP0002GateDispatcher.Dispatch(
                        WP0002GateDispatcher.AssetRefreshGate,
                        new string('0', 64)));
            Assert.That(
                exception!.Message,
                Is.EqualTo("WP0002_DISPATCHER_HASH_MISMATCH"));
        }
    }
}
