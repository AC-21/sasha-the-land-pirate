#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AtomicLandPirate.Save.LastBearing
{
    internal enum LastBearingOperationStatus
    {
        Success = 0,
        Missing = 1,
        Exists = 2,
        Busy = 3,
        InvalidData = 4,
        ConfinementFailure = 5,
        IoFailure = 6,
    }

    internal readonly struct LastBearingEntryName : IEquatable<LastBearingEntryName>
    {
        private LastBearingEntryName(string value)
        {
            Value = value;
        }

        internal string Value { get; }

        internal static LastBearingEntryName CurrentPointer { get; } =
            new LastBearingEntryName(LastBearingProfileContract.CurrentPointerName);

        internal static LastBearingEntryName LastGoodPointer { get; } =
            new LastBearingEntryName(LastBearingProfileContract.LastGoodPointerName);

        internal static LastBearingEntryName WriterLock { get; } =
            new LastBearingEntryName(LastBearingProfileContract.WriterLockName);

        internal static LastBearingEntryName ForGeneration(
            ulong generation,
            string lowerDigest)
        {
            RequireGenerationAndDigest(generation, lowerDigest);
            return new LastBearingEntryName(
                "gen-" +
                generation.ToString("D20", CultureInfo.InvariantCulture) +
                "-" + lowerDigest + ".lbg");
        }

        internal static LastBearingEntryName ForGenerationStage(
            ulong generation,
            string lowerDigest)
        {
            RequireGenerationAndDigest(generation, lowerDigest);
            return new LastBearingEntryName(
                ".stage-gen-" +
                generation.ToString("D20", CultureInfo.InvariantCulture) +
                "-" + lowerDigest + ".tmp");
        }

        internal static LastBearingEntryName ForPointerStage(
            bool current,
            byte[] pointerBytes)
        {
            if (pointerBytes is null)
            {
                throw new ArgumentNullException(nameof(pointerBytes));
            }

            string digest = LastBearingGenerationCodec.ToLowerHex(
                LastBearingGenerationCodec.ComputeSha256(pointerBytes));
            return new LastBearingEntryName(
                current
                    ? ".stage-current-" + digest + ".tmp"
                    : ".stage-last-good-" + digest + ".tmp");
        }

        internal bool IsScratchStage
        {
            get
            {
                if (Value is null || !Value.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    return false;
                }

                const string generationPrefix = ".stage-gen-";
                if (Value.StartsWith(generationPrefix, StringComparison.Ordinal))
                {
                    const int generationDigits = 20;
                    const int digestDigits = 64;
                    int separator = generationPrefix.Length + generationDigits;
                    if (Value.Length != separator + 1 + digestDigits + 4 ||
                        Value[separator] != '-')
                    {
                        return false;
                    }

                    string generationText = Value.Substring(
                        generationPrefix.Length,
                        generationDigits);
                    if (!ulong.TryParse(
                            generationText,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out ulong generation) ||
                        generation == 0 ||
                        !string.Equals(
                            generation.ToString("D20", CultureInfo.InvariantCulture),
                            generationText,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return IsLowerDigest(
                        Value.Substring(separator + 1, digestDigits));
                }

                return IsCanonicalPointerStage(".stage-current-") ||
                    IsCanonicalPointerStage(".stage-last-good-");
            }
        }

        internal static bool TryParseGeneration(
            string value,
            out LastBearingEntryName entryName,
            out ulong generation,
            out string lowerDigest)
        {
            entryName = default;
            generation = 0;
            lowerDigest = string.Empty;
            if (value is null ||
                value.Length != LastBearingProfileContract.GenerationFileNameBytes ||
                !value.StartsWith("gen-", StringComparison.Ordinal) ||
                value[24] != '-' ||
                !value.EndsWith(".lbg", StringComparison.Ordinal))
            {
                return false;
            }

            string generationText = value.Substring(4, 20);
            if (generationText[0] == '0' && generationText !=
                "00000000000000000000" &&
                generationText.TrimStart('0').Length == 20)
            {
                return false;
            }

            if (!ulong.TryParse(
                    generationText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out generation) ||
                generation == 0 ||
                !string.Equals(
                    generation.ToString("D20", CultureInfo.InvariantCulture),
                    generationText,
                    StringComparison.Ordinal))
            {
                return false;
            }

            lowerDigest = value.Substring(25, 64);
            if (!IsLowerDigest(lowerDigest))
            {
                generation = 0;
                lowerDigest = string.Empty;
                return false;
            }

            entryName = new LastBearingEntryName(value);
            return true;
        }

        public bool Equals(LastBearingEntryName other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is LastBearingEntryName other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        private static void RequireGenerationAndDigest(
            ulong generation,
            string lowerDigest)
        {
            if (generation == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            if (!IsLowerDigest(lowerDigest))
            {
                throw new ArgumentException(
                    "Digest must be 64 lowercase hexadecimal characters.",
                    nameof(lowerDigest));
            }
        }

        private static bool IsLowerDigest(string value)
        {
            if (value is null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCanonicalPointerStage(string prefix)
        {
            const int digestDigits = 64;
            return Value.StartsWith(prefix, StringComparison.Ordinal) &&
                Value.Length == prefix.Length + digestDigits + 4 &&
                IsLowerDigest(Value.Substring(prefix.Length, digestDigits));
        }
    }

    internal readonly struct LastBearingFileResult
    {
        internal LastBearingFileResult(LastBearingOperationStatus status)
        {
            Status = status;
        }

        internal LastBearingOperationStatus Status { get; }
    }

    internal readonly struct LastBearingConfinementResult
    {
        internal LastBearingConfinementResult(LastBearingOperationStatus status)
        {
            Status = status;
        }

        internal LastBearingOperationStatus Status { get; }
    }

    internal readonly struct LastBearingDirectoryOpenResult
    {
        internal LastBearingDirectoryOpenResult(
            LastBearingOperationStatus status,
            ILastBearingProfileDirectory? directory)
        {
            Status = status;
            Directory = directory;
        }

        internal LastBearingOperationStatus Status { get; }

        internal ILastBearingProfileDirectory? Directory { get; }
    }

    internal readonly struct LastBearingLockResult
    {
        internal LastBearingLockResult(
            LastBearingOperationStatus status,
            IDisposable? lease)
        {
            Status = status;
            Lease = lease;
        }

        internal LastBearingOperationStatus Status { get; }

        internal IDisposable? Lease { get; }
    }

    internal readonly struct LastBearingReadResult
    {
        private readonly byte[]? _bytes;

        internal LastBearingReadResult(
            LastBearingOperationStatus status,
            byte[]? bytes)
        {
            Status = status;
            _bytes = bytes is null ? null : (byte[])bytes.Clone();
        }

        internal LastBearingOperationStatus Status { get; }

        internal byte[]? Bytes =>
            _bytes is null ? null : (byte[])_bytes.Clone();
    }

    internal readonly struct LastBearingWriteResult
    {
        internal LastBearingWriteResult(LastBearingOperationStatus status)
        {
            Status = status;
        }

        internal LastBearingOperationStatus Status { get; }
    }

    internal readonly struct LastBearingMoveResult
    {
        internal LastBearingMoveResult(LastBearingOperationStatus status)
        {
            Status = status;
        }

        internal LastBearingOperationStatus Status { get; }
    }

    internal interface ILastBearingFileOperations
    {
        LastBearingDirectoryOpenResult TryOpenProfileDirectory(
            string exactCanonicalPath,
            bool createFixedTerminalIfMissing);
    }

    internal interface ILastBearingProfileDirectory : IDisposable
    {
        LastBearingConfinementResult RevalidateBinding();

        LastBearingLockResult TryAcquireExclusiveLock(
            LastBearingEntryName name);

        LastBearingReadResult TryReadExact(
            LastBearingEntryName name,
            int maximumBytes);

        LastBearingWriteResult TryWriteNewDurable(
            LastBearingEntryName name,
            byte[] exactBytes);

        LastBearingFileResult TryRemoveScratchStage(
            LastBearingEntryName name);

        LastBearingMoveResult TryMoveNoReplace(
            LastBearingEntryName source,
            LastBearingEntryName destination);

        LastBearingMoveResult TryAtomicReplace(
            LastBearingEntryName source,
            LastBearingEntryName destination);

        LastBearingFileResult TryFlushDirectory();
    }

    internal sealed class LastBearingFileOperations : ILastBearingFileOperations
    {
        internal static LastBearingFileOperations Instance { get; } =
            new LastBearingFileOperations();

        private LastBearingFileOperations()
        {
        }

        public LastBearingDirectoryOpenResult TryOpenProfileDirectory(
            string exactCanonicalPath,
            bool createFixedTerminalIfMissing)
        {
            if (!UnixNative.IsSupported)
            {
                return OpenFailure(
                    LastBearingOperationStatus.ConfinementFailure);
            }

            string[] components = exactCanonicalPath.Substring(1).Split('/');
            if (components.Length == 0 ||
                Array.Exists(components, component => component.Length == 0) ||
                !string.Equals(
                    components[components.Length - 1],
                    LastBearingProfileContract.ProfileName,
                    StringComparison.Ordinal))
            {
                return OpenFailure(
                    LastBearingOperationStatus.ConfinementFailure);
            }

            int parentDescriptor = UnixNative.OpenDirectoryRoot();
            if (parentDescriptor < 0)
            {
                return OpenFailure(LastBearingOperationStatus.IoFailure);
            }

            try
            {
                for (int index = 0; index < components.Length - 1; index++)
                {
                    int next = UnixNative.OpenDirectoryAt(
                        parentDescriptor,
                        components[index]);
                    if (next < 0)
                    {
                        return OpenFailure(UnixNative.OpenFailureStatus());
                    }

                    UnixNative.Close(parentDescriptor);
                    parentDescriptor = next;
                }

                string terminal = components[components.Length - 1];
                int profileDescriptor = UnixNative.OpenDirectoryAt(
                    parentDescriptor,
                    terminal);
                if (profileDescriptor < 0 &&
                    UnixNative.LastError == UnixNative.ErrorNoEntry &&
                    createFixedTerminalIfMissing)
                {
                    int createResult = UnixNative.MakeDirectoryAt(
                        parentDescriptor,
                        terminal,
                        448);
                    if (createResult != 0 &&
                        UnixNative.LastError != UnixNative.ErrorExists)
                    {
                        return OpenFailure(
                            UnixNative.OpenFailureStatus());
                    }

                    profileDescriptor = UnixNative.OpenDirectoryAt(
                        parentDescriptor,
                        terminal);
                }

                if (profileDescriptor < 0)
                {
                    LastBearingOperationStatus status =
                        UnixNative.LastError == UnixNative.ErrorNoEntry
                            ? LastBearingOperationStatus.Missing
                            : UnixNative.OpenFailureStatus();
                    return OpenFailure(status);
                }

                if (!UnixNative.TryGetIdentity(
                        parentDescriptor,
                        out UnixFileIdentity parentIdentity) ||
                    !parentIdentity.IsDirectory ||
                    !UnixNative.TryGetIdentity(
                        profileDescriptor,
                        out UnixFileIdentity profileIdentity) ||
                    !profileIdentity.IsDirectory ||
                    !UnixNative.TryGetHandlePath(
                        profileDescriptor,
                        exactCanonicalPath,
                        out string observedPath) ||
                    !string.Equals(
                        exactCanonicalPath,
                        observedPath,
                        StringComparison.Ordinal))
                {
                    UnixNative.Close(profileDescriptor);
                    return OpenFailure(
                        LastBearingOperationStatus.ConfinementFailure);
                }

                var directory = new UnixProfileDirectory(
                    exactCanonicalPath,
                    terminal,
                    parentDescriptor,
                    profileDescriptor,
                    parentIdentity,
                    profileIdentity);
                parentDescriptor = -1;
                if (directory.RevalidateBinding().Status !=
                    LastBearingOperationStatus.Success)
                {
                    directory.Dispose();
                    return OpenFailure(
                        LastBearingOperationStatus.ConfinementFailure);
                }

                return new LastBearingDirectoryOpenResult(
                    LastBearingOperationStatus.Success,
                    directory);
            }
            finally
            {
                if (parentDescriptor >= 0)
                {
                    UnixNative.Close(parentDescriptor);
                }
            }
        }

        private static LastBearingDirectoryOpenResult OpenFailure(
            LastBearingOperationStatus status)
        {
            return new LastBearingDirectoryOpenResult(status, null);
        }
    }

    internal sealed class UnixProfileDirectory : ILastBearingProfileDirectory
    {
        private readonly string _exactPath;
        private readonly string _terminal;
        private readonly UnixFileIdentity _parentIdentity;
        private readonly UnixFileIdentity _profileIdentity;
        private int _parentDescriptor;
        private int _profileDescriptor;
        private bool _disposed;

        internal UnixProfileDirectory(
            string exactPath,
            string terminal,
            int parentDescriptor,
            int profileDescriptor,
            UnixFileIdentity parentIdentity,
            UnixFileIdentity profileIdentity)
        {
            _exactPath = exactPath;
            _terminal = terminal;
            _parentDescriptor = parentDescriptor;
            _profileDescriptor = profileDescriptor;
            _parentIdentity = parentIdentity;
            _profileIdentity = profileIdentity;
        }

        public LastBearingConfinementResult RevalidateBinding()
        {
            if (_disposed ||
                !UnixNative.TryGetIdentity(
                    _parentDescriptor,
                    out UnixFileIdentity parentIdentity) ||
                !_parentIdentity.Equals(parentIdentity) ||
                !UnixNative.TryGetIdentity(
                    _profileDescriptor,
                    out UnixFileIdentity profileIdentity) ||
                !_profileIdentity.Equals(profileIdentity))
            {
                return ConfinementFailure();
            }

            int rebound = UnixNative.OpenDirectoryAt(
                _parentDescriptor,
                _terminal);
            if (rebound < 0)
            {
                return ConfinementFailure();
            }

            try
            {
                if (!UnixNative.TryGetIdentity(
                        rebound,
                        out UnixFileIdentity reboundIdentity) ||
                    !_profileIdentity.Equals(reboundIdentity) ||
                    !UnixNative.TryGetHandlePath(
                        _profileDescriptor,
                        _exactPath,
                        out string observedPath) ||
                    !string.Equals(
                        _exactPath,
                        observedPath,
                        StringComparison.Ordinal))
                {
                    return ConfinementFailure();
                }
            }
            finally
            {
                UnixNative.Close(rebound);
            }

            return new LastBearingConfinementResult(
                LastBearingOperationStatus.Success);
        }

        public LastBearingLockResult TryAcquireExclusiveLock(
            LastBearingEntryName name)
        {
            if (!name.Equals(LastBearingEntryName.WriterLock) ||
                RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return new LastBearingLockResult(
                    LastBearingOperationStatus.ConfinementFailure,
                    null);
            }

            int descriptor = UnixNative.OpenLockAt(
                _profileDescriptor,
                name.Value);
            if (descriptor < 0)
            {
                return new LastBearingLockResult(
                    UnixNative.OpenFailureStatus(),
                    null);
            }

            if (!TryRequireRegularSingleLink(descriptor) ||
                UnixNative.TryLockExclusiveNonBlocking(descriptor) != 0)
            {
                int error = UnixNative.LastError;
                UnixNative.Close(descriptor);
                return new LastBearingLockResult(
                    UnixNative.IsWouldBlock(error)
                        ? LastBearingOperationStatus.Busy
                        : LastBearingOperationStatus.ConfinementFailure,
                    null);
            }

            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                UnixNative.Unlock(descriptor);
                UnixNative.Close(descriptor);
                return new LastBearingLockResult(
                    LastBearingOperationStatus.ConfinementFailure,
                    null);
            }

            return new LastBearingLockResult(
                LastBearingOperationStatus.Success,
                new UnixFileLease(descriptor));
        }

        public LastBearingReadResult TryReadExact(
            LastBearingEntryName name,
            int maximumBytes)
        {
            if (maximumBytes <= 0 || string.IsNullOrEmpty(name.Value))
            {
                return new LastBearingReadResult(
                    LastBearingOperationStatus.InvalidData,
                    null);
            }

            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return ReadConfinementFailure();
            }

            int descriptor = UnixNative.OpenReadAt(
                _profileDescriptor,
                name.Value);
            if (descriptor < 0)
            {
                LastBearingOperationStatus status =
                    UnixNative.LastError == UnixNative.ErrorNoEntry
                        ? LastBearingOperationStatus.Missing
                        : UnixNative.OpenFailureStatus();
                return new LastBearingReadResult(status, null);
            }

            try
            {
                if (!TryRequireRegularSingleLink(descriptor))
                {
                    return ReadConfinementFailure();
                }

                var bytes = new List<byte>(Math.Min(maximumBytes, 4096));
                var buffer = new byte[Math.Min(maximumBytes + 1, 8192)];
                while (true)
                {
                    long read = UnixNative.Read(descriptor, buffer);
                    if (read < 0)
                    {
                        return new LastBearingReadResult(
                            LastBearingOperationStatus.IoFailure,
                            null);
                    }

                    if (read == 0)
                    {
                        break;
                    }

                    for (int index = 0; index < checked((int)read); index++)
                    {
                        bytes.Add(buffer[index]);
                        if (bytes.Count > maximumBytes)
                        {
                            return new LastBearingReadResult(
                                LastBearingOperationStatus.InvalidData,
                                null);
                        }
                    }
                }

                if (RevalidateBinding().Status !=
                    LastBearingOperationStatus.Success)
                {
                    return ReadConfinementFailure();
                }

                return new LastBearingReadResult(
                    LastBearingOperationStatus.Success,
                    bytes.ToArray());
            }
            finally
            {
                UnixNative.Close(descriptor);
            }
        }

        public LastBearingWriteResult TryWriteNewDurable(
            LastBearingEntryName name,
            byte[] exactBytes)
        {
            if (exactBytes is null)
            {
                throw new ArgumentNullException(nameof(exactBytes));
            }

            if (exactBytes.Length == 0 || string.IsNullOrEmpty(name.Value))
            {
                return new LastBearingWriteResult(
                    LastBearingOperationStatus.InvalidData);
            }

            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return WriteConfinementFailure();
            }

            int descriptor = UnixNative.OpenNewWriteAt(
                _profileDescriptor,
                name.Value);
            if (descriptor < 0)
            {
                LastBearingOperationStatus status =
                    UnixNative.LastError == UnixNative.ErrorExists
                        ? LastBearingOperationStatus.Exists
                        : UnixNative.OpenFailureStatus();
                return new LastBearingWriteResult(status);
            }

            bool succeeded = false;
            try
            {
                if (!TryRequireRegularSingleLink(descriptor) ||
                    !UnixNative.WriteAll(descriptor, exactBytes) ||
                    !UnixNative.FlushFileDurably(descriptor))
                {
                    return new LastBearingWriteResult(
                        LastBearingOperationStatus.IoFailure);
                }

                succeeded = true;
            }
            finally
            {
                UnixNative.Close(descriptor);
            }

            if (!succeeded)
            {
                return new LastBearingWriteResult(
                    LastBearingOperationStatus.IoFailure);
            }

            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return WriteConfinementFailure();
            }

            LastBearingReadResult verification = TryReadExact(
                name,
                exactBytes.Length);
            if (verification.Status != LastBearingOperationStatus.Success ||
                verification.Bytes is null ||
                !LastBearingGenerationCodec.ByteArraysEqual(
                    verification.Bytes,
                    exactBytes))
            {
                return new LastBearingWriteResult(
                    verification.Status ==
                    LastBearingOperationStatus.ConfinementFailure
                        ? LastBearingOperationStatus.ConfinementFailure
                        : LastBearingOperationStatus.IoFailure);
            }

            return new LastBearingWriteResult(
                LastBearingOperationStatus.Success);
        }

        public LastBearingFileResult TryRemoveScratchStage(
            LastBearingEntryName name)
        {
            if (!name.IsScratchStage ||
                RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.ConfinementFailure);
            }

            LastBearingOperationStatus inspection =
                InspectRegularSingleLink(name, out bool exists);
            if (inspection != LastBearingOperationStatus.Success)
            {
                return new LastBearingFileResult(inspection);
            }

            if (!exists)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.Success);
            }

            if (UnixNative.RemoveAt(_profileDescriptor, name.Value) != 0 &&
                UnixNative.LastError != UnixNative.ErrorNoEntry)
            {
                return new LastBearingFileResult(
                    UnixNative.OpenFailureStatus());
            }

            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.ConfinementFailure);
            }

            if (UnixNative.FlushDirectory(_profileDescriptor) != 0)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.IoFailure);
            }

            return RevalidateBinding().Status ==
                LastBearingOperationStatus.Success
                    ? new LastBearingFileResult(
                        LastBearingOperationStatus.Success)
                    : new LastBearingFileResult(
                        LastBearingOperationStatus.ConfinementFailure);
        }

        public LastBearingMoveResult TryMoveNoReplace(
            LastBearingEntryName source,
            LastBearingEntryName destination)
        {
            LastBearingOperationStatus preflight = ValidateMoveEntries(
                source,
                destination,
                destinationMayBeMissing: true,
                out bool destinationExists);
            if (preflight != LastBearingOperationStatus.Success)
            {
                return new LastBearingMoveResult(preflight);
            }

            if (destinationExists)
            {
                return new LastBearingMoveResult(
                    LastBearingOperationStatus.Exists);
            }

            int result = UnixNative.RenameNoReplaceAt(
                _profileDescriptor,
                source.Value,
                destination.Value);
            if (result != 0)
            {
                return new LastBearingMoveResult(
                    UnixNative.LastError == UnixNative.ErrorExists
                        ? LastBearingOperationStatus.Exists
                        : UnixNative.OpenFailureStatus());
            }

            return RevalidateBinding().Status ==
                LastBearingOperationStatus.Success
                    ? new LastBearingMoveResult(
                        LastBearingOperationStatus.Success)
                    : new LastBearingMoveResult(
                        LastBearingOperationStatus.ConfinementFailure);
        }

        public LastBearingMoveResult TryAtomicReplace(
            LastBearingEntryName source,
            LastBearingEntryName destination)
        {
            LastBearingOperationStatus preflight = ValidateMoveEntries(
                source,
                destination,
                destinationMayBeMissing: true,
                out _);
            if (preflight != LastBearingOperationStatus.Success)
            {
                return new LastBearingMoveResult(preflight);
            }

            if (UnixNative.RenameReplaceAt(
                    _profileDescriptor,
                    source.Value,
                    destination.Value) != 0)
            {
                return new LastBearingMoveResult(
                    UnixNative.OpenFailureStatus());
            }

            return RevalidateBinding().Status ==
                LastBearingOperationStatus.Success
                    ? new LastBearingMoveResult(
                        LastBearingOperationStatus.Success)
                    : new LastBearingMoveResult(
                        LastBearingOperationStatus.ConfinementFailure);
        }

        public LastBearingFileResult TryFlushDirectory()
        {
            if (RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.ConfinementFailure);
            }

            if (UnixNative.FlushDirectory(_profileDescriptor) != 0)
            {
                return new LastBearingFileResult(
                    LastBearingOperationStatus.IoFailure);
            }

            return RevalidateBinding().Status ==
                LastBearingOperationStatus.Success
                    ? new LastBearingFileResult(
                        LastBearingOperationStatus.Success)
                    : new LastBearingFileResult(
                        LastBearingOperationStatus.ConfinementFailure);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_profileDescriptor >= 0)
            {
                UnixNative.Close(_profileDescriptor);
                _profileDescriptor = -1;
            }

            if (_parentDescriptor >= 0)
            {
                UnixNative.Close(_parentDescriptor);
                _parentDescriptor = -1;
            }
        }

        private LastBearingOperationStatus ValidateMoveEntries(
            LastBearingEntryName source,
            LastBearingEntryName destination,
            bool destinationMayBeMissing,
            out bool destinationExists)
        {
            destinationExists = false;
            if (string.IsNullOrEmpty(source.Value) ||
                string.IsNullOrEmpty(destination.Value) ||
                RevalidateBinding().Status != LastBearingOperationStatus.Success)
            {
                return LastBearingOperationStatus.ConfinementFailure;
            }

            LastBearingOperationStatus sourceStatus =
                InspectRegularSingleLink(source, out bool sourceExists);
            if (sourceStatus != LastBearingOperationStatus.Success ||
                !sourceExists)
            {
                return sourceStatus == LastBearingOperationStatus.Success
                    ? LastBearingOperationStatus.IoFailure
                    : sourceStatus;
            }

            LastBearingOperationStatus destinationStatus =
                InspectRegularSingleLink(destination, out destinationExists);
            if (destinationStatus != LastBearingOperationStatus.Success)
            {
                return destinationStatus;
            }

            if (!destinationMayBeMissing && !destinationExists)
            {
                return LastBearingOperationStatus.Missing;
            }

            return LastBearingOperationStatus.Success;
        }

        private LastBearingOperationStatus InspectRegularSingleLink(
            LastBearingEntryName name,
            out bool exists)
        {
            exists = false;
            int descriptor = UnixNative.OpenReadAt(
                _profileDescriptor,
                name.Value);
            if (descriptor < 0)
            {
                if (UnixNative.LastError == UnixNative.ErrorNoEntry)
                {
                    return LastBearingOperationStatus.Success;
                }

                return UnixNative.OpenFailureStatus();
            }

            try
            {
                if (!TryRequireRegularSingleLink(descriptor))
                {
                    return LastBearingOperationStatus.ConfinementFailure;
                }

                exists = true;
                return LastBearingOperationStatus.Success;
            }
            finally
            {
                UnixNative.Close(descriptor);
            }
        }

        private static bool TryRequireRegularSingleLink(int descriptor)
        {
            return UnixNative.TryGetIdentity(
                    descriptor,
                    out UnixFileIdentity identity) &&
                identity.IsRegular &&
                identity.LinkCount == 1;
        }

        private static LastBearingConfinementResult ConfinementFailure()
        {
            return new LastBearingConfinementResult(
                LastBearingOperationStatus.ConfinementFailure);
        }

        private static LastBearingReadResult ReadConfinementFailure()
        {
            return new LastBearingReadResult(
                LastBearingOperationStatus.ConfinementFailure,
                null);
        }

        private static LastBearingWriteResult WriteConfinementFailure()
        {
            return new LastBearingWriteResult(
                LastBearingOperationStatus.ConfinementFailure);
        }
    }

    internal readonly struct UnixFileIdentity : IEquatable<UnixFileIdentity>
    {
        internal UnixFileIdentity(
            ulong device,
            ulong inode,
            uint mode,
            ulong linkCount)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            LinkCount = linkCount;
        }

        internal ulong Device { get; }

        internal ulong Inode { get; }

        internal uint Mode { get; }

        internal ulong LinkCount { get; }

        internal bool IsDirectory => (Mode & 0xf000U) == 0x4000U;

        internal bool IsRegular => (Mode & 0xf000U) == 0x8000U;

        public bool Equals(UnixFileIdentity other)
        {
            return Device == other.Device &&
                Inode == other.Inode;
        }

        public override bool Equals(object? obj)
        {
            return obj is UnixFileIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Device.GetHashCode();
                hash = (hash * 397) ^ Inode.GetHashCode();
                return hash;
            }
        }
    }

    internal sealed class UnixFileLease : IDisposable
    {
        private int _descriptor;

        internal UnixFileLease(int descriptor)
        {
            _descriptor = descriptor;
        }

        public void Dispose()
        {
            if (_descriptor < 0)
            {
                return;
            }

            UnixNative.Unlock(_descriptor);
            UnixNative.Close(_descriptor);
            _descriptor = -1;
        }
    }

    internal static class UnixNative
    {
        private const string LibC = "libc";
        private const int LockExclusive = 2;
        private const int LockNonBlocking = 4;
        private const int LockUnlock = 8;
        private const int DarwinFullSync = 51;
        private const uint DarwinRenameExclusive = 4;
        private const uint LinuxRenameNoReplace = 1;

        internal const int ErrorNoEntry = 2;
        internal const int ErrorExists = 17;

        internal static bool IsMac { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        internal static bool IsLinux { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        internal static bool IsSupported { get; } =
            IsMac ||
            (IsLinux && RuntimeInformation.ProcessArchitecture ==
                Architecture.X64);

        internal static int LastError => Marshal.GetLastWin32Error();

        internal static int OpenDirectoryRoot()
        {
            return Open("/", DirectoryReadFlags, 0);
        }

        internal static int OpenDirectoryAt(int parent, string name)
        {
            return OpenAt(parent, name, DirectoryReadFlags, 0);
        }

        internal static int OpenReadAt(int directory, string name)
        {
            return OpenAt(directory, name, ReadFlags, 0);
        }

        internal static int OpenLockAt(int directory, string name)
        {
            return OpenPrivateFileAt(directory, name, LockFlags);
        }

        internal static int OpenNewWriteAt(int directory, string name)
        {
            return OpenPrivateFileAt(directory, name, NewWriteFlags);
        }

        internal static int MakeDirectoryAt(
            int parent,
            string name,
            int mode)
        {
            return MkdirAt(parent, name, mode);
        }

        internal static int TryLockExclusiveNonBlocking(int descriptor)
        {
            return Flock(descriptor, LockExclusive | LockNonBlocking);
        }

        internal static void Unlock(int descriptor)
        {
            Flock(descriptor, LockUnlock);
        }

        internal static bool IsWouldBlock(int error)
        {
            return error == 11 || (IsMac && error == 35);
        }

        internal static LastBearingOperationStatus OpenFailureStatus()
        {
            int error = LastError;
            if (error == ErrorNoEntry)
            {
                return LastBearingOperationStatus.Missing;
            }

            if (error == ErrorExists)
            {
                return LastBearingOperationStatus.Exists;
            }

            if (error == 40 || (IsMac && error == 62) ||
                error == 20 || error == 21 || error == 13)
            {
                return LastBearingOperationStatus.ConfinementFailure;
            }

            return LastBearingOperationStatus.IoFailure;
        }

        internal static bool TryGetIdentity(
            int descriptor,
            out UnixFileIdentity identity)
        {
            identity = default;
            IntPtr buffer = Marshal.AllocHGlobal(256);
            try
            {
                for (int offset = 0; offset < 256; offset += 8)
                {
                    Marshal.WriteInt64(buffer, offset, 0);
                }

                if (FStat(descriptor, buffer) != 0)
                {
                    return false;
                }

                if (IsMac)
                {
                    ulong device = unchecked((uint)Marshal.ReadInt32(buffer, 0));
                    uint mode = unchecked((ushort)Marshal.ReadInt16(buffer, 4));
                    ulong links = unchecked((ushort)Marshal.ReadInt16(buffer, 6));
                    ulong inode = unchecked((ulong)Marshal.ReadInt64(buffer, 8));
                    identity = new UnixFileIdentity(
                        device,
                        inode,
                        mode,
                        links);
                    return true;
                }

                ulong linuxDevice = unchecked(
                    (ulong)Marshal.ReadInt64(buffer, 0));
                ulong linuxInode = unchecked(
                    (ulong)Marshal.ReadInt64(buffer, 8));
                ulong linuxLinks = unchecked(
                    (ulong)Marshal.ReadInt64(buffer, 16));
                uint linuxMode = unchecked(
                    (uint)Marshal.ReadInt32(buffer, 24));
                identity = new UnixFileIdentity(
                    linuxDevice,
                    linuxInode,
                    linuxMode,
                    linuxLinks);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        internal static bool TryGetHandlePath(
            int descriptor,
            string expectedPath,
            out string path)
        {
            path = string.Empty;
            var buffer = new byte[4096];
            if (IsMac)
            {
                IntPtr nativeBuffer = Marshal.AllocHGlobal(buffer.Length);
                try
                {
                    if (RealPath(expectedPath, nativeBuffer) == IntPtr.Zero)
                    {
                        return false;
                    }

                    Marshal.Copy(nativeBuffer, buffer, 0, buffer.Length);
                }
                finally
                {
                    Marshal.FreeHGlobal(nativeBuffer);
                }

                int terminator = Array.IndexOf(buffer, (byte)0);
                if (terminator <= 0)
                {
                    return false;
                }

                path = Encoding.UTF8.GetString(buffer, 0, terminator);
                return true;
            }

            string descriptorPath = "/proc/self/fd/" +
                descriptor.ToString(CultureInfo.InvariantCulture);
            long length = ReadLink(
                descriptorPath,
                buffer,
                checked((UIntPtr)buffer.Length)).ToInt64();
            if (length <= 0 || length >= buffer.Length)
            {
                return false;
            }

            path = Encoding.UTF8.GetString(buffer, 0, checked((int)length));
            return true;
        }

        internal static long Read(int descriptor, byte[] buffer)
        {
            return NativeRead(
                descriptor,
                buffer,
                checked((UIntPtr)buffer.Length)).ToInt64();
        }

        internal static bool WriteAll(int descriptor, byte[] bytes)
        {
            IntPtr memory = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                int offset = 0;
                while (offset < bytes.Length)
                {
                    long written = NativeWrite(
                        descriptor,
                        IntPtr.Add(memory, offset),
                        checked((UIntPtr)(bytes.Length - offset))).ToInt64();
                    if (written <= 0)
                    {
                        return false;
                    }

                    offset = checked(offset + (int)written);
                }

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        internal static bool FlushFileDurably(int descriptor)
        {
            return IsMac
                ? FcntlInt(descriptor, DarwinFullSync, 0) == 0
                : Fsync(descriptor) == 0;
        }

        internal static int FlushDirectory(int descriptor)
        {
            return Fsync(descriptor);
        }

        internal static int RenameNoReplaceAt(
            int directory,
            string source,
            string destination)
        {
            return IsMac
                ? RenameAtExclusiveMac(
                    directory,
                    source,
                    directory,
                    destination,
                    DarwinRenameExclusive)
                : RenameAtNoReplaceLinux(
                    directory,
                    source,
                    directory,
                    destination,
                    LinuxRenameNoReplace);
        }

        internal static int RenameReplaceAt(
            int directory,
            string source,
            string destination)
        {
            return RenameAt(directory, source, directory, destination);
        }

        internal static int RemoveAt(int directory, string name)
        {
            return UnlinkAt(directory, name, 0);
        }

        internal static int Close(int descriptor)
        {
            return NativeClose(descriptor);
        }

        private static int OpenPrivateFileAt(
            int directory,
            string name,
            int flags)
        {
            int descriptor = OpenAt(directory, name, flags, 384);
            if (descriptor >= 0 && Fchmod(descriptor, 384) != 0)
            {
                NativeClose(descriptor);
                return -1;
            }

            return descriptor;
        }

        private static int DirectoryReadFlags =>
            IsMac
                ? 0x01000000 | 0x00100000 | 0x00000100
                : 0x00080000 | 0x00010000 | 0x00020000;

        private static int ReadFlags =>
            IsMac
                ? 0x01000000 | 0x00000100 | 0x00000004
                : 0x00080000 | 0x00020000 | 0x00000800;

        private static int LockFlags =>
            2 | (IsMac ? 0x01000000 | 0x00000100 | 0x00000200
                       : 0x00080000 | 0x00020000 | 0x00000040);

        private static int NewWriteFlags =>
            1 | (IsMac
                ? 0x01000000 | 0x00000100 | 0x00000200 | 0x00000800
                : 0x00080000 | 0x00020000 | 0x00000040 | 0x00000080);

        [DllImport(LibC, EntryPoint = "open", SetLastError = true)]
        private static extern int Open(string path, int flags, int mode);

        [DllImport(LibC, EntryPoint = "openat", SetLastError = true)]
        private static extern int OpenAt(
            int directory,
            string path,
            int flags,
            int mode);

        [DllImport(LibC, EntryPoint = "mkdirat", SetLastError = true)]
        private static extern int MkdirAt(int directory, string path, int mode);

        [DllImport(LibC, EntryPoint = "fstat", SetLastError = true)]
        private static extern int FStat(int descriptor, IntPtr buffer);

        [DllImport(LibC, EntryPoint = "fchmod", SetLastError = true)]
        private static extern int Fchmod(int descriptor, int mode);

        [DllImport(LibC, EntryPoint = "read", SetLastError = true)]
        private static extern IntPtr NativeRead(
            int descriptor,
            byte[] buffer,
            UIntPtr count);

        [DllImport(LibC, EntryPoint = "write", SetLastError = true)]
        private static extern IntPtr NativeWrite(
            int descriptor,
            IntPtr buffer,
            UIntPtr count);

        [DllImport(LibC, EntryPoint = "readlink", SetLastError = true)]
        private static extern IntPtr ReadLink(
            string path,
            byte[] buffer,
            UIntPtr count);

        [DllImport(LibC, EntryPoint = "fsync", SetLastError = true)]
        private static extern int Fsync(int descriptor);

        [DllImport(LibC, EntryPoint = "flock", SetLastError = true)]
        private static extern int Flock(int descriptor, int operation);

        [DllImport(LibC, EntryPoint = "fcntl", SetLastError = true)]
        private static extern int FcntlInt(
            int descriptor,
            int command,
            int value);

        [DllImport(LibC, EntryPoint = "realpath", SetLastError = true)]
        private static extern IntPtr RealPath(string path, IntPtr resolvedPath);

        [DllImport(LibC, EntryPoint = "renameat", SetLastError = true)]
        private static extern int RenameAt(
            int oldDirectory,
            string oldName,
            int newDirectory,
            string newName);

        [DllImport(LibC, EntryPoint = "renameat2", SetLastError = true)]
        private static extern int RenameAtNoReplaceLinux(
            int oldDirectory,
            string oldName,
            int newDirectory,
            string newName,
            uint flags);

        [DllImport(LibC, EntryPoint = "renameatx_np", SetLastError = true)]
        private static extern int RenameAtExclusiveMac(
            int oldDirectory,
            string oldName,
            int newDirectory,
            string newName,
            uint flags);

        [DllImport(LibC, EntryPoint = "unlinkat", SetLastError = true)]
        private static extern int UnlinkAt(
            int directory,
            string name,
            int flags);

        [DllImport(LibC, EntryPoint = "close", SetLastError = true)]
        private static extern int NativeClose(int descriptor);
    }
}
