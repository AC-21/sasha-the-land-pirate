#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AtomicLandPirate.Save.LastBearing
{
    public sealed class LastBearingProfileStore
    {
        private readonly string _exactProfileDirectory;
        private readonly ILastBearingFileOperations _operations;

        private LastBearingProfileStore(
            string exactProfileDirectory,
            ILastBearingFileOperations operations)
        {
            _exactProfileDirectory = exactProfileDirectory;
            _operations = operations;
        }

        public static LastBearingProfileStore OpenFixedProfileDirectory(
            string exactProfileDirectory)
        {
            ValidateExactProfileDirectory(exactProfileDirectory);
            return new LastBearingProfileStore(
                exactProfileDirectory,
                LastBearingFileOperations.Instance);
        }

        public LastBearingPersistResult TryPersist(byte[] canonicalPayload)
        {
            ValidatePayload(canonicalPayload);
            byte[] payload = (byte[])canonicalPayload.Clone();

            try
            {
                LastBearingDirectoryOpenResult open =
                    _operations.TryOpenProfileDirectory(
                        _exactProfileDirectory,
                        createFixedTerminalIfMissing: true);
                if (open.Status != LastBearingOperationStatus.Success ||
                    open.Directory is null)
                {
                    return PersistOperationFailure(open.Status);
                }

                using (ILastBearingProfileDirectory directory = open.Directory)
                {
                    LastBearingLockResult lockResult =
                        directory.TryAcquireExclusiveLock(
                            LastBearingEntryName.WriterLock);
                    if (lockResult.Status != LastBearingOperationStatus.Success ||
                        lockResult.Lease is null)
                    {
                        return lockResult.Status == LastBearingOperationStatus.Busy
                            ? LastBearingPersistResult.Failure(
                                LastBearingSaveCodes.WriterBusy)
                            : PersistOperationFailure(lockResult.Status);
                    }

                    using (lockResult.Lease)
                    {
                        return PersistUnderLease(directory, payload);
                    }
                }
            }
            catch (Exception exception) when (IsOperationalException(exception))
            {
                return LastBearingPersistResult.Failure(
                    LastBearingSaveCodes.InterruptedWrite);
            }
        }

        public LastBearingLoadResult TryLoad(
            Func<byte[], bool> canonicalPayloadValidator)
        {
            if (canonicalPayloadValidator is null)
            {
                throw new ArgumentNullException(nameof(canonicalPayloadValidator));
            }

            try
            {
                LastBearingDirectoryOpenResult open =
                    _operations.TryOpenProfileDirectory(
                        _exactProfileDirectory,
                        createFixedTerminalIfMissing: false);
                if (open.Status == LastBearingOperationStatus.Missing)
                {
                    return LastBearingLoadResult.Failure(
                        LastBearingSaveCodes.NoProfile);
                }

                if (open.Status != LastBearingOperationStatus.Success ||
                    open.Directory is null)
                {
                    return LoadOperationFailure(open.Status);
                }

                using (ILastBearingProfileDirectory directory = open.Directory)
                {
                    LastBearingCandidate current = ReadCandidate(
                        directory,
                        LastBearingEntryName.CurrentPointer,
                        canonicalPayloadValidator);
                    if (current.Status == LastBearingCandidateStatus.Valid &&
                        current.Generation is not null)
                    {
                        return LastBearingLoadResult.Success(
                            LastBearingSaveCodes.SaveOk,
                            current.Generation.Generation,
                            false,
                            current.Generation.Payload);
                    }

                    if (current.Status == LastBearingCandidateStatus.UnknownVersion)
                    {
                        return LastBearingLoadResult.Failure(
                            LastBearingSaveCodes.UnknownVersion);
                    }

                    if (current.Status ==
                            LastBearingCandidateStatus.ConfinementFailure ||
                        current.Status == LastBearingCandidateStatus.IoFailure)
                    {
                        return LoadCandidateFailure(current.Status);
                    }

                    LastBearingCandidate lastGood = ReadCandidate(
                        directory,
                        LastBearingEntryName.LastGoodPointer,
                        canonicalPayloadValidator);
                    if (lastGood.Status == LastBearingCandidateStatus.Valid &&
                        lastGood.Generation is not null)
                    {
                        return LastBearingLoadResult.Success(
                            LastBearingSaveCodes.RecoveredLastGood,
                            lastGood.Generation.Generation,
                            true,
                            lastGood.Generation.Payload);
                    }

                    if (lastGood.Status ==
                            LastBearingCandidateStatus.ConfinementFailure ||
                        lastGood.Status == LastBearingCandidateStatus.IoFailure)
                    {
                        return LoadCandidateFailure(lastGood.Status);
                    }

                    if (current.Status == LastBearingCandidateStatus.Missing &&
                        lastGood.Status == LastBearingCandidateStatus.Missing)
                    {
                        return LastBearingLoadResult.Failure(
                            LastBearingSaveCodes.NoProfile);
                    }

                    if (current.Status == LastBearingCandidateStatus.UnknownVersion ||
                        lastGood.Status == LastBearingCandidateStatus.UnknownVersion)
                    {
                        return LastBearingLoadResult.Failure(
                            LastBearingSaveCodes.UnknownVersion);
                    }

                    if (current.Status != LastBearingCandidateStatus.Missing &&
                        lastGood.Status != LastBearingCandidateStatus.Missing)
                    {
                        return LastBearingLoadResult.Failure(
                            LastBearingSaveCodes.BothCorrupt);
                    }

                    return LastBearingLoadResult.Failure(
                        LastBearingSaveCodes.CurrentCorrupt);
                }
            }
            catch (Exception exception) when (IsOperationalException(exception))
            {
                return LastBearingLoadResult.Failure(
                    LastBearingSaveCodes.InterruptedWrite);
            }
        }

        internal static LastBearingProfileStore OpenFixedProfileDirectoryForTests(
            string exactProfileDirectory,
            ILastBearingFileOperations operations)
        {
            ValidateExactProfileDirectory(exactProfileDirectory);
            if (operations is null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            return new LastBearingProfileStore(
                exactProfileDirectory,
                operations);
        }

        private LastBearingPersistResult PersistUnderLease(
            ILastBearingProfileDirectory directory,
            byte[] payload)
        {
            LastBearingCandidate current = ReadCandidate(
                directory,
                LastBearingEntryName.CurrentPointer,
                validator: null);
            if (current.Status != LastBearingCandidateStatus.Valid &&
                current.Status != LastBearingCandidateStatus.Missing)
            {
                return PersistCandidateFailure(current.Status);
            }

            if (current.Status == LastBearingCandidateStatus.Missing)
            {
                LastBearingReadResult lastGoodPresence = directory.TryReadExact(
                    LastBearingEntryName.LastGoodPointer,
                    LastBearingProfileContract.PointerBytes);
                if (lastGoodPresence.Status != LastBearingOperationStatus.Missing)
                {
                    if (lastGoodPresence.Status ==
                        LastBearingOperationStatus.ConfinementFailure)
                    {
                        return LastBearingPersistResult.Failure(
                            LastBearingSaveCodes.ConfinementFailure);
                    }

                    if (lastGoodPresence.Status == LastBearingOperationStatus.IoFailure)
                    {
                        return LastBearingPersistResult.Failure(
                            LastBearingSaveCodes.InterruptedWrite);
                    }

                    return LastBearingPersistResult.Failure(
                        LastBearingSaveCodes.CurrentCorrupt);
                }
            }

            if (current.Generation is not null &&
                LastBearingGenerationCodec.ByteArraysEqual(
                    current.Generation.Payload,
                    payload))
            {
                LastBearingFileResult flush = directory.TryFlushDirectory();
                return flush.Status == LastBearingOperationStatus.Success
                    ? LastBearingPersistResult.Success(
                        current.Generation.Generation,
                        true,
                        0)
                    : PersistOperationFailure(flush.Status);
            }

            ulong nextGeneration;
            if (current.Generation is null)
            {
                nextGeneration = 1;
            }
            else if (current.Generation.Generation == ulong.MaxValue)
            {
                return LastBearingPersistResult.Failure(
                    LastBearingSaveCodes.CurrentCorrupt,
                    current.Generation.Generation);
            }
            else
            {
                nextGeneration = checked(current.Generation.Generation + 1);
            }

            LastBearingGenerationRecord next =
                LastBearingGenerationCodec.EncodeGeneration(
                    nextGeneration,
                    payload);
            long bytesWritten = 0;
            LastBearingOperationStatus generationStatus =
                EnsureImmutableGeneration(
                    directory,
                    next,
                    ref bytesWritten);
            if (generationStatus != LastBearingOperationStatus.Success)
            {
                return PersistOperationFailure(generationStatus);
            }

            if (current.Pointer is not null)
            {
                byte[] previousPointerBytes = current.Pointer.EncodedBytes;
                LastBearingOperationStatus lastGoodStatus = PublishPointer(
                    directory,
                    LastBearingEntryName.LastGoodPointer,
                    LastBearingEntryName.ForPointerStage(
                        current: false,
                        previousPointerBytes),
                    previousPointerBytes,
                    ref bytesWritten);
                if (lastGoodStatus != LastBearingOperationStatus.Success)
                {
                    return PersistOperationFailure(lastGoodStatus);
                }
            }

            LastBearingPointerRecord nextPointer =
                LastBearingGenerationCodec.EncodePointer(next);
            byte[] nextPointerBytes = nextPointer.EncodedBytes;
            LastBearingOperationStatus currentStatus = PublishPointer(
                directory,
                LastBearingEntryName.CurrentPointer,
                LastBearingEntryName.ForPointerStage(
                    current: true,
                    nextPointerBytes),
                nextPointerBytes,
                ref bytesWritten);
            if (currentStatus != LastBearingOperationStatus.Success)
            {
                return PersistOperationFailure(currentStatus);
            }

            return LastBearingPersistResult.Success(
                nextGeneration,
                false,
                bytesWritten);
        }

        private static LastBearingOperationStatus EnsureImmutableGeneration(
            ILastBearingProfileDirectory directory,
            LastBearingGenerationRecord generation,
            ref long bytesWritten)
        {
            byte[] generationBytes = generation.EncodedBytes;
            LastBearingEntryName stage =
                LastBearingEntryName.ForGenerationStage(
                    generation.Generation,
                    LastBearingGenerationCodec.ToLowerHex(
                        generation.WholeDigest));

            LastBearingOperationStatus stageStatus = EnsureScratchStage(
                directory,
                stage,
                generationBytes,
                ref bytesWritten);
            if (stageStatus != LastBearingOperationStatus.Success)
            {
                return stageStatus;
            }

            LastBearingMoveResult move = directory.TryMoveNoReplace(
                stage,
                generation.FileName);
            if (move.Status == LastBearingOperationStatus.Success)
            {
                LastBearingFileResult flush = directory.TryFlushDirectory();
                if (flush.Status != LastBearingOperationStatus.Success)
                {
                    return flush.Status;
                }
            }
            else if (move.Status == LastBearingOperationStatus.Exists)
            {
                LastBearingOperationStatus publishedStatus = VerifyExactEntry(
                    directory,
                    generation.FileName,
                    generationBytes);
                if (publishedStatus != LastBearingOperationStatus.Success)
                {
                    return publishedStatus;
                }

                LastBearingFileResult removal =
                    directory.TryRemoveScratchStage(stage);
                return removal.Status;
            }
            else
            {
                return move.Status;
            }

            return VerifyExactEntry(
                directory,
                generation.FileName,
                generationBytes);
        }

        private static LastBearingOperationStatus PublishPointer(
            ILastBearingProfileDirectory directory,
            LastBearingEntryName destination,
            LastBearingEntryName stage,
            byte[] pointerBytes,
            ref long bytesWritten)
        {
            LastBearingOperationStatus stageStatus = EnsureScratchStage(
                directory,
                stage,
                pointerBytes,
                ref bytesWritten);
            if (stageStatus != LastBearingOperationStatus.Success)
            {
                return stageStatus;
            }

            LastBearingMoveResult replace = directory.TryAtomicReplace(
                stage,
                destination);
            if (replace.Status != LastBearingOperationStatus.Success)
            {
                return replace.Status;
            }

            LastBearingFileResult flush = directory.TryFlushDirectory();
            if (flush.Status != LastBearingOperationStatus.Success)
            {
                return flush.Status;
            }

            return VerifyExactEntry(directory, destination, pointerBytes);
        }

        private static LastBearingOperationStatus EnsureScratchStage(
            ILastBearingProfileDirectory directory,
            LastBearingEntryName stage,
            byte[] exactBytes,
            ref long bytesWritten)
        {
            if (!stage.IsScratchStage)
            {
                return LastBearingOperationStatus.ConfinementFailure;
            }

            LastBearingWriteResult write = directory.TryWriteNewDurable(
                stage,
                exactBytes);
            if (write.Status == LastBearingOperationStatus.Success)
            {
                bytesWritten = checked(bytesWritten + exactBytes.Length);
                return LastBearingOperationStatus.Success;
            }

            if (write.Status != LastBearingOperationStatus.Exists)
            {
                return write.Status;
            }

            LastBearingOperationStatus verification = VerifyExactEntry(
                directory,
                stage,
                exactBytes);
            if (verification == LastBearingOperationStatus.Success)
            {
                return LastBearingOperationStatus.Success;
            }

            if (verification == LastBearingOperationStatus.ConfinementFailure)
            {
                return verification;
            }

            LastBearingFileResult removal =
                directory.TryRemoveScratchStage(stage);
            if (removal.Status != LastBearingOperationStatus.Success)
            {
                return removal.Status;
            }

            write = directory.TryWriteNewDurable(stage, exactBytes);
            if (write.Status != LastBearingOperationStatus.Success)
            {
                return write.Status;
            }

            bytesWritten = checked(bytesWritten + exactBytes.Length);
            return LastBearingOperationStatus.Success;
        }

        private static LastBearingOperationStatus VerifyExactEntry(
            ILastBearingProfileDirectory directory,
            LastBearingEntryName name,
            byte[] expectedBytes)
        {
            LastBearingReadResult read = directory.TryReadExact(
                name,
                expectedBytes.Length);
            if (read.Status != LastBearingOperationStatus.Success)
            {
                return read.Status;
            }

            return read.Bytes is not null &&
                LastBearingGenerationCodec.ByteArraysEqual(
                    read.Bytes,
                    expectedBytes)
                    ? LastBearingOperationStatus.Success
                    : LastBearingOperationStatus.IoFailure;
        }

        private static LastBearingCandidate ReadCandidate(
            ILastBearingProfileDirectory directory,
            LastBearingEntryName pointerName,
            Func<byte[], bool>? validator)
        {
            LastBearingReadResult pointerRead = directory.TryReadExact(
                pointerName,
                LastBearingProfileContract.PointerBytes);
            LastBearingCandidateStatus pointerStatus =
                CandidateStatusFromOperation(pointerRead.Status);
            if (pointerStatus != LastBearingCandidateStatus.Valid ||
                pointerRead.Bytes is null)
            {
                return new LastBearingCandidate(pointerStatus, null, null);
            }

            LastBearingDecodeStatus pointerDecode =
                LastBearingGenerationCodec.TryDecodePointer(
                    pointerRead.Bytes,
                    out LastBearingPointerRecord? pointer);
            if (pointerDecode != LastBearingDecodeStatus.Success ||
                pointer is null)
            {
                return new LastBearingCandidate(
                    pointerDecode == LastBearingDecodeStatus.UnknownVersion
                        ? LastBearingCandidateStatus.UnknownVersion
                        : LastBearingCandidateStatus.Corrupt,
                    null,
                    null);
            }

            LastBearingReadResult generationRead = directory.TryReadExact(
                pointer.GenerationFileName,
                LastBearingProfileContract.GenerationHeaderBytes +
                LastBearingProfileContract.MaxCanonicalPayloadBytes);
            LastBearingCandidateStatus generationStatus =
                CandidateStatusFromOperation(generationRead.Status);
            if (generationStatus != LastBearingCandidateStatus.Valid ||
                generationRead.Bytes is null)
            {
                return new LastBearingCandidate(
                    generationStatus == LastBearingCandidateStatus.Missing
                        ? LastBearingCandidateStatus.Corrupt
                        : generationStatus,
                    pointer,
                    null);
            }

            LastBearingDecodeStatus generationDecode =
                LastBearingGenerationCodec.TryDecodeGeneration(
                    pointer.GenerationFileName,
                    generationRead.Bytes,
                    out LastBearingGenerationRecord? generation);
            if (generationDecode != LastBearingDecodeStatus.Success ||
                generation is null)
            {
                return new LastBearingCandidate(
                    generationDecode == LastBearingDecodeStatus.UnknownVersion
                        ? LastBearingCandidateStatus.UnknownVersion
                        : LastBearingCandidateStatus.Corrupt,
                    pointer,
                    null);
            }

            if (pointer.Generation != generation.Generation ||
                !LastBearingGenerationCodec.ByteArraysEqual(
                    pointer.WholeGenerationDigest,
                    generation.WholeDigest))
            {
                return new LastBearingCandidate(
                    LastBearingCandidateStatus.Corrupt,
                    pointer,
                    null);
            }

            if (validator is not null)
            {
                bool valid;
                try
                {
                    valid = validator(generation.Payload);
                }
                catch (Exception)
                {
                    valid = false;
                }

                if (!valid)
                {
                    return new LastBearingCandidate(
                        LastBearingCandidateStatus.Corrupt,
                        pointer,
                        null);
                }
            }

            return new LastBearingCandidate(
                LastBearingCandidateStatus.Valid,
                pointer,
                generation);
        }

        private static LastBearingCandidateStatus CandidateStatusFromOperation(
            LastBearingOperationStatus status)
        {
            switch (status)
            {
                case LastBearingOperationStatus.Success:
                    return LastBearingCandidateStatus.Valid;
                case LastBearingOperationStatus.Missing:
                    return LastBearingCandidateStatus.Missing;
                case LastBearingOperationStatus.ConfinementFailure:
                    return LastBearingCandidateStatus.ConfinementFailure;
                case LastBearingOperationStatus.IoFailure:
                    return LastBearingCandidateStatus.IoFailure;
                default:
                    return LastBearingCandidateStatus.Corrupt;
            }
        }

        private static LastBearingPersistResult PersistCandidateFailure(
            LastBearingCandidateStatus status)
        {
            switch (status)
            {
                case LastBearingCandidateStatus.UnknownVersion:
                    return LastBearingPersistResult.Failure(
                        LastBearingSaveCodes.UnknownVersion);
                case LastBearingCandidateStatus.ConfinementFailure:
                    return LastBearingPersistResult.Failure(
                        LastBearingSaveCodes.ConfinementFailure);
                case LastBearingCandidateStatus.IoFailure:
                    return LastBearingPersistResult.Failure(
                        LastBearingSaveCodes.InterruptedWrite);
                default:
                    return LastBearingPersistResult.Failure(
                        LastBearingSaveCodes.CurrentCorrupt);
            }
        }

        private static LastBearingPersistResult PersistOperationFailure(
            LastBearingOperationStatus status)
        {
            if (status == LastBearingOperationStatus.ConfinementFailure)
            {
                return LastBearingPersistResult.Failure(
                    LastBearingSaveCodes.ConfinementFailure);
            }

            if (status == LastBearingOperationStatus.Busy)
            {
                return LastBearingPersistResult.Failure(
                    LastBearingSaveCodes.WriterBusy);
            }

            return LastBearingPersistResult.Failure(
                LastBearingSaveCodes.InterruptedWrite);
        }

        private static LastBearingLoadResult LoadCandidateFailure(
            LastBearingCandidateStatus status)
        {
            return status == LastBearingCandidateStatus.ConfinementFailure
                ? LastBearingLoadResult.Failure(
                    LastBearingSaveCodes.ConfinementFailure)
                : LastBearingLoadResult.Failure(
                    LastBearingSaveCodes.InterruptedWrite);
        }

        private static LastBearingLoadResult LoadOperationFailure(
            LastBearingOperationStatus status)
        {
            return status == LastBearingOperationStatus.ConfinementFailure
                ? LastBearingLoadResult.Failure(
                    LastBearingSaveCodes.ConfinementFailure)
                : LastBearingLoadResult.Failure(
                    LastBearingSaveCodes.InterruptedWrite);
        }

        private static void ValidatePayload(byte[] canonicalPayload)
        {
            if (canonicalPayload is null)
            {
                throw new ArgumentNullException(nameof(canonicalPayload));
            }

            if (canonicalPayload.Length == 0 ||
                canonicalPayload.Length >
                LastBearingProfileContract.MaxCanonicalPayloadBytes)
            {
                throw new ArgumentException(
                    "Canonical payload length is outside the Last Bearing bound.",
                    nameof(canonicalPayload));
            }
        }

        private static void ValidateExactProfileDirectory(
            string exactProfileDirectory)
        {
            if (exactProfileDirectory is null)
            {
                throw new ArgumentNullException(nameof(exactProfileDirectory));
            }

            if (exactProfileDirectory.Length == 0 ||
                exactProfileDirectory.IndexOf('\0') >= 0 ||
                exactProfileDirectory.IndexOf('\\') >= 0 ||
                exactProfileDirectory.EndsWith("/", StringComparison.Ordinal) ||
                exactProfileDirectory.IndexOf("//", StringComparison.Ordinal) >= 0 ||
                !Path.IsPathFullyQualified(exactProfileDirectory))
            {
                throw new ArgumentException(
                    "Profile directory must be one exact canonical absolute path.",
                    nameof(exactProfileDirectory));
            }

            string canonical;
            try
            {
                canonical = Path.GetFullPath(exactProfileDirectory);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                throw new ArgumentException(
                    "Profile directory is not a canonical path.",
                    nameof(exactProfileDirectory),
                    exception);
            }

            string[] components = exactProfileDirectory.Substring(1).Split('/');
            if (!string.Equals(
                    canonical,
                    exactProfileDirectory,
                    StringComparison.Ordinal) ||
                components.Length == 0 ||
                Array.Exists(
                    components,
                    component =>
                        component.Length == 0 ||
                        component == "." ||
                        component == "..") ||
                !string.Equals(
                    components[components.Length - 1],
                    LastBearingProfileContract.ProfileName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Profile directory must end at the fixed Last Bearing child.",
                    nameof(exactProfileDirectory));
            }
        }

        private static bool IsOperationalException(Exception exception)
        {
            return exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException ||
                exception is ExternalException ||
                exception is ObjectDisposedException ||
                exception is OverflowException;
        }

        private enum LastBearingCandidateStatus
        {
            Valid = 0,
            Missing = 1,
            Corrupt = 2,
            UnknownVersion = 3,
            ConfinementFailure = 4,
            IoFailure = 5,
        }

        private sealed class LastBearingCandidate
        {
            internal LastBearingCandidate(
                LastBearingCandidateStatus status,
                LastBearingPointerRecord? pointer,
                LastBearingGenerationRecord? generation)
            {
                Status = status;
                Pointer = pointer;
                Generation = generation;
            }

            internal LastBearingCandidateStatus Status { get; }

            internal LastBearingPointerRecord? Pointer { get; }

            internal LastBearingGenerationRecord? Generation { get; }
        }
    }
}
