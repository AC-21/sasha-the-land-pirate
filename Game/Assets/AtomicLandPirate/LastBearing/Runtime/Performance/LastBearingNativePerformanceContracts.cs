#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Performance
{
    [Serializable]
    public sealed class LastBearingNativePerformanceRequest
    {
        public const int CurrentSchemaVersion = 1;
        public const string ContractId =
            "WP0002_NATIVE_PERFORMANCE_REQUEST_V1";

        public int schema_version;
        public string contract_id = string.Empty;
        public string request_nonce = string.Empty;
        public string expected_source_commit = string.Empty;
        public string expected_source_tree_sha256 = string.Empty;
        public string expected_build_identity_sha256 = string.Empty;
        public string expected_build_guid = string.Empty;
        public string expected_executable_sha256 = string.Empty;
    }

    [Serializable]
    public sealed class LastBearingNativePerformanceBuildIdentity
    {
        public const int CurrentSchemaVersion = 1;
        public const string IdentityId =
            "WP0002_NATIVE_PERFORMANCE_BUILD_IDENTITY_V1";

        public int schema_version;
        public string identity_id = string.Empty;
        public string source_commit = string.Empty;
        public string source_tree_sha256 = string.Empty;
        public string build_guid = string.Empty;
        public string unity_version = string.Empty;
        public string executable_sha256 = string.Empty;
        public bool development_build;
    }

    public static class LastBearingNativePerformanceEnvironment
    {
        public const string RequiredUnityVersion = "6000.5.4f1";
        public const int RequiredWidth = 2560;
        public const int RequiredHeight = 1600;

        public static bool IsExactResolution(int width, int height)
        {
            return width == RequiredWidth && height == RequiredHeight;
        }
    }

    public sealed class LastBearingNativePerformanceLaunch
    {
        public const string ActivationArgument =
            "--wp0002-native-performance";
        public const string PlayerBundleName =
            "SashaAtomicLandPirateVGR13.app";
        public const string RequestFileName =
            "wp0002-native-performance-request.json";
        public const string BuildIdentityFileName =
            "wp0002-native-performance-build-identity.json";

        private LastBearingNativePerformanceLaunch(
            LastBearingNativePerformanceRequest request,
            LastBearingNativePerformanceBuildIdentity buildIdentity,
            string requestPath,
            string requestSha256,
            string buildIdentityPath,
            string buildIdentitySha256,
            string reportPath)
        {
            Request = request;
            BuildIdentity = buildIdentity;
            RequestPath = requestPath;
            RequestSha256 = requestSha256;
            BuildIdentityPath = buildIdentityPath;
            BuildIdentitySha256 = buildIdentitySha256;
            ReportPath = reportPath;
        }

        public LastBearingNativePerformanceRequest Request { get; }

        public LastBearingNativePerformanceBuildIdentity BuildIdentity { get; }

        public string RequestPath { get; }

        public string RequestSha256 { get; }

        public string BuildIdentityPath { get; }

        public string BuildIdentitySha256 { get; }

        public string ReportPath { get; }

        public static bool TryDeriveRunDirectoryFromMacPlayerDataPath(
            string applicationDataPath,
            out string runDirectory,
            out string error)
        {
            runDirectory = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(applicationDataPath) ||
                !Path.IsPathRooted(applicationDataPath))
            {
                error = "macOS player data path must be absolute";
                return false;
            }

            try
            {
                string canonicalDataPath = Path.GetFullPath(applicationDataPath)
                    .TrimEnd(Path.DirectorySeparatorChar);
                if (!string.Equals(
                        Path.GetFileName(canonicalDataPath),
                        "Contents",
                        StringComparison.Ordinal))
                {
                    error = "macOS player data path must end in .app/Contents";
                    return false;
                }

                DirectoryInfo? bundle = Directory.GetParent(canonicalDataPath);
                DirectoryInfo? parent = bundle?.Parent;
                if (bundle == null ||
                    parent == null ||
                    !string.Equals(
                        bundle.Name,
                        PlayerBundleName,
                        StringComparison.Ordinal))
                {
                    error = "macOS player bundle parent is invalid";
                    return false;
                }

                runDirectory = Path.GetFullPath(parent.FullName)
                    .TrimEnd(Path.DirectorySeparatorChar);
                if (!IsExactNativeRunDirectory(runDirectory))
                {
                    runDirectory = string.Empty;
                    error =
                        "player is outside BuildArtifacts/WP-0002/local-only/native-il2cpp-arm64";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                error = "macOS player run-directory derivation failed: " +
                        exception.GetType().Name;
                return false;
            }
        }

        public static bool HasActivationArgument(string[] arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        ActivationArgument,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryLoad(
            string[] arguments,
            bool compiledWithIl2Cpp,
            Architecture processArchitecture,
            string applicationBuildGuid,
            string unityVersion,
            bool isDevelopmentBuild,
            string allowedRunDirectory,
            out LastBearingNativePerformanceLaunch? launch,
            out string error)
        {
            launch = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(allowedRunDirectory))
            {
                error = "derived native-performance run directory is required";
                return false;
            }

            try
            {
                allowedRunDirectory = Path.GetFullPath(allowedRunDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                error = "derived run directory is invalid: " +
                        exception.GetType().Name;
                return false;
            }

            if (!IsExactNativeRunDirectory(allowedRunDirectory))
            {
                error = "derived run directory does not match the fixed layout";
                return false;
            }

            if (!HasExactlyOneActivationArgument(arguments, out error))
            {
                return false;
            }

            string requestPath = Path.Combine(
                allowedRunDirectory,
                RequestFileName);
            string identityPath = Path.Combine(
                allowedRunDirectory,
                BuildIdentityFileName);

            if (!TryValidateBoundedPaths(
                    requestPath,
                    identityPath,
                    allowedRunDirectory,
                    out requestPath,
                    out identityPath,
                    out error))
            {
                return false;
            }

            try
            {
                if ((File.GetAttributes(requestPath) &
                     FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(identityPath) &
                     FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(allowedRunDirectory) &
                     FileAttributes.ReparsePoint) != 0)
                {
                    error = "request and build identity must not be symbolic links";
                    return false;
                }

                byte[] requestBytes = File.ReadAllBytes(requestPath);
                byte[] identityBytes = File.ReadAllBytes(identityPath);
                string requestSha256 = ComputeSha256(requestBytes);
                string identitySha256 = ComputeSha256(identityBytes);
                string requestJson = Encoding.UTF8.GetString(requestBytes);
                string identityJson = Encoding.UTF8.GetString(identityBytes);
                LastBearingNativePerformanceRequest? request =
                    JsonUtility.FromJson<LastBearingNativePerformanceRequest>(
                        requestJson);
                LastBearingNativePerformanceBuildIdentity? identity =
                    JsonUtility.FromJson<LastBearingNativePerformanceBuildIdentity>(
                        identityJson);
                if (request == null || identity == null)
                {
                    error = "request or build identity JSON is invalid";
                    return false;
                }

                if (!Validate(
                        request,
                        identity,
                        identitySha256,
                        compiledWithIl2Cpp,
                        processArchitecture,
                        applicationBuildGuid,
                        unityVersion,
                        isDevelopmentBuild,
                        out error))
                {
                    return false;
                }

                string expectedRunName =
                    identity.source_tree_sha256.Substring(0, 12) + "-" +
                    identity.build_guid;
                if (!string.Equals(
                        Path.GetFileName(allowedRunDirectory),
                        expectedRunName,
                        StringComparison.Ordinal))
                {
                    error =
                        "source-bound run directory does not match build identity";
                    return false;
                }

                string? directory = Path.GetDirectoryName(requestPath);
                if (string.IsNullOrEmpty(directory))
                {
                    error = "request path has no report directory";
                    return false;
                }

                string reportPath = Path.Combine(
                    directory,
                    "wp0002-native-performance-" +
                    request.request_nonce +
                    ".report.json");
                if (File.Exists(reportPath) || File.Exists(reportPath + ".tmp"))
                {
                    error = "nonce-bound report path already exists";
                    return false;
                }

                launch = new LastBearingNativePerformanceLaunch(
                    request,
                    identity,
                    Path.GetFullPath(requestPath),
                    requestSha256,
                    Path.GetFullPath(identityPath),
                    identitySha256,
                    reportPath);
                return true;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is ArgumentException ||
                      exception is CryptographicException)
            {
                error = "request load failed: " + exception.GetType().Name;
                return false;
            }
        }

        public static bool TryValidateBoundedPaths(
            string requestPath,
            string buildIdentityPath,
            string allowedRunDirectory,
            out string canonicalRequestPath,
            out string canonicalBuildIdentityPath,
            out string error)
        {
            canonicalRequestPath = string.Empty;
            canonicalBuildIdentityPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(requestPath) ||
                string.IsNullOrWhiteSpace(buildIdentityPath) ||
                string.IsNullOrWhiteSpace(allowedRunDirectory) ||
                !Path.IsPathRooted(requestPath) ||
                !Path.IsPathRooted(buildIdentityPath) ||
                !Path.IsPathRooted(allowedRunDirectory))
            {
                error = "request, identity, and run-directory paths must be absolute";
                return false;
            }

            try
            {
                string runDirectory = Path.GetFullPath(allowedRunDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar);
                canonicalRequestPath = Path.GetFullPath(requestPath);
                canonicalBuildIdentityPath = Path.GetFullPath(buildIdentityPath);
                if (!string.Equals(
                        Path.GetDirectoryName(canonicalRequestPath),
                        runDirectory,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        Path.GetDirectoryName(canonicalBuildIdentityPath),
                        runDirectory,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        Path.GetFileName(canonicalRequestPath),
                        RequestFileName,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        Path.GetFileName(canonicalBuildIdentityPath),
                        BuildIdentityFileName,
                        StringComparison.Ordinal))
                {
                    error =
                        "request and identity must use fixed names in the bounded run directory";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                error = "run path validation failed: " +
                        exception.GetType().Name;
                return false;
            }
        }

        public static bool Validate(
            LastBearingNativePerformanceRequest request,
            LastBearingNativePerformanceBuildIdentity identity,
            string actualBuildIdentitySha256,
            bool compiledWithIl2Cpp,
            Architecture processArchitecture,
            string applicationBuildGuid,
            string unityVersion,
            bool isDevelopmentBuild,
            out string error)
        {
            error = string.Empty;
            if (request.schema_version !=
                    LastBearingNativePerformanceRequest.CurrentSchemaVersion ||
                !string.Equals(
                    request.contract_id,
                    LastBearingNativePerformanceRequest.ContractId,
                    StringComparison.Ordinal))
            {
                error = "unsupported request contract";
                return false;
            }

            if (!IsLowerHex(request.request_nonce, 32))
            {
                error = "request nonce is not canonical";
                return false;
            }

            if (identity.schema_version !=
                    LastBearingNativePerformanceBuildIdentity.CurrentSchemaVersion ||
                !string.Equals(
                    identity.identity_id,
                    LastBearingNativePerformanceBuildIdentity.IdentityId,
                    StringComparison.Ordinal))
            {
                error = "unsupported build identity contract";
                return false;
            }

            if (!IsLowerHex(request.expected_source_commit, 40) ||
                !IsLowerHex(request.expected_source_tree_sha256, 64) ||
                !IsLowerHex(request.expected_build_identity_sha256, 64) ||
                !IsLowerHex(request.expected_executable_sha256, 64) ||
                !IsLowerHex(identity.source_commit, 40) ||
                !IsLowerHex(identity.source_tree_sha256, 64) ||
                !IsLowerHex(identity.executable_sha256, 64) ||
                !IsLowerHex(request.expected_build_guid, 32) ||
                !IsLowerHex(identity.build_guid, 32) ||
                !IsLowerHex(applicationBuildGuid, 32) ||
                !IsLowerHex(actualBuildIdentitySha256, 64))
            {
                error = "source or artifact identity is not canonical lowercase hex";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.expected_build_guid) ||
                string.IsNullOrWhiteSpace(identity.build_guid) ||
                string.IsNullOrWhiteSpace(identity.unity_version) ||
                string.IsNullOrWhiteSpace(applicationBuildGuid) ||
                string.IsNullOrWhiteSpace(unityVersion))
            {
                error = "build GUID and Unity version must be explicit";
                return false;
            }

            if (!string.Equals(
                    request.expected_build_identity_sha256,
                    actualBuildIdentitySha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.expected_source_commit,
                    identity.source_commit,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.expected_source_tree_sha256,
                    identity.source_tree_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.expected_executable_sha256,
                    identity.executable_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.expected_build_guid,
                    identity.build_guid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    applicationBuildGuid,
                    identity.build_guid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    unityVersion,
                    identity.unity_version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    unityVersion,
                    LastBearingNativePerformanceEnvironment.RequiredUnityVersion,
                    StringComparison.Ordinal))
            {
                error = "request does not match the running source-bound build identity";
                return false;
            }

            if (!compiledWithIl2Cpp)
            {
                error = "running player was not compiled with ENABLE_IL2CPP";
                return false;
            }

            if (processArchitecture != Architecture.Arm64)
            {
                error = "running player process is not Arm64";
                return false;
            }

            if (!identity.development_build || !isDevelopmentBuild)
            {
                error = "running player is not a source-bound development build";
                return false;
            }

            return true;
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                for (var index = 0; index < digest.Length; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static bool HasExactlyOneActivationArgument(
            string[] arguments,
            out string error)
        {
            error = string.Empty;
            var matches = 0;
            if (arguments != null)
            {
                for (var index = 0; index < arguments.Length; index++)
                {
                    string argument = arguments[index];
                    if (argument.StartsWith(
                            ActivationArgument,
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            argument,
                            ActivationArgument,
                            StringComparison.Ordinal))
                    {
                        error =
                            "path-valued or unknown native-performance arguments are forbidden";
                        return false;
                    }

                    if (!string.Equals(
                            argument,
                            ActivationArgument,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matches++;
                }
            }

            if (matches != 1)
            {
                error =
                    "exactly one fixed native-performance flag is required";
                return false;
            }

            return true;
        }

        private static bool IsExactNativeRunDirectory(string runDirectory)
        {
            DirectoryInfo run = new DirectoryInfo(runDirectory);
            DirectoryInfo? runs = run.Parent;
            DirectoryInfo? native = runs?.Parent;
            DirectoryInfo? localOnly = native?.Parent;
            DirectoryInfo? packet = localOnly?.Parent;
            DirectoryInfo? artifacts = packet?.Parent;
            if (runs == null ||
                native == null ||
                localOnly == null ||
                packet == null ||
                artifacts == null ||
                !string.Equals(runs.Name, "runs", StringComparison.Ordinal) ||
                !string.Equals(
                    native.Name,
                    "native-il2cpp-arm64",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    localOnly.Name,
                    "local-only",
                    StringComparison.Ordinal) ||
                !string.Equals(packet.Name, "WP-0002", StringComparison.Ordinal) ||
                !string.Equals(
                    artifacts.Name,
                    "BuildArtifacts",
                    StringComparison.Ordinal))
            {
                return false;
            }

            string name = run.Name;
            return name.Length == 45 &&
                   name[12] == '-' &&
                   IsLowerHex(name.Substring(0, 12), 12) &&
                   IsLowerHex(name.Substring(13), 32);
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }

    }
}
