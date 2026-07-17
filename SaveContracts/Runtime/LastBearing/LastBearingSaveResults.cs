#nullable enable

using System;

namespace AtomicLandPirate.Save.LastBearing
{
    public static class LastBearingSaveCodes
    {
        public const string SaveOk = "LB_SAVE_OK";
        public const string RecoveredLastGood = "LB_SAVE_RECOVERED_LAST_GOOD";
        public const string NoProfile = "LB_SAVE_NO_PROFILE";
        public const string CurrentCorrupt = "LB_SAVE_CURRENT_CORRUPT";
        public const string BothCorrupt = "LB_SAVE_BOTH_CORRUPT";
        public const string UnknownVersion = "LB_SAVE_UNKNOWN_VERSION";
        public const string ConfinementFailure = "LB_SAVE_CONFINEMENT_FAILURE";
        public const string WriterBusy = "LB_SAVE_WRITER_BUSY";
        public const string InterruptedWrite = "LB_SAVE_INTERRUPTED_WRITE";
    }

    public sealed class LastBearingPersistResult
    {
        private LastBearingPersistResult(
            bool succeeded,
            string code,
            ulong generation,
            bool alreadyCurrent,
            long bytesWritten)
        {
            Succeeded = succeeded;
            Code = code;
            Generation = generation;
            AlreadyCurrent = alreadyCurrent;
            BytesWritten = bytesWritten;
        }

        public bool Succeeded { get; }

        public string Code { get; }

        public ulong Generation { get; }

        public bool AlreadyCurrent { get; }

        public long BytesWritten { get; }

        internal static LastBearingPersistResult Success(
            ulong generation,
            bool alreadyCurrent,
            long bytesWritten)
        {
            return new LastBearingPersistResult(
                true,
                LastBearingSaveCodes.SaveOk,
                generation,
                alreadyCurrent,
                bytesWritten);
        }

        internal static LastBearingPersistResult Failure(
            string code,
            ulong generation = 0)
        {
            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            return new LastBearingPersistResult(false, code, generation, false, 0);
        }
    }

    public sealed class LastBearingLoadResult
    {
        private readonly byte[]? _canonicalPayload;

        private LastBearingLoadResult(
            bool succeeded,
            string code,
            ulong generation,
            bool fromLastGood,
            byte[]? canonicalPayload)
        {
            Succeeded = succeeded;
            Code = code;
            Generation = generation;
            FromLastGood = fromLastGood;
            _canonicalPayload = Clone(canonicalPayload);
        }

        public bool Succeeded { get; }

        public string Code { get; }

        public ulong Generation { get; }

        public bool FromLastGood { get; }

        public byte[]? CanonicalPayload => Clone(_canonicalPayload);

        internal static LastBearingLoadResult Success(
            string code,
            ulong generation,
            bool fromLastGood,
            byte[] canonicalPayload)
        {
            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (canonicalPayload is null)
            {
                throw new ArgumentNullException(nameof(canonicalPayload));
            }

            return new LastBearingLoadResult(
                true,
                code,
                generation,
                fromLastGood,
                canonicalPayload);
        }

        internal static LastBearingLoadResult Failure(string code)
        {
            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            return new LastBearingLoadResult(false, code, 0, false, null);
        }

        private static byte[]? Clone(byte[]? value)
        {
            return value is null ? null : (byte[])value.Clone();
        }
    }
}
