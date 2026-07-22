#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;
using Unity.Pipeline.Editor.Commands;
using UnityEditor;
using UnityEditor.MPE;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Editor
{
    /// <summary>
    /// Immediately stops any listener the package briefly auto-starts during a
    /// cold import, then defers the filtered restart until assets are loadable.
    /// </summary>
    [InitializeOnLoad]
    internal static class WP0002PipelineBootstrap
    {
        private const int ExpectedCommandCount = 7;
        private const string CanonicalProjectRoot =
            "/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/" +
            "sasha-the-land-pirate/Game";
        private const string SettingsAssetPath =
            "Assets/Settings/Pipeline/EditorPipelineManager.asset";
        private const string SettingsAssetGuid =
            "a615acb88f7e4559a2831dad2ac14921";
        private static readonly bool s_ProcessEligible;

        static WP0002PipelineBootstrap()
        {
            s_ProcessEligible =
                !Application.isBatchMode &&
                ProcessService.level == ProcessLevel.Main;
            if (!s_ProcessEligible)
            {
                return;
            }

            // Cold import may initialize the package before its settings asset
            // is loadable. Force its owner now and close any unfiltered server;
            // no project assets are read by this early guard.
            RuntimeHelpers.RunClassConstructor(
                typeof(PipelineServerStartup).TypeHandle);
            if (PipelineServerStartup.Server != null)
            {
                PipelineServerStartup.StopServer();
                AutoTickCommand.SetAutoTick(false);
            }
        }

        internal static void ConfigureAndStartAfterDomainReload()
        {
            if (!s_ProcessEligible)
            {
                return;
            }

            try
            {
                if (AssetDatabase.IsAssetImportWorkerProcess())
                {
                    return;
                }

                FailIfServerRunningBeforeAssetValidation();
                ValidateCanonicalProjectRoot();
                EditorPipelineManager settings = LoadExactSettingsAsset();
                ValidateSettings(settings);

                var discovery = new WP0002CliCommandDiscovery();
                CommandRegistry.SetDiscovery(discovery);
                ValidateRegisteredCommands(CommandRegistry.DiscoverCommands());

                PipelineServerStartup.EnsureServerStarted();
                if (PipelineServerStartup.Server == null ||
                    !PipelineServerStartup.Server.IsRunning)
                {
                    throw new InvalidOperationException(
                        "Filtered WP-0002 Pipeline server did not start.");
                }
            }
            catch (Exception exception)
            {
                PipelineServerStartup.StopServer();
                throw new InvalidOperationException(
                    "WP-0002 Pipeline bootstrap failed closed.",
                    exception);
            }
        }

        private static void ValidateCanonicalProjectRoot()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            if (!string.Equals(
                    projectRoot,
                    CanonicalProjectRoot,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unity Pipeline is disabled outside the canonical project.");
            }
        }

        private static void FailIfServerRunningBeforeAssetValidation()
        {
            if (PipelineServerStartup.Server == null)
            {
                return;
            }

            PipelineServerStartup.StopServer();
            AutoTickCommand.SetAutoTick(false);
            throw new InvalidOperationException(
                "Pipeline server existed before WP-0002 asset validation.");
        }

        private static EditorPipelineManager LoadExactSettingsAsset()
        {
            string[] settingsGuids =
                AssetDatabase.FindAssets("t:EditorPipelineManager");
            if (settingsGuids.Length != 1 ||
                !string.Equals(
                    settingsGuids[0],
                    SettingsAssetGuid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.GUIDToAssetPath(settingsGuids[0]),
                    SettingsAssetPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The exact WP-0002 Pipeline settings asset is missing.");
            }

            return AssetDatabase.LoadAssetAtPath<EditorPipelineManager>(
                    SettingsAssetPath)
                ?? throw new InvalidOperationException(
                    "The pinned WP-0002 Pipeline settings asset did not load.");
        }

        private static void ValidateSettings(EditorPipelineManager settings)
        {
            if (settings.AutoStart ||
                settings.Port != 0 ||
                settings.WatchdogEnabled ||
                settings.WatchdogIntervalSeconds != 5 ||
                settings.LogRequestsResponses)
            {
                throw new InvalidOperationException(
                    "WP-0002 Pipeline settings differ from the pinned profile.");
            }
        }

        private static void ValidateRegisteredCommands(
            IEnumerable<CommandInfo> commands)
        {
            CommandInfo[] registered = commands
                .OrderBy(command => command.Name, StringComparer.Ordinal)
                .ToArray();
            string[] expected = WP0002CliCommandDiscovery.ExpectedCommandNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            if (registered.Length != ExpectedCommandCount ||
                !registered.Select(command => command.Name)
                    .SequenceEqual(expected, StringComparer.Ordinal) ||
                registered.Any(command =>
                    command.Method.DeclaringType != typeof(WP0002CliGateCommands) ||
                    !command.MainThreadRequired ||
                    command.RuntimeOnly ||
                    command.Parameters.Count != 1 ||
                    !string.Equals(
                        command.Parameters[0].Name,
                        "expected_source_sha256",
                        StringComparison.Ordinal) ||
                    !command.Parameters[0].Required ||
                    command.Parameters[0].ParameterType != typeof(string)))
            {
                throw new InvalidOperationException(
                    "WP-0002 Pipeline registry is not the exact pinned surface.");
            }
        }
    }

    /// <summary>
    /// Defers all project-asset reads until Unity reports that the domain reload
    /// and its cold-import asset pass have completed.
    /// </summary>
    internal sealed class WP0002PipelineAssetPostprocessor : AssetPostprocessor
    {
        private static bool s_Attempted;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (!didDomainReload || s_Attempted)
            {
                return;
            }

            s_Attempted = true;
            WP0002PipelineBootstrap.ConfigureAndStartAfterDomainReload();
        }
    }

    /// <summary>
    /// Discovery filter that returns only the seven fixed WP-0002 wrappers.
    /// Requests for every other attribute type intentionally return no methods.
    /// </summary>
    internal sealed class WP0002CliCommandDiscovery : ICommandDiscovery
    {
        private static readonly (string MethodName, string CommandName)[] Expected =
        {
            (nameof(WP0002CliGateCommands.AssetRefresh),
                "wp0002_asset_refresh"),
            (nameof(WP0002CliGateCommands.EditModeTests),
                "wp0002_editmode_tests"),
            (nameof(WP0002CliGateCommands.PlayModeTests),
                "wp0002_playmode_tests"),
            (nameof(WP0002CliGateCommands.TechnicalCapture),
                "wp0002_technical_capture"),
            (nameof(WP0002CliGateCommands.NativeBuild),
                "wp0002_native_build"),
            (nameof(WP0002CliGateCommands.NativePerformanceStart),
                "wp0002_native_performance_start"),
            (nameof(WP0002CliGateCommands.NativePerformanceCollect),
                "wp0002_native_performance_collect")
        };

        private readonly IReadOnlyList<MethodInfo> m_Methods;

        internal static IReadOnlyList<string> ExpectedCommandNames { get; } =
            Array.AsReadOnly(Expected.Select(entry => entry.CommandName).ToArray());

        internal WP0002CliCommandDiscovery()
        {
            m_Methods = Array.AsReadOnly(BuildAndValidateMethods());
        }

        public IEnumerable<MethodInfo> GetMethodsWithAttribute<T>()
            where T : Attribute
        {
            return typeof(T) == typeof(CliCommandAttribute)
                ? m_Methods
                : Array.Empty<MethodInfo>();
        }

        private static MethodInfo[] BuildAndValidateMethods()
        {
            MethodInfo[] allAttributed = typeof(WP0002CliGateCommands)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.GetCustomAttribute<CliCommandAttribute>() != null)
                .ToArray();
            if (allAttributed.Length != Expected.Length)
            {
                throw new InvalidOperationException(
                    "Unexpected WP-0002 CLI command count.");
            }

            var methods = new MethodInfo[Expected.Length];
            for (int index = 0; index < Expected.Length; index++)
            {
                (string methodName, string commandName) = Expected[index];
                MethodInfo method = typeof(WP0002CliGateCommands).GetMethod(
                        methodName,
                        BindingFlags.Public |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly,
                        binder: null,
                        types: new[] { typeof(string) },
                        modifiers: null)
                    ?? throw new InvalidOperationException(
                        "Missing WP-0002 CLI method " + methodName + ".");
                CliCommandAttribute attribute = method
                    .GetCustomAttributes<CliCommandAttribute>(inherit: false)
                    .Single();
                ParameterInfo[] parameters = method.GetParameters();
                CliArgAttribute argument = parameters.Length == 1
                    ? parameters[0]
                        .GetCustomAttributes<CliArgAttribute>(inherit: false)
                        .Single()
                    : throw new InvalidOperationException(
                        "Unexpected argument count for " + methodName + ".");

                if (!string.Equals(
                        attribute.Name,
                        commandName,
                        StringComparison.Ordinal) ||
                    !attribute.MainThreadRequired ||
                    attribute.RuntimeOnly ||
                    method.ReturnType != typeof(string) ||
                    !string.Equals(
                        argument.Name,
                        "expected_source_sha256",
                        StringComparison.Ordinal) ||
                    !argument.Required ||
                    parameters[0].ParameterType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Unexpected WP-0002 CLI shape for " + methodName + ".");
                }

                methods[index] = method;
            }

            if (!allAttributed.ToHashSet().SetEquals(methods))
            {
                throw new InvalidOperationException(
                    "WP-0002 CLI discovery contains an unexpected method.");
            }

            return methods;
        }
    }
}
