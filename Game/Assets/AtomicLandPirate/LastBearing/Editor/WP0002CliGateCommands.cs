#nullable enable

using Unity.Pipeline.Commands;

namespace AtomicLandPirate.Presentation.LastBearing.Editor
{
    /// <summary>
    /// Fixed Unity Pipeline command names for the existing content-addressed
    /// WP-0002 gates. The wrapper adds no gate selection or filesystem input.
    /// </summary>
    public static class WP0002CliGateCommands
    {
        [CliCommand(
            "wp0002_asset_refresh",
            "Run the fixed WP-0002 asset refresh and compile gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string AssetRefresh(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.AssetRefreshGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_editmode_tests",
            "Run the fixed WP-0002 EditMode test gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string EditModeTests(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.EditModeGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_playmode_tests",
            "Run the fixed WP-0002 PlayMode test gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string PlayModeTests(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.PlayModeGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_technical_capture",
            "Run the fixed WP-0002 technical capture gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string TechnicalCapture(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.TechnicalCaptureGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_native_build",
            "Run the fixed WP-0002 native IL2CPP ARM64 build gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string NativeBuild(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.NativeBuildGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_native_performance_start",
            "Run the fixed WP-0002 native performance start gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string NativePerformanceStart(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.NativePerformanceStartGate,
                expectedSourceSha256);
        }

        [CliCommand(
            "wp0002_native_performance_collect",
            "Run the fixed WP-0002 native performance collect gate.",
            MainThreadRequired = true,
            RuntimeOnly = false)]
        public static string NativePerformanceCollect(
            [CliArg(
                "expected_source_sha256",
                "Expected SHA-256 of the WP-0002 dispatcher source.",
                Required = true)]
            string expectedSourceSha256)
        {
            return WP0002GateDispatcher.Dispatch(
                WP0002GateDispatcher.NativePerformanceCollectGate,
                expectedSourceSha256);
        }
    }
}
