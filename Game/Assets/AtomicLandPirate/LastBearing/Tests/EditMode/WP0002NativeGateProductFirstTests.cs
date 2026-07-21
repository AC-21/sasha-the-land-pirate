#nullable enable

using System;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void NativeBuildSchedulerUsesFocusIndependentOneShotEditorUpdate()
        {
            string dispatcherPath = Path.Combine(
                Application.dataPath,
                "AtomicLandPirate",
                "LastBearing",
                "Editor",
                "WP0002GateDispatcher.cs");
            string source = File.ReadAllText(dispatcherPath);
            string runNativeBuild = Segment(
                source,
                "private static string RunNativeBuild(",
                "private static void ExecuteScheduledNativeBuild()");
            string scheduledCallback = Segment(
                source,
                "private static void ExecuteScheduledNativeBuild()",
                "private static string ExecuteNativeBuild(");
            string invalidation = Segment(
                source,
                "private static void InvalidateTrustedNativeState()",
                "private static void InvalidateTrustedNativeRun()");

            Assert.That(
                runNativeBuild,
                Does.Contain(
                    "EditorApplication.update += ExecuteScheduledNativeBuild"));
            Assert.That(
                runNativeBuild,
                Does.Not.Contain(
                    "EditorApplication.delayCall += ExecuteScheduledNativeBuild"));
            Assert.That(
                scheduledCallback,
                Does.Contain(
                    "EditorApplication.update -= ExecuteScheduledNativeBuild"));
            Assert.That(
                invalidation,
                Does.Contain(
                    "EditorApplication.update -= ExecuteScheduledNativeBuild"));
        }

        private static string Segment(
            string source,
            string startToken,
            string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }
    }
}
