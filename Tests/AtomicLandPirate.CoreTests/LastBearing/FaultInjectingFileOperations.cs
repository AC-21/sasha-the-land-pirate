#nullable enable

using System;
using AtomicLandPirate.Save.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal enum SaveFaultPoint
    {
        None = 0,
        AfterGenerationStageDurableClose = 1,
        AfterGenerationPublication = 2,
        AfterLastGoodPublication = 3,
        AfterCurrentPublication = 4,
        PartialGenerationStage = 5,
        PartialLastGoodPointerStage = 6,
        PartialCurrentPointerStage = 7,
    }

    internal sealed class FaultInjectingFileOperations : ILastBearingFileOperations
    {
        private readonly ILastBearingFileOperations _inner;
        private readonly SaveFaultPoint _point;
        private bool _fired;

        public FaultInjectingFileOperations(
            ILastBearingFileOperations inner,
            SaveFaultPoint point)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _point = point;
        }

        public bool Fired => _fired;

        public LastBearingDirectoryOpenResult TryOpenProfileDirectory(
            string exactCanonicalPath,
            bool createFixedTerminalIfMissing)
        {
            LastBearingDirectoryOpenResult opened = _inner.TryOpenProfileDirectory(
                exactCanonicalPath,
                createFixedTerminalIfMissing);
            if (opened.Status != LastBearingOperationStatus.Success || opened.Directory == null)
            {
                return opened;
            }

            return new LastBearingDirectoryOpenResult(
                LastBearingOperationStatus.Success,
                new DirectoryProxy(opened.Directory, this));
        }

        private bool Trip(SaveFaultPoint point)
        {
            if (_fired || _point != point)
            {
                return false;
            }

            _fired = true;
            return true;
        }

        private sealed class DirectoryProxy : ILastBearingProfileDirectory
        {
            private readonly ILastBearingProfileDirectory _inner;
            private readonly FaultInjectingFileOperations _owner;

            internal DirectoryProxy(
                ILastBearingProfileDirectory inner,
                FaultInjectingFileOperations owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public LastBearingConfinementResult RevalidateBinding()
            {
                return _inner.RevalidateBinding();
            }

            public LastBearingLockResult TryAcquireExclusiveLock(
                LastBearingEntryName name)
            {
                return _inner.TryAcquireExclusiveLock(name);
            }

            public LastBearingReadResult TryReadExact(
                LastBearingEntryName name,
                int maximumBytes)
            {
                return _inner.TryReadExact(name, maximumBytes);
            }

            public LastBearingWriteResult TryWriteNewDurable(
                LastBearingEntryName name,
                byte[] exactBytes)
            {
                SaveFaultPoint partialPoint = PartialFaultPoint(name);
                if (partialPoint != SaveFaultPoint.None &&
                    _owner.Trip(partialPoint))
                {
                    int partialLength = Math.Max(1, exactBytes.Length / 2);
                    var partialBytes = new byte[partialLength];
                    Buffer.BlockCopy(
                        exactBytes,
                        0,
                        partialBytes,
                        0,
                        partialBytes.Length);
                    LastBearingWriteResult partial =
                        _inner.TryWriteNewDurable(name, partialBytes);
                    return partial.Status == LastBearingOperationStatus.Success
                        ? new LastBearingWriteResult(
                            LastBearingOperationStatus.IoFailure)
                        : partial;
                }

                LastBearingWriteResult result = _inner.TryWriteNewDurable(name, exactBytes);
                if (result.Status == LastBearingOperationStatus.Success &&
                    name.Value.StartsWith(".stage-gen-", StringComparison.Ordinal) &&
                    _owner.Trip(SaveFaultPoint.AfterGenerationStageDurableClose))
                {
                    return new LastBearingWriteResult(LastBearingOperationStatus.IoFailure);
                }

                return result;
            }

            public LastBearingFileResult TryRemoveScratchStage(
                LastBearingEntryName name)
            {
                return _inner.TryRemoveScratchStage(name);
            }

            public LastBearingMoveResult TryMoveNoReplace(
                LastBearingEntryName source,
                LastBearingEntryName destination)
            {
                LastBearingMoveResult result = _inner.TryMoveNoReplace(source, destination);
                if (result.Status == LastBearingOperationStatus.Success &&
                    _owner.Trip(SaveFaultPoint.AfterGenerationPublication))
                {
                    return new LastBearingMoveResult(LastBearingOperationStatus.IoFailure);
                }

                return result;
            }

            public LastBearingMoveResult TryAtomicReplace(
                LastBearingEntryName source,
                LastBearingEntryName destination)
            {
                LastBearingMoveResult result = _inner.TryAtomicReplace(source, destination);
                if (result.Status != LastBearingOperationStatus.Success)
                {
                    return result;
                }

                if (string.Equals(
                        destination.Value,
                        LastBearingProfileContract.LastGoodPointerName,
                        StringComparison.Ordinal) &&
                    _owner.Trip(SaveFaultPoint.AfterLastGoodPublication))
                {
                    return new LastBearingMoveResult(LastBearingOperationStatus.IoFailure);
                }

                if (string.Equals(
                        destination.Value,
                        LastBearingProfileContract.CurrentPointerName,
                        StringComparison.Ordinal) &&
                    _owner.Trip(SaveFaultPoint.AfterCurrentPublication))
                {
                    return new LastBearingMoveResult(LastBearingOperationStatus.IoFailure);
                }

                return result;
            }

            public LastBearingFileResult TryFlushDirectory()
            {
                return _inner.TryFlushDirectory();
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            private static SaveFaultPoint PartialFaultPoint(
                LastBearingEntryName name)
            {
                if (name.Value.StartsWith(
                        ".stage-gen-",
                        StringComparison.Ordinal))
                {
                    return SaveFaultPoint.PartialGenerationStage;
                }

                if (name.Value.StartsWith(
                        ".stage-last-good-",
                        StringComparison.Ordinal))
                {
                    return SaveFaultPoint.PartialLastGoodPointerStage;
                }

                if (name.Value.StartsWith(
                        ".stage-current-",
                        StringComparison.Ordinal))
                {
                    return SaveFaultPoint.PartialCurrentPointerStage;
                }

                return SaveFaultPoint.None;
            }
        }
    }
}
